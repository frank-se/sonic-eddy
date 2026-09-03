// midi-cube: renders incoming MIDI note-on/off events as a 3D "cube" - X is
// time, Y is note number, Z is channel (each channel its own plane) - and
// pushes the result out as its own PipeWire video source, so any downstream
// PipeWire compositor/consumer (in particular this repo's own
// pw-video-compositor) can pick it up like any other camera.
//
// Phase 3 (this file, current version): real MIDI note-on/off input, parsed
// by RunningStatusParser and tracked in a NoteRegistry, rendered by raylib
// on its own thread, publishing finished RGBA frames for the PipeWire RT
// process callback to push out. See cube_renderer.hpp for why rendering
// can't happen directly in that RT callback (raylib's GL context is
// thread-affine).
//
// Self-driven output: PW_STREAM_FLAG_DRIVER + a repeating pw_loop timer
// calling pw_stream_trigger_process(), the same idiom test/av_sync_record.cpp
// uses to drive an otherwise driver-less PipeWire component (see that file's
// header comment for why a plain producer/consumer pair may not tick on its
// own) - verified empirically in Phase 1 to be necessary in this environment.
#include <array>
#include <atomic>
#include <chrono>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/control/control.h>
#include <spa/param/buffers.h>
#include <spa/param/format.h>
#include <spa/param/video/format-utils.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

#include "cube_renderer.hpp"
#include "midi_cube_midi.hpp"
#include "midi_note_registry.hpp"

namespace {

constexpr uint32_t kBytesPerPixel = 4; // SPA_VIDEO_FORMAT_RGBA

struct App {
  pw_main_loop *main_loop = nullptr;
  pw_stream *out_stream = nullptr;
  pw_stream *midi_in_stream = nullptr;
  spa_source *frame_timer = nullptr;
  uint32_t width = 1280;
  uint32_t height = 720;
  uint32_t fps = 30;
  double time_window_seconds = 8.0;
  midi_cube::CameraConfig camera;

  // MIDI input stream's RT process callback (single stream, so a single
  // parser instance is never touched concurrently) feeds this; the render
  // thread reads it via NoteRegistry::snapshot().
  midi_cube::RunningStatusParser midi_parser;
  midi_cube::NoteRegistry note_registry;

  // Render thread publishes here; RT process callback (on_output_process)
  // just memcpy's whatever's latest - same "RT callback does a dumb copy,
  // rendering happens elsewhere" split as fr-sonic/src/video/Producer.cpp.
  std::mutex frame_mutex;
  std::vector<uint8_t> latest_frame;

  std::atomic<bool> running{true};
  std::thread render_thread;
};

void render_thread_main(App *app_ptr) {
  auto &app = *app_ptr;
  midi_cube::CubeRenderer renderer(app.width, app.height, app.time_window_seconds,
                                   app.camera);

  std::vector<uint8_t> scratch;
  const auto frame_interval =
      std::chrono::duration_cast<std::chrono::steady_clock::duration>(
          std::chrono::duration<double>(1.0 / app.fps));

  while (app.running.load(std::memory_order_relaxed)) {
    const auto frame_start = std::chrono::steady_clock::now();
    const double now =
        std::chrono::duration<double>(frame_start.time_since_epoch()).count();

    const auto spans = app.note_registry.snapshot(now, app.time_window_seconds);
    renderer.render(spans, now, scratch);

    {
      std::lock_guard<std::mutex> lock(app.frame_mutex);
      app.latest_frame = scratch;
    }

    std::this_thread::sleep_until(frame_start + frame_interval);
  }
}

// Reads a PipeWire MIDI input buffer's spa_pod_sequence and feeds every
// SPA_CONTROL_Midi control body to the parser. Mirrors
// fr-sonic/src/midi/Receiver.cpp::process() exactly - this part is just how
// PipeWire hands you MIDI bytes, not fr-sonic-specific logic.
void on_midi_input_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.midi_in_stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas != 1 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(app.midi_in_stream, pw_buffer);
    return;
  }

  auto *pod = static_cast<spa_pod *>(
      spa_pod_from_data(buffer->datas[0].data, buffer->datas[0].maxsize,
                        buffer->datas[0].chunk->offset, buffer->datas[0].chunk->size));

  if (pod != nullptr && spa_pod_is_sequence(pod)) {
    const double now =
        std::chrono::duration<double>(std::chrono::steady_clock::now().time_since_epoch())
            .count();
    auto *sequence = reinterpret_cast<spa_pod_sequence *>(pod);
    spa_pod_control *pod_control;
    SPA_POD_SEQUENCE_FOREACH(sequence, pod_control) {
      if (pod_control->type != SPA_CONTROL_Midi)
        continue;
      const auto *bytes = static_cast<const uint8_t *>(SPA_POD_BODY(&pod_control->value));
      const uint32_t length = SPA_POD_BODY_SIZE(&pod_control->value);
      app.midi_parser.feed(bytes, length, app.note_registry, now);
    }
  }

  pw_stream_queue_buffer(app.midi_in_stream, pw_buffer);
}

const pw_stream_events midi_input_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_midi_input_process,
};

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
  if (spa_data.maxsize < needed) {
    pw_stream_queue_buffer(app.out_stream, pw_buffer);
    return;
  }

  auto *dst = static_cast<uint8_t *>(spa_data.data);
  {
    std::lock_guard<std::mutex> lock(app.frame_mutex);
    if (app.latest_frame.size() == needed)
      std::memcpy(dst, app.latest_frame.data(), needed);
    else
      std::memset(dst, 0, needed); // render thread hasn't produced a frame yet
  }

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = static_cast<uint32_t>(needed);
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(app.out_stream, pw_buffer);
}

// Once the concrete output format is negotiated, declare the buffer size we
// actually need and that buffers carry SPA_META_Header - without this,
// consumers like GStreamer's pipewiresrc silently drop every buffer. Mirrors
// pw-video-compositor/src/main.cpp's on_output_param_changed exactly (see
// that file's comment for the full explanation).
void on_output_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &app = *static_cast<App *>(data);
  if (param == nullptr || id != SPA_PARAM_Format)
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

const pw_stream_events output_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_output_param_changed,
    .process = on_output_process,
};

void on_frame_timer(void *data, uint64_t /*expirations*/) {
  auto &app = *static_cast<App *>(data);
  pw_stream_trigger_process(app.out_stream);
}

void on_quit_signal(void *data, int) {
  auto &app = *static_cast<App *>(data);
  pw_main_loop_quit(app.main_loop);
}

std::string node_name(const std::string &instance_name, const std::string &suffix) {
  return instance_name.empty() ? "se.midi-cube." + suffix
                                : "se.midi-cube." + instance_name + "." + suffix;
}

uint32_t parse_u32(const std::string &value) {
  return static_cast<uint32_t>(std::strtoul(value.c_str(), nullptr, 10));
}

double parse_double(const std::string &value) {
  return std::strtod(value.c_str(), nullptr);
}

struct Args {
  std::string instance_name;
  std::string midi_target; // optional PW_KEY_TARGET_OBJECT for the MIDI input stream
  uint32_t width = 1280;
  uint32_t height = 720;
  uint32_t fps = 30;
  double time_window_seconds = 8.0;
  midi_cube::CameraConfig camera; // CLI defaults mirror CameraConfig's own defaults
};

bool parse_args(int argc, char **argv, Args &args) {
  auto next = [&](int &i) -> std::string {
    return i + 1 < argc ? argv[++i] : std::string{};
  };
  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    if (arg == "--instance-name")
      args.instance_name = next(i);
    else if (arg == "--midi-target")
      args.midi_target = next(i);
    else if (arg == "--video-width")
      args.width = parse_u32(next(i));
    else if (arg == "--video-height")
      args.height = parse_u32(next(i));
    else if (arg == "--fps")
      args.fps = parse_u32(next(i));
    else if (arg == "--time-window-seconds")
      args.time_window_seconds = parse_double(next(i));
    else if (arg == "--camera-pos-x")
      args.camera.pos_x = static_cast<float>(parse_double(next(i)));
    else if (arg == "--camera-pos-y")
      args.camera.pos_y = static_cast<float>(parse_double(next(i)));
    else if (arg == "--camera-pos-z")
      args.camera.pos_z = static_cast<float>(parse_double(next(i)));
    else if (arg == "--camera-target-x")
      args.camera.target_x = static_cast<float>(parse_double(next(i)));
    else if (arg == "--camera-target-y")
      args.camera.target_y = static_cast<float>(parse_double(next(i)));
    else if (arg == "--camera-target-z")
      args.camera.target_z = static_cast<float>(parse_double(next(i)));
    else if (arg == "--camera-fov")
      args.camera.fov_y = static_cast<float>(parse_double(next(i)));
    else if (arg == "--background-r")
      args.camera.background_r = static_cast<uint8_t>(parse_u32(next(i)));
    else if (arg == "--background-g")
      args.camera.background_g = static_cast<uint8_t>(parse_u32(next(i)));
    else if (arg == "--background-b")
      args.camera.background_b = static_cast<uint8_t>(parse_u32(next(i)));
    else {
      std::cerr << "unknown argument: " << arg << '\n';
      return false;
    }
  }
  return true;
}

void print_usage() {
  std::cerr
      << "usage: midi-cube [--instance-name NAME] [--midi-target NAME_OR_ID]\n"
         "                  [--video-width W] [--video-height H] [--fps N]\n"
         "                  [--time-window-seconds S]\n"
         "                  [--camera-pos-x/-y/-z F] [--camera-target-x/-y/-z F] "
         "[--camera-fov DEG]\n"
         "                  [--background-r/-g/-b N]\n";
}

} // namespace

int main(int argc, char **argv) {
  Args args;
  if (!parse_args(argc, argv, args)) {
    print_usage();
    return 1;
  }

  App app;
  app.width = args.width;
  app.height = args.height;
  app.fps = args.fps == 0 ? 30 : args.fps;
  app.time_window_seconds = args.time_window_seconds;
  app.camera = args.camera;

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr) {
    std::cerr << "pw_main_loop_new failed\n";
    return 1;
  }
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  const std::string out_node_name = node_name(args.instance_name, "out");
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "Video", PW_KEY_MEDIA_CLASS, "Stream/Output/Video",
      PW_KEY_NODE_NAME, out_node_name.c_str(), PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy MIDI cube visualizer", nullptr);

  app.out_stream = pw_stream_new_simple(loop, out_node_name.c_str(), properties,
                                        &output_stream_events, &app);
  if (app.out_stream == nullptr) {
    std::cerr << "pw_stream_new_simple failed\n";
    return 1;
  }

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  // framerate.denom == 0 means "unconstrained" at EnumFormat time (see
  // pw-video-compositor/src/main.cpp's connect_video_stream comment) - a
  // fixed rate here would require peers to negotiate that exact rate.
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = SPA_VIDEO_FORMAT_RGBA,
                                            .size = SPA_RECTANGLE(app.width, app.height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {
      spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  const auto result = pw_stream_connect(
      app.out_stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS |
                                   PW_STREAM_FLAG_DRIVER),
      params, 1);
  if (result < 0) {
    std::cerr << out_node_name << ": pw_stream_connect failed: " << result << '\n';
    return 1;
  }

  // MIDI input: mirrors fr-sonic/src/midi/Receiver.cpp::setup() (media type
  // application/control, "8 bit raw midi" DSP format) but reimplemented
  // standalone here - no fr-sonic dependency, per this tool's design.
  const std::string midi_in_node_name = node_name(args.instance_name, "midi-in");
  auto *midi_properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Midi", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_MEDIA_CLASS, "Stream/Input/Midi",
      PW_KEY_FORMAT_DSP, "8 bit raw midi", PW_KEY_NODE_NAME,
      midi_in_node_name.c_str(), PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy MIDI cube visualizer - MIDI input", nullptr);
  if (!args.midi_target.empty())
    pw_properties_set(midi_properties, PW_KEY_TARGET_OBJECT, args.midi_target.c_str());

  app.midi_in_stream = pw_stream_new_simple(loop, midi_in_node_name.c_str(),
                                            midi_properties, &midi_input_stream_events,
                                            &app);
  if (app.midi_in_stream == nullptr) {
    std::cerr << "pw_stream_new_simple (midi) failed\n";
    return 1;
  }

  std::array<uint8_t, 256> midi_pod_buffer{};
  auto midi_builder = SPA_POD_BUILDER_INIT(midi_pod_buffer.data(), midi_pod_buffer.size());
  spa_pod_frame midi_frame{};
  spa_pod_builder_push_object(&midi_builder, &midi_frame, SPA_TYPE_OBJECT_Format,
                              SPA_PARAM_EnumFormat);
  spa_pod_builder_add(&midi_builder, SPA_FORMAT_mediaType,
                      SPA_POD_Id(SPA_MEDIA_TYPE_application), SPA_FORMAT_mediaSubtype,
                      SPA_POD_Id(SPA_MEDIA_SUBTYPE_control), 0);
  const spa_pod *midi_params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_pop(&midi_builder, &midi_frame))};

  const auto midi_result = pw_stream_connect(
      app.midi_in_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                   PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      midi_params, 1);
  if (midi_result < 0) {
    std::cerr << midi_in_node_name << ": pw_stream_connect failed: " << midi_result << '\n';
    return 1;
  }

  app.render_thread = std::thread(render_thread_main, &app);

  app.frame_timer = pw_loop_add_timer(loop, on_frame_timer, &app);
  const int64_t interval_ns = 1'000'000'000LL / app.fps;
  struct timespec value = {0, interval_ns};
  struct timespec interval = {0, interval_ns};
  pw_loop_update_timer(loop, app.frame_timer, &value, &interval, false);

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  std::cout << "midi-cube running (" << app.width << "x" << app.height << " @ "
            << app.fps << "fps), output node: " << out_node_name << '\n'
            << std::flush;
  pw_main_loop_run(app.main_loop);

  app.running.store(false, std::memory_order_relaxed);
  app.render_thread.join();

  pw_loop_destroy_source(loop, app.frame_timer);
  pw_stream_destroy(app.midi_in_stream);
  pw_stream_destroy(app.out_stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
