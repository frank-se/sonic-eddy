// Small standalone PipeWire node: two RGBA video inputs, one RGBA output,
// linearly cross-dissolves the two inputs based on a "blend_position" float
// (0..1) received over Props. Companion to pw-video-compositor - meant to
// sit downstream of two --instance-name'd compositor instances (see
// node_name() in main.cpp), blending their outputs for a simple T-bar-style
// M/E switcher prototype. Deliberately standalone/self-contained rather than
// sharing main.cpp's App/FrameSource types - the shapes only partially
// overlap and this file is small enough that duplicating them is clearer
// than factoring out a shared header for a two-input-one-output special case.
//
// Scope cuts, all deliberate for this first prototype pass:
// - No scaling: both inputs and the output must already share the same
//   width/height (set via --width/--height, matching both upstream
//   compositors' canvas size) - a mismatched frame is simply blended
//   byte-for-byte against whatever's in the other slot, which will look
//   wrong but won't crash. (This constraint is about the blend itself -
//   see the separate --preview monitor window below, which does its own
//   independent downscaling and isn't subject to it.)
// - Inputs auto-link via --in0-target/--in1-target (sets PW_KEY_TARGET_OBJECT
//   + PW_STREAM_FLAG_AUTOCONNECT) when given; omit either to keep that
//   stream on manual `pw-link`. The output is never given a target here -
//   this project's convention is the consumer side sets target_object (the
//   producer is usually outside our control), so se.video-blender.out is
//   whatever downstream node's own --*-target points back at this node's
//   name.
//
// Optional --preview: opens a local GStreamer/waylandsink window showing two
// downscaled (1/4 linear size) panes stacked vertically - top is "program"
// (the actual blended output, identical to what's published on
// se.video-blender.out), bottom is "preview" (the *minority*-weight raw
// input, unblended: t<0.5 shows input B at full strength, t>=0.5 shows
// input A). Minority rather than majority so the preview pane never
// converges with program at the t=0/t=1 extremes - it always shows "the
// side you'd land back on fully if you reversed the fader from here".
#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <mutex>
#include <string>
#include <thread>

#include <boost/lockfree/spsc_value.hpp>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/buffers.h>
#include <spa/param/props.h>
#include <spa/param/video/format-utils.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

#include <gst/app/gstappsrc.h>
#include <gst/gst.h>

namespace {

constexpr uint32_t kBytesPerPixel = 4; // SPA_VIDEO_FORMAT_RGBA
constexpr size_t kInputCount = 2;

// Every RT frame buffer below is a fixed-size std::array, capped at this
// canvas size, rather than a runtime-sized std::vector. That's what lets
// boost::lockfree::spsc_value (see FrameSource/PreviewSlot below) hold them
// directly - it default-constructs its 3 internal slots with no way to pass
// a runtime size in, so only compile-time-sized payloads fit without the RT
// thread ever allocating. 1920x1080 covers every canvas/camera config in
// this repo as of 2026-09-08 (video-setup/**/*.json, cameras/*.fish) - bump
// this and rebuild if a larger canvas is ever needed. Configs that exceed it
// are rejected at startup (see the check in main()), never silently
// truncated.
constexpr uint32_t kMaxCanvasWidth = 1920;
constexpr uint32_t kMaxCanvasHeight = 1080;
constexpr size_t kMaxFrameBytes =
    static_cast<size_t>(kMaxCanvasWidth) * kMaxCanvasHeight * kBytesPerPixel;

// Payload for FrameSource's spsc_value. allow_multiple_reads<true> (see
// FrameSource below) means "peek at the latest value any number of times",
// which is what on_output_process needs - it's fine to fall back to a
// slightly-stale (or, before the first frame, all-zero/has_frame=false)
// value; it never needs to distinguish "no update since last read".
struct FrameSlot {
  std::array<uint8_t, kMaxFrameBytes> data{};
  bool has_frame = false;
};

// FrameSource: one video input's producer(RT)/consumer(RT) handoff.
// - buffer: the audited boost::lockfree triple buffer - the only thing that
//   crosses the on_input_process/on_output_process thread boundary.
// - write_scratch: on_input_process's own persistent staging buffer. Never
//   touched by any other thread - it exists purely so on_input_process has
//   somewhere to memcpy the incoming PipeWire buffer into before handing a
//   *copy* to buffer.write(), without allocating (or reusing a giant stack
//   local) on every RT callback.
struct FrameSource {
  uint32_t width = 0;
  uint32_t height = 0;
  boost::lockfree::spsc_value<FrameSlot, boost::lockfree::allow_multiple_reads<true>> buffer;
  FrameSlot write_scratch;
  pw_stream *stream = nullptr;
};

// Payload for App::preview_buffer. Plain drain-queue semantics (default,
// not allow_multiple_reads) - preview_thread only ever wants "the next
// frame I haven't pushed yet", not "peek repeatedly".
//
// program/side hold full-canvas-sized (pre-downscale) frames - on_output_
// process copies the actual blended output (and the minority-weight raw
// input) in at full resolution; push_preview_frame does the 1/4-linear-size
// shrink for the monitor window afterwards, on preview_thread. So these
// need kMaxFrameBytes capacity, same as FrameSlot::data, NOT the smaller
// downscaled-pane size - sizing them to the pane size was a real bug (8MB
// memcpy into a ~512KB array) caught by a live SEGV in on_output_process.
struct PreviewSlot {
  std::array<uint8_t, kMaxFrameBytes> program{};
  std::array<uint8_t, kMaxFrameBytes> side{};
  bool side_valid = false;
};

struct App {
  pw_main_loop *main_loop = nullptr;
  uint32_t width = 0;
  uint32_t height = 0;
  std::array<std::string, kInputCount> in_target; // optional PW_KEY_TARGET_OBJECT per input

  std::array<FrameSource, kInputCount> sources;
  pw_stream *out_stream = nullptr;

  // Control thread (param_changed) writes this, process() reads it on the RT
  // graph thread - relaxed is enough since it's an independent scalar
  // "last write wins" value, same idiom as pw-video-compositor's
  // App::active_scene_index (main.cpp).
  std::atomic<float> blend_position{0.0f};

  // --preview: local monitor window (program+preview stacked, downscaled).
  bool preview_enabled = false;
  GstElement *preview_pipeline = nullptr;
  // Two appsrcs (program, side), each pushed a full-resolution frame -
  // vapostproc does the downscale to preview_pane_width/height inside the
  // pipeline (see main()), not hand-rolled C++ (downscale_nearest used to
  // live here; GStreamer's scaler is better-tested and, via vapostproc,
  // can be hardware-accelerated - same element the camera scripts already
  // use, see cameras/stream-*.fish).
  GstElement *preview_appsrc_program = nullptr;
  GstElement *preview_appsrc_side = nullptr;
  uint32_t preview_pane_width = 0;
  uint32_t preview_pane_height = 0;

  // on_output_process (RT thread, SCHED_FIFO) must never call into
  // GStreamer/Wayland directly - gst_app_src_push_buffer can block on a
  // Wayland round-trip to the compositor, and blocking a realtime thread on
  // that risks priority inversion (kwin's own input-handling thread starved
  // while it never gets scheduled), not just a dropped video frame. So the
  // RT thread only memcpy's, via boost::lockfree::spsc_value - NOT a mutex,
  // and not a hand-rolled primitive either (see feedback_rt_thread_no_locks
  // / feedback_no_custom_concurrency_primitives memories for why both of
  // those are unacceptable here) - and preview_thread owns the actual
  // push_preview_frame/GStreamer calls. preview_write_scratch is
  // on_output_process's own persistent staging slot, same reasoning as
  // FrameSource::write_scratch above.
  boost::lockfree::spsc_value<PreviewSlot> preview_buffer;
  PreviewSlot preview_write_scratch;
  std::condition_variable preview_wake_cv; // wakeup hint only, no data behind it
  std::atomic<bool> preview_thread_running{false};
  std::thread preview_thread;
};

void on_input_process(void *data) {
  auto &source = *static_cast<FrameSource *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(source.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(source.stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  const size_t expected =
      static_cast<size_t>(source.width) * source.height * kBytesPerPixel;
  // chunk->size alone isn't trustworthy - maxsize is the actual mapped
  // buffer size and can be smaller (e.g. transitional negotiation buffers).
  // kMaxFrameBytes clamp is defense in depth - main() already rejects
  // width/height configs that would exceed it.
  const size_t copy_size = std::min<size_t>(
      {expected, spa_data.chunk->size, static_cast<size_t>(spa_data.maxsize),
       kMaxFrameBytes});
  if (copy_size > 0) {
    std::memcpy(source.write_scratch.data.data(), spa_data.data, copy_size);
    source.write_scratch.has_frame = true;
    source.buffer.write(source.write_scratch); // copy - write_scratch stays ours to reuse
  }

  pw_stream_queue_buffer(source.stream, pw_buffer);
}

// Wraps one full-resolution (app.width x app.height) RGBA frame into a
// GstBuffer and pushes it to the given appsrc. No scaling here - each
// appsrc's branch in the preview pipeline (see main()) runs its own
// vapostproc down to preview_pane_width/height; letting GStreamer do the
// scaling means no hand-rolled resampling code and, via vapostproc, a
// hardware-accelerated scale on the same path the camera scripts already
// use (cameras/stream-*.fish).
void push_preview_source(GstElement *appsrc, uint32_t width, uint32_t height,
                         const uint8_t *frame) {
  const size_t frame_size =
      static_cast<size_t>(width) * height * kBytesPerPixel;
  auto *gst_buffer = gst_buffer_new_allocate(nullptr, frame_size, nullptr);
  GstMapInfo map;
  gst_buffer_map(gst_buffer, &map, GST_MAP_WRITE);
  if (frame != nullptr)
    std::memcpy(map.data, frame, frame_size);
  else
    std::memset(map.data, 0, frame_size); // source hasn't produced a frame yet
  gst_buffer_unmap(gst_buffer, &map);

  gst_app_src_push_buffer(GST_APP_SRC(appsrc), gst_buffer);
}

// preview_src may be null (source hasn't produced a frame yet) - matches
// on_output_process's own missing-source-is-black behavior.
void push_preview_frame(App &app, const uint8_t *program,
                        const uint8_t *preview_src) {
  push_preview_source(app.preview_appsrc_program, app.width, app.height, program);
  push_preview_source(app.preview_appsrc_side, app.width, app.height, preview_src);
}

// Owns every GStreamer/Wayland call for the preview path, off the PipeWire
// RT thread (see App::preview_buffer comment). Drains app.preview_buffer via
// its wait-free consume(); the wait_for below is a plain sleep/wake hint
// with a bounded timeout fallback, not a data handshake - the RT thread's
// notify_one() call is fire-and-forget (no mutex held, may be missed if this
// thread isn't in wait() yet), so a bounded timeout is what guarantees a
// published frame is never stuck for longer than kWakePollInterval even if
// a notification is lost.
void preview_thread_main(App *app_ptr) {
  auto &app = *app_ptr;
  constexpr auto kWakePollInterval = std::chrono::milliseconds(50);
  std::mutex wake_mutex; // guards only the condition_variable wait, never the frame data

  // Persists across wake ticks so a tick with no new RT-published frame
  // still has something to push - starts black/side_valid=false, matching
  // on_output_process's own missing-source-is-black behavior for the state
  // before anything is connected downstream (on_output_process bails out
  // early via pw_stream_dequeue_buffer whenever nothing is pulling this
  // node's output yet, so app.preview_buffer may never get a single write).
  PreviewSlot last_slot;

  auto tick = [&app, &last_slot] {
    while (app.preview_buffer.consume(
        [&last_slot](const PreviewSlot &slot) { last_slot = slot; })) {
    }
    // Pushed every tick unconditionally, not only when something new
    // arrived: waylandsink/the compositor need a steady stream of buffer
    // commits to this window from the moment it's mapped, or the surface
    // can wedge the compositor before it ever receives a first frame - "no
    // data until we have real data" was itself the 2026-09-08 preview-freeze
    // bug, not a benign startup gap.
    push_preview_frame(app, last_slot.program.data(),
                       last_slot.side_valid ? last_slot.side.data() : nullptr);
  };

  while (app.preview_thread_running.load(std::memory_order_relaxed)) {
    {
      std::unique_lock<std::mutex> lock(wake_mutex);
      app.preview_wake_cv.wait_for(lock, kWakePollInterval);
    }
    tick();
  }
  tick(); // flush anything published right before shutdown
}

void on_output_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.out_stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(app.out_stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  const uint32_t stride = app.width * kBytesPerPixel;
  const size_t needed = static_cast<size_t>(stride) * app.height;
  // maxsize is the actual mapped buffer size; a transitional negotiation
  // buffer can report maxsize 0 even though data/chunk are non-null.
  if (spa_data.maxsize < needed) {
    pw_stream_queue_buffer(app.out_stream, pw_buffer);
    return;
  }
  auto *dst = static_cast<uint8_t *>(spa_data.data);

  const float t = app.blend_position.load(std::memory_order_relaxed);

  // consume() with allow_multiple_reads<true> always succeeds (even before
  // the first write, returning the default-constructed has_frame=false
  // slot) and is wait-free - no lock, no blocking, bounded time regardless
  // of on_input_process's scheduling. The captured pointers stay valid past
  // the lambda because they point into FrameSource::buffer's own
  // permanently-allocated storage, not into anything that can be freed or
  // reused before this function calls consume() again (it doesn't).
  bool has_a = false, has_b = false;
  const uint8_t *a = nullptr;
  const uint8_t *b = nullptr;
  app.sources[0].buffer.consume([&](const FrameSlot &slot) {
    has_a = slot.has_frame;
    a = slot.data.data();
  });
  app.sources[1].buffer.consume([&](const FrameSlot &slot) {
    has_b = slot.has_frame;
    b = slot.data.data();
  });
  if (!has_a)
    a = nullptr;
  if (!has_b)
    b = nullptr;

  // A missing source is treated as all-zero/black, matching pw-video-
  // compositor's own graceful-degradation behavior for objects whose source
  // hasn't produced a frame yet. RGB channels get a plain linear cross-
  // dissolve; alpha is forced to fully opaque (255) rather than blended -
  // blending it the same way produced genuinely partial-transparency pixels
  // (alpha < 255) whenever a source was missing or t wasn't exactly 0/1,
  // which got sent straight through to the --preview window's waylandsink
  // as real see-through content. That's what "transparent images" (see
  // project_video_blender_preview_rt_thread_freeze memory) actually was -
  // not a KWin/Wayland bug, just us handing it a non-opaque surface. A
  // compositor's output - and its preview - should never be see-through.
  for (size_t i = 0; i < needed; ++i) {
    if ((i & 3) == 3) {
      dst[i] = 255;
      continue;
    }
    const float av = a != nullptr ? static_cast<float>(a[i]) : 0.0f;
    const float bv = b != nullptr ? static_cast<float>(b[i]) : 0.0f;
    dst[i] = static_cast<uint8_t>(
        std::clamp(av * (1.0f - t) + bv * t, 0.0f, 255.0f));
  }

  if (app.preview_enabled) {
    // Minority-weight side, so preview never converges with program at the
    // t=0/t=1 extremes - see file header for the rationale. Only memcpy here
    // - preview_thread does the actual GStreamer push (see App::preview_buffer).
    // write() is a wait-free copy into spsc_value's own storage; notify_one()
    // here holds no mutex, so neither call can ever block this RT thread.
    const uint8_t *preview_src = (t < 0.5f) ? b : a;
    auto &slot = app.preview_write_scratch;
    std::memcpy(slot.program.data(), dst, needed);
    if (preview_src != nullptr) {
      std::memcpy(slot.side.data(), preview_src, needed);
      slot.side_valid = true;
    } else {
      slot.side_valid = false;
    }
    app.preview_buffer.write(slot);
    app.preview_wake_cv.notify_one();
  }

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = static_cast<uint32_t>(needed);
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(app.out_stream, pw_buffer);
}

void on_quit_signal(void *data, int) {
  auto &app = *static_cast<App *>(data);
  pw_main_loop_quit(app.main_loop);
}

// Mirrors pw-video-compositor's handle_output_props() (main.cpp): walk the
// SPA_PROP_params struct treating even entries as keys and odd as values.
// Only one key is recognized here, "blend_position" (float, clamped 0..1).
void handle_output_props(App &app, const spa_pod *param) {
  const auto *params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr || params_prop->value.type != SPA_TYPE_Struct)
    return;

  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(static_cast<spa_pod *>(SPA_POD_BODY(&params_prop->value)),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key != nullptr && std::strcmp(key, "blend_position") == 0) {
      float value = 0.0f;
      if (spa_pod_get_float(child, &value) == 0)
        app.blend_position.store(std::clamp(value, 0.0f, 1.0f),
                                 std::memory_order_relaxed);
    }
    ++index;
  }
}

// Once the concrete output format is negotiated, declare the buffer size we
// actually need and that buffers carry SPA_META_Header - same rationale as
// pw-video-compositor's on_output_param_changed (main.cpp): without
// ParamBuffers/ParamMeta(Header), consumers like GStreamer's pipewiresrc
// silently starve/drop every buffer.
void on_output_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &app = *static_cast<App *>(data);
  if (param == nullptr)
    return;

  if (id == SPA_PARAM_Props) {
    handle_output_props(app, param);
    return;
  }
  if (id != SPA_PARAM_Format)
    return;

  const uint32_t stride = app.width * kBytesPerPixel;
  std::array<uint8_t, 512> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  const spa_pod *params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers,
          SPA_PARAM_BUFFERS_buffers, SPA_POD_CHOICE_RANGE_Int(4, 2, 8),
          SPA_PARAM_BUFFERS_blocks, SPA_POD_Int(1), SPA_PARAM_BUFFERS_size,
          SPA_POD_Int(stride * app.height), SPA_PARAM_BUFFERS_stride,
          SPA_POD_Int(stride))),
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamMeta, SPA_PARAM_Meta,
          SPA_PARAM_META_type, SPA_POD_Id(SPA_META_Header), SPA_PARAM_META_size,
          SPA_POD_Int(sizeof(spa_meta_header))))};
  pw_stream_update_params(app.out_stream, params, 2);
}

const pw_stream_events input_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_input_process,
};

const pw_stream_events output_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_output_param_changed,
    .process = on_output_process,
};

pw_stream *connect_video_stream(pw_loop *loop, const char *name,
                                 const char *media_class,
                                 pw_direction direction,
                                 const pw_stream_events *events, void *user_data,
                                 uint32_t width, uint32_t height,
                                 const std::string &target_object = {}) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY,
      direction == PW_DIRECTION_INPUT ? "Capture" : "Playback",
      PW_KEY_MEDIA_ROLE, "Video", PW_KEY_MEDIA_CLASS, media_class,
      PW_KEY_NODE_NAME, name, PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy video blender", nullptr);
  if (!target_object.empty()) {
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT, target_object.c_str());
    // Without this, WirePlumber falls back to linking any other compatible
    // node (e.g. a raw camera device) when the named target isn't up yet -
    // causing a storm of doomed format-negotiation attempts against
    // unrelated nodes instead of just waiting for the real target.
    pw_properties_set(properties, "node.dont-fallback", "true");
    // Without this, WirePlumber's session-manager GC removes the node the
    // moment it notices it's unlinked (target not up yet) - it never gets a
    // chance to link later when the target actually appears.
    pw_properties_set(properties, "node.linger", "true");
  }

  auto *stream = pw_stream_new_simple(loop, name, properties, events, user_data);
  if (stream == nullptr)
    return nullptr;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = SPA_VIDEO_FORMAT_RGBA,
                                            .size = SPA_RECTANGLE(width, height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {
      spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  // AUTOCONNECT only when a target was actually given - PW_KEY_TARGET_OBJECT
  // alone does not make WirePlumber attempt a link; both are required
  // together.
  auto flags = static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                            PW_STREAM_FLAG_RT_PROCESS);
  if (!target_object.empty())
    flags = static_cast<pw_stream_flags>(flags | PW_STREAM_FLAG_AUTOCONNECT);

  const auto result = pw_stream_connect(stream, direction, PW_ID_ANY, flags, params, 1);
  if (result < 0) {
    std::cerr << name << ": pw_stream_connect failed: " << result << '\n';
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

} // namespace

int main(int argc, char **argv) {
  // App is heap/static-sized, not stack-sized: FrameSource's spsc_value now
  // holds fixed kMaxFrameBytes (~8MB) x3 slots x kInputCount, well beyond a
  // safe stack frame - see the kMaxCanvasWidth/Height comment above.
  static App app;

  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    auto next = [&]() { return std::string(i + 1 < argc ? argv[++i] : ""); };
    if (arg == "--width")
      app.width = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--height")
      app.height = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--preview")
      app.preview_enabled = true;
    else if (arg == "--in0-target")
      app.in_target[0] = next();
    else if (arg == "--in1-target")
      app.in_target[1] = next();
    else {
      std::cerr << "unknown argument: " << arg << '\n';
      return 1;
    }
  }
  if (app.width == 0 || app.height == 0) {
    std::cerr << "usage: video-blender --width W --height H [--preview] "
                 "[--in0-target NAME_OR_ID] [--in1-target NAME_OR_ID]\n";
    return 1;
  }
  if (app.width > kMaxCanvasWidth || app.height > kMaxCanvasHeight) {
    std::cerr << "video-blender: --width/--height (" << app.width << "x" << app.height
               << ") exceeds the compiled-in max (" << kMaxCanvasWidth << "x"
               << kMaxCanvasHeight << "). RT frame buffers are fixed-size for RT-safety "
                 "(see feedback_rt_thread_no_locks memory) - bump kMaxCanvasWidth/"
                 "kMaxCanvasHeight in video_blender.cpp and rebuild.\n";
    return 1;
  }

  for (auto &source : app.sources) {
    source.width = app.width;
    source.height = app.height;
  }

  if (app.preview_enabled) {
    app.preview_pane_width = std::max<uint32_t>(1, app.width / 4);
    app.preview_pane_height = std::max<uint32_t>(1, app.height / 4);

    gst_init(&argc, &argv);

    // Two full-resolution appsrcs (program, side) each scaled down to
    // preview_pane_width/height by plain CPU videoscale - deliberately NOT
    // vapostproc (VA-API/VCN hardware scaler). This preview path is fed
    // directly from appsrc with no upstream flow control tying it to real
    // camera/encoder timing, so a stall or burst here has previously been
    // suspected of reaching the GPU hardware scaler and contributing to the
    // amdgpu ring hangs on 2026-09-08 - see project_camera_gpu_hang_backlog
    // memory. videoscale is CPU-only, at some CPU cost, but removes that
    // hardware path entirely from this specific window regardless of
    // whether the suspicion is confirmed.
    // compositor stacks the two scaled panes (program on top, side on
    // bottom) into one taller frame for a single waylandsink window.
    // do-timestamp=true (rather than av_sync_record's manual epoch/CFR-grid
    // PTS bookkeeping) is enough here - there's no muxer to satisfy, this is
    // a pure live monitor. framerate=30/1 is a nominal caps value only;
    // actual display cadence follows however often on_output_process() fires
    // since sync=false. waylandsink, not autovideosink: autovideosink's
    // auto-selected sink triggers a GStreamer-CRITICAL assertion on this
    // system (see test/av_sync_record.cpp for the full story); waylandsink
    // is confirmed clean on its own. Each appsrc branch keeps its
    // leaky=downstream queue capped at 1 buffer right before videoscale, so
    // a stalled/slow scaler still can't build an unbounded backlog.
    const std::string src_caps =
        "video/x-raw,format=RGBA,width=" + std::to_string(app.width) +
        ",height=" + std::to_string(app.height) + ",framerate=30/1";
    const std::string pane_caps =
        "video/x-raw,format=RGBA,width=" + std::to_string(app.preview_pane_width) +
        ",height=" + std::to_string(app.preview_pane_height);
    const std::string pipeline_desc =
        "compositor name=comp sink_0::xpos=0 sink_0::ypos=0 sink_1::xpos=0 "
        "sink_1::ypos=" + std::to_string(app.preview_pane_height) +
        " ! video/x-raw,format=RGBA,width=" + std::to_string(app.preview_pane_width) +
        ",height=" + std::to_string(app.preview_pane_height * 2) +
        " ! videoconvert ! waylandsink sync=false "
        "appsrc name=vsrc_program is-live=true do-timestamp=true format=time "
        "caps=\"" + src_caps + "\" ! queue leaky=downstream max-size-buffers=1 "
        "max-size-bytes=0 max-size-time=0 ! videoscale ! " + pane_caps + " ! comp.sink_0 "
        "appsrc name=vsrc_side is-live=true do-timestamp=true format=time "
        "caps=\"" + src_caps + "\" ! queue leaky=downstream max-size-buffers=1 "
        "max-size-bytes=0 max-size-time=0 ! videoscale ! " + pane_caps + " ! comp.sink_1";

    GError *parse_error = nullptr;
    app.preview_pipeline = gst_parse_launch(pipeline_desc.c_str(), &parse_error);
    if (app.preview_pipeline == nullptr) {
      std::cerr << "preview: gst_parse_launch failed: "
                << (parse_error != nullptr ? parse_error->message : "?") << '\n';
      return 1;
    }
    app.preview_appsrc_program =
        gst_bin_get_by_name(GST_BIN(app.preview_pipeline), "vsrc_program");
    app.preview_appsrc_side =
        gst_bin_get_by_name(GST_BIN(app.preview_pipeline), "vsrc_side");
    if (gst_element_set_state(app.preview_pipeline, GST_STATE_PLAYING) ==
        GST_STATE_CHANGE_FAILURE) {
      std::cerr << "preview: failed to start gstreamer pipeline\n";
      return 1;
    }

    app.preview_thread_running.store(true, std::memory_order_relaxed);
    app.preview_thread = std::thread(preview_thread_main, &app);
  }

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr) {
    std::cerr << "pw_main_loop_new failed\n";
    return 1;
  }
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  static const char *kInputNames[kInputCount] = {"se.video-blender.in0",
                                                 "se.video-blender.in1"};
  for (size_t idx = 0; idx < kInputCount; ++idx) {
    app.sources[idx].stream = connect_video_stream(
        loop, kInputNames[idx], "Stream/Input/Video", PW_DIRECTION_INPUT,
        &input_stream_events, &app.sources[idx], app.width, app.height,
        app.in_target[idx]);
    if (app.sources[idx].stream == nullptr)
      return 1;
  }

  app.out_stream = connect_video_stream(loop, "se.video-blender.out",
                                        "Stream/Output/Video",
                                        PW_DIRECTION_OUTPUT, &output_stream_events,
                                        &app, app.width, app.height);
  if (app.out_stream == nullptr)
    return 1;

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  std::cout << "video-blender running (" << app.width << "x" << app.height
            << ")\n" << std::flush;
  pw_main_loop_run(app.main_loop);

  for (auto &source : app.sources)
    if (source.stream != nullptr)
      pw_stream_destroy(source.stream);
  pw_stream_destroy(app.out_stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();

  if (app.preview_enabled) {
    // Stop feeding new frames and join before touching the pipeline - the
    // worker thread is the only thing calling into it.
    app.preview_thread_running.store(false, std::memory_order_relaxed);
    app.preview_wake_cv.notify_all();
    app.preview_thread.join();

    gst_element_set_state(app.preview_pipeline, GST_STATE_NULL);
    gst_object_unref(app.preview_appsrc_program);
    gst_object_unref(app.preview_appsrc_side);
    gst_object_unref(app.preview_pipeline);
  }
  return 0;
}
