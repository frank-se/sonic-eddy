#include <algorithm>
#include <array>
#include <atomic>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <filesystem>
#include <iostream>
#include <mutex>
#include <string>
#include <vector>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/buffers.h>
#include <spa/param/props.h>
#include <spa/param/video/format-utils.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

#include <nlohmann/json.hpp>

#include "camera_config.hpp"
#include "scene.hpp"

namespace {

constexpr uint32_t kBytesPerPixel = 4; // SPA_VIDEO_FORMAT_RGBA
// Non-scene CLI mode is unrelated to scenes/camera_config - kept at a
// fixed 2 inputs (--in0-*/--in1-*) for quick manual testing.
constexpr size_t kCliInputCount = 2;
constexpr size_t kMaxScenes = 5;

// Where a frame actually comes from: either a live PipeWire input stream
// (camera-backed, `stream` non-null) or a one-shot decode at startup
// (image-backed, scene mode only, `stream` null, `has_frame` permanently
// true). Camera sources are shared - every scene's camera-type objects
// that target the same index point at the *same* FrameSource, so the
// physical camera is only captured once regardless of how many scenes or
// objects reference it.
struct FrameSource {
  uint32_t width = 0;
  uint32_t height = 0;
  std::vector<uint8_t> frame;
  std::mutex frame_mutex;
  bool has_frame = false;
  pw_stream *stream = nullptr;
  // True if any decoded pixel's alpha byte is < 255 - scanned once at image
  // load time (see stbi_load call site) so composite_input can skip the
  // per-pixel alpha check for the common case. Always false for camera
  // sources: GStreamer's videoconvert->RGBA (cameras/stream-*.fish) and the
  // mixer-overview producer (always-opaque UI, see
  // project_mixer_overview_video_stream memory) both guarantee alpha=255
  // everywhere, so there's nothing to scan per-frame.
  bool has_alpha = false;
};

// One object's compositing geometry within a scene: which FrameSource to
// sample, the precomputed nearest-neighbor sample maps, and where it lands
// on the canvas. Everything here is sized and built once, up front, so
// process() never allocates.
// Everything below (except `source`, which is fixed at build time) is
// live-controllable via the "object_params" Props channel - see
// handle_output_props(). layout_mutex guards all of it uniformly (rather
// than mixing lock-free atomics for the cheap fields with a mutex for the
// ones that need col_map/row_map rebuilt) so there's exactly one
// synchronization story per RenderSlot, matching FrameSource::frame_mutex's
// existing "lock for the whole composite_input body" precedent.
struct RenderSlot {
  FrameSource *source = nullptr; // shared (camera) or owned via App::image_sources (image)

  double scale_x = 1.0;
  double scale_y = 1.0;
  bool flip_horizontal = false;
  bool flip_vertical = false;
  uint32_t rotate = 0; // 0, 90, 180 or 270 - fixed at build time, no live control yet
  bool transposed = false; // true for rotate 90/270 - set by build_sample_maps

  uint32_t dst_x = 0;
  uint32_t dst_y = 0;
  uint32_t dst_width = 0;
  uint32_t dst_height = 0;

  bool visible = true;
  float red_gain = 1.0f;
  float green_gain = 1.0f;
  float blue_gain = 1.0f;
  // Blend factor against whatever's already painted at this pixel this
  // frame (0 = fully transparent, 1 = fully opaque/opaque fast path) -
  // distinct from `visible`, which is a hard on/off short-circuit.
  // Introduced for the downstream-effects node's T-bar-tied keyer objects,
  // which need a smooth fade rather than a hard cut.
  float opacity = 1.0f;

  // Non-transposed: col_map (size dst_width) -> source column, row_map
  // (size dst_height) -> source row. Transposed (90/270 rotation): the
  // roles swap - col_map (indexed by dst x) holds the source ROW and
  // row_map (indexed by dst y) holds the source COLUMN, since a 90/270
  // rotation can't be expressed as two independent per-axis maps.
  std::vector<uint32_t> col_map;
  std::vector<uint32_t> row_map;

  std::mutex layout_mutex;
};

struct Scene {
  std::string name;
  std::string file; // the --scene path this was loaded from (empty in CLI mode)
  // deque, not vector: RenderSlot holds a std::mutex (non-movable) - same
  // reasoning as App::image_sources below.
  std::deque<RenderSlot> render_slots;
  std::vector<size_t> paint_order; // indices into render_slots, back-to-front
};

struct App {
  pw_main_loop *main_loop = nullptr;
  uint32_t canvas_width = 1280;
  uint32_t canvas_height = 720;

  // deque, not array: input slot count is now runtime-determined (from
  // camera_config::load), and FrameSource holds a std::mutex (non-movable)
  // - same reasoning as image_sources below.
  std::deque<FrameSource> camera_sources;
  // deque, not vector: FrameSource holds a std::mutex (non-movable), and
  // RenderSlot::source keeps a raw pointer into this container that must
  // stay valid as later scenes' images are added - vector would both fail
  // to compile (mutex isn't MoveInsertable) and, worse, silently
  // invalidate those pointers on reallocation. deque never relocates
  // existing elements on growth, so both problems go away.
  std::deque<FrameSource> image_sources;

  // deque, not vector: Scene holds a deque<RenderSlot>, and RenderSlot's
  // mutex makes it neither copyable nor move-noexcept - vector's growth
  // path can fall back to copy-constructing Scene (which would try to
  // copy-construct each RenderSlot) unless Scene's move is provably
  // noexcept, which the compiler doesn't reliably infer through two
  // levels of container nesting. deque never needs to move/copy existing
  // elements on growth, sidestepping the question entirely.
  std::deque<Scene> scenes;
  std::atomic<int> active_scene_index{0}; // written from param_changed (control thread), read in process() (RT thread)
  bool props_enabled = false; // true only in scene mode - CLI mode has no scene list to expose

  pw_stream *out_stream = nullptr;
  std::array<uint8_t, 4096> params_buffer{};
};

void build_sample_maps(RenderSlot &slot, uint32_t src_width, uint32_t src_height) {
  slot.transposed = slot.rotate == 90 || slot.rotate == 270;
  const uint32_t rotated_width = slot.transposed ? src_height : src_width;
  const uint32_t rotated_height = slot.transposed ? src_width : src_height;

  // dst -> position within the rotated-but-unflipped frame.
  std::vector<uint32_t> rx(slot.dst_width);
  for (uint32_t x = 0; x < slot.dst_width; ++x) {
    uint32_t v = std::min<uint32_t>(static_cast<uint32_t>(x / slot.scale_x),
                                    rotated_width - 1);
    rx[x] = slot.flip_horizontal ? rotated_width - 1 - v : v;
  }
  std::vector<uint32_t> ry(slot.dst_height);
  for (uint32_t y = 0; y < slot.dst_height; ++y) {
    uint32_t v = std::min<uint32_t>(static_cast<uint32_t>(y / slot.scale_y),
                                    rotated_height - 1);
    ry[y] = slot.flip_vertical ? rotated_height - 1 - v : v;
  }

  slot.col_map.resize(slot.dst_width);
  slot.row_map.resize(slot.dst_height);

  if (!slot.transposed) {
    // rotate 0: source = (rx, ry); rotate 180 = flip both axes.
    for (uint32_t x = 0; x < slot.dst_width; ++x)
      slot.col_map[x] = slot.rotate == 180 ? src_width - 1 - rx[x] : rx[x];
    for (uint32_t y = 0; y < slot.dst_height; ++y)
      slot.row_map[y] = slot.rotate == 180 ? src_height - 1 - ry[y] : ry[y];
  } else {
    // rotate 90 CW:  src_x = ry,             src_y = src_height-1-rx
    // rotate 270 CW: src_x = src_width-1-ry, src_y = rx
    // col_map (indexed by dst x = rx) carries src_y; row_map (indexed by
    // dst y = ry) carries src_x - composite_input swaps their roles.
    for (uint32_t x = 0; x < slot.dst_width; ++x)
      slot.col_map[x] = slot.rotate == 90 ? src_height - 1 - rx[x] : rx[x];
    for (uint32_t y = 0; y < slot.dst_height; ++y)
      slot.row_map[y] = slot.rotate == 90 ? ry[y] : src_width - 1 - ry[y];
  }
}

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
  const size_t copy_size = std::min<size_t>(
      {expected, spa_data.chunk->size, static_cast<size_t>(spa_data.maxsize)});
  if (copy_size > 0) {
    std::lock_guard<std::mutex> lock(source.frame_mutex);
    std::memcpy(source.frame.data(), spa_data.data, copy_size);
    source.has_frame = true;
  }

  pw_stream_queue_buffer(source.stream, pw_buffer);
}

inline uint8_t apply_gain(uint8_t value, float gain) {
  return static_cast<uint8_t>(
      std::clamp(static_cast<float>(value) * gain, 0.0f, 255.0f));
}

// Blends a (already gain-adjusted) source channel against whatever's
// already at that destination pixel this frame, by `opacity` (0..1).
inline uint8_t blend_channel(uint8_t src, uint8_t dst, float opacity) {
  return static_cast<uint8_t>(std::clamp(
      static_cast<float>(src) * opacity + static_cast<float>(dst) * (1.0f - opacity),
      0.0f, 255.0f));
}

void composite_input(RenderSlot &slot, uint8_t *dst, uint32_t dst_stride) {
  std::lock_guard<std::mutex> layout_lock(slot.layout_mutex);
  if (!slot.visible)
    return;

  auto &source = *slot.source;
  std::lock_guard<std::mutex> lock(source.frame_mutex);
  if (!source.has_frame)
    return;

  // Fast path: identity gain, fully opaque (the defaults), and a source with
  // no per-pixel transparency keep today's plain memcpy - no performance
  // regression for objects that don't use color control, a T-bar-tied fade,
  // or an alpha-cutout image (frame/keyer PNGs).
  const bool identity_gain =
      slot.red_gain == 1.0f && slot.green_gain == 1.0f && slot.blue_gain == 1.0f;
  const bool opaque = slot.opacity >= 1.0f;
  const bool fast_path = identity_gain && opaque && !source.has_alpha;
  const float opacity = std::clamp(slot.opacity, 0.0f, 1.0f);

  const uint32_t src_stride = source.width * kBytesPerPixel;
  for (uint32_t y = 0; y < slot.dst_height; ++y) {
    uint8_t *dst_row = dst + static_cast<size_t>(slot.dst_y + y) * dst_stride +
                       static_cast<size_t>(slot.dst_x) * kBytesPerPixel;
    if (!slot.transposed) {
      const uint8_t *src_row =
          source.frame.data() + static_cast<size_t>(slot.row_map[y]) * src_stride;
      for (uint32_t x = 0; x < slot.dst_width; ++x) {
        const uint8_t *src_px =
            src_row + static_cast<size_t>(slot.col_map[x]) * kBytesPerPixel;
        uint8_t *dst_px = dst_row + static_cast<size_t>(x) * kBytesPerPixel;
        if (fast_path) {
          std::memcpy(dst_px, src_px, kBytesPerPixel);
        } else {
          const float weight =
              source.has_alpha ? opacity * (static_cast<float>(src_px[3]) / 255.0f) : opacity;
          dst_px[0] = blend_channel(apply_gain(src_px[0], slot.red_gain), dst_px[0], weight);
          dst_px[1] = blend_channel(apply_gain(src_px[1], slot.green_gain), dst_px[1], weight);
          dst_px[2] = blend_channel(apply_gain(src_px[2], slot.blue_gain), dst_px[2], weight);
          dst_px[3] = blend_channel(255, dst_px[3], weight);
        }
      }
    } else {
      const uint32_t src_col = slot.row_map[y];
      for (uint32_t x = 0; x < slot.dst_width; ++x) {
        const uint32_t src_row = slot.col_map[x];
        const uint8_t *src_px = source.frame.data() +
            static_cast<size_t>(src_row) * src_stride +
            static_cast<size_t>(src_col) * kBytesPerPixel;
        uint8_t *dst_px = dst_row + static_cast<size_t>(x) * kBytesPerPixel;
        if (fast_path) {
          std::memcpy(dst_px, src_px, kBytesPerPixel);
        } else {
          const float weight =
              source.has_alpha ? opacity * (static_cast<float>(src_px[3]) / 255.0f) : opacity;
          dst_px[0] = blend_channel(apply_gain(src_px[0], slot.red_gain), dst_px[0], weight);
          dst_px[1] = blend_channel(apply_gain(src_px[1], slot.green_gain), dst_px[1], weight);
          dst_px[2] = blend_channel(apply_gain(src_px[2], slot.blue_gain), dst_px[2], weight);
          dst_px[3] = blend_channel(255, dst_px[3], weight);
        }
      }
    }
  }
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
  const uint32_t stride = app.canvas_width * kBytesPerPixel;
  const size_t needed = static_cast<size_t>(stride) * app.canvas_height;
  // maxsize is the actual mapped buffer size; a transitional negotiation
  // buffer can report maxsize 0 even though data/chunk are non-null.
  if (spa_data.maxsize < needed) {
    pw_stream_queue_buffer(app.out_stream, pw_buffer);
    return;
  }
  auto *dst = static_cast<uint8_t *>(spa_data.data);
  std::memset(dst, 0, needed);

  // Control thread (param_changed) writes this, we read it here on the RT
  // graph thread - relaxed is enough since it's an independent scalar
  // "last write wins" value, not a synchronization point for other memory
  // (same idiom as fr-sonic's Ducker _param_* atomics).
  int idx = app.active_scene_index.load(std::memory_order_relaxed);
  if (idx < 0 || static_cast<size_t>(idx) >= app.scenes.size())
    idx = 0;
  auto &scene = app.scenes[idx];
  for (auto slot_idx : scene.paint_order)
    composite_input(scene.render_slots[slot_idx], dst, stride);

  spa_data.chunk->offset = 0;
  spa_data.chunk->size = stride * app.canvas_height;
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(app.out_stream, pw_buffer);
}

void on_quit_signal(void *data, int) {
  auto &app = *static_cast<App *>(data);
  pw_main_loop_quit(app.main_loop);
}

// Publishes the scene list + current active index as PipeWire Props on the
// output stream, mirroring fr-sonic's Ducker::publish_params()/Looper
// publish_params() idiom: a single SPA_PARAM_Props object whose
// SPA_PROP_params value is a struct of alternating (string key, value)
// pairs. The scene list is variable-length, so it goes out as a JSON
// string blob under "scenes" (the same escape hatch the looper uses for
// its loop list) rather than a native SPA array-of-structs pod.
void publish_scene_params(App &app) {
  if (app.out_stream == nullptr)
    return;

  nlohmann::json scenes_array = nlohmann::json::array();
  for (const auto &scene : app.scenes)
    scenes_array.push_back({{"name", scene.name}, {"file", scene.file}});
  const std::string scenes_json = scenes_array.dump();

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, app.params_buffer.data(), app.params_buffer.size());

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);

  spa_pod_builder_string(&builder, "active_scene_index");
  spa_pod_builder_int(&builder, app.active_scene_index.load(std::memory_order_relaxed));
  spa_pod_builder_string(&builder, "scenes");
  spa_pod_builder_string(&builder, scenes_json.c_str());

  spa_pod_builder_pop(&builder, &struct_frame);

  const spa_pod *params[] = {
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame))};
  pw_stream_update_params(app.out_stream, params, 1);
}

// Applies a partial per-object update from the "object_params" JSON blob,
// e.g. {"object":0,"dst_x":100,"dst_y":50,"visible":false,"red_gain":0.2}.
// Every field except "object" is optional - only present keys change.
// Treated as untrusted external input, not a startup config error: an
// unknown/out-of-range object index is silently ignored, never crashes or
// spams stderr. Per the "Props is a REST API for the backend" framing,
// this function only ever accepts final, absolute values (e.g. dst_x/
// dst_y) - any ABS/REL, "unify" or other derived UX logic is SonicEddy's
// job, not this one's.
void apply_object_params(App &app, const std::string &json_text) {
  nlohmann::json command;
  try {
    command = nlohmann::json::parse(json_text);
  } catch (const nlohmann::json::exception &) {
    return;
  }
  if (!command.is_object() || !command.contains("object") ||
      !command["object"].is_number_integer())
    return;

  const int scene_idx = app.active_scene_index.load(std::memory_order_relaxed);
  if (scene_idx < 0 || static_cast<size_t>(scene_idx) >= app.scenes.size())
    return;
  auto &scene = app.scenes[scene_idx];

  const int object_idx = command["object"].get<int>();
  std::cerr << "[debug] apply_object_params: " << json_text << '\n'; // TEMP
  if (object_idx < 0 || static_cast<size_t>(object_idx) >= scene.render_slots.size()) {
    std::cerr << "[debug] apply_object_params: object_idx " << object_idx
               << " out of range (render_slots.size()=" << scene.render_slots.size()
               << ")\n"; // TEMP
    return;
  }
  auto &slot = scene.render_slots[object_idx];

  std::lock_guard<std::mutex> lock(slot.layout_mutex);

  if (command.contains("dst_x") && command["dst_x"].is_number()) {
    const uint32_t requested = command["dst_x"].get<uint32_t>();
    slot.dst_x = app.canvas_width > slot.dst_width
                     ? std::min(requested, app.canvas_width - slot.dst_width)
                     : 0;
    std::cerr << "[debug] dst_x requested=" << requested
               << " applied=" << slot.dst_x << " dst_width=" << slot.dst_width
               << " canvas_width=" << app.canvas_width << '\n'; // TEMP
  }
  if (command.contains("dst_y") && command["dst_y"].is_number()) {
    const uint32_t requested = command["dst_y"].get<uint32_t>();
    slot.dst_y = app.canvas_height > slot.dst_height
                     ? std::min(requested, app.canvas_height - slot.dst_height)
                     : 0;
    std::cerr << "[debug] dst_y requested=" << requested
               << " applied=" << slot.dst_y << " dst_height=" << slot.dst_height
               << " canvas_height=" << app.canvas_height << '\n'; // TEMP
  }
  if (command.contains("visible") && command["visible"].is_boolean())
    slot.visible = command["visible"].get<bool>();
  if (command.contains("red_gain") && command["red_gain"].is_number())
    slot.red_gain = command["red_gain"].get<float>();
  if (command.contains("green_gain") && command["green_gain"].is_number())
    slot.green_gain = command["green_gain"].get<float>();
  if (command.contains("blue_gain") && command["blue_gain"].is_number())
    slot.blue_gain = command["blue_gain"].get<float>();
  if (command.contains("opacity") && command["opacity"].is_number())
    slot.opacity = std::clamp(command["opacity"].get<float>(), 0.0f, 1.0f);

  bool rebuild = false;
  if (command.contains("flip_horizontal") && command["flip_horizontal"].is_boolean()) {
    slot.flip_horizontal = command["flip_horizontal"].get<bool>();
    rebuild = true;
  }
  if (command.contains("flip_vertical") && command["flip_vertical"].is_boolean()) {
    slot.flip_vertical = command["flip_vertical"].get<bool>();
    rebuild = true;
  }
  if (rebuild)
    build_sample_maps(slot, slot.source->width, slot.source->height);
}

// Receives external Props updates (e.g. `pw-cli set-param <id> Props
// "{ params = [ \"active_scene_index\" 1 ] }"`), mirroring
// Ducker::handle_audio_params()/handle_param_value(): walk the
// SPA_PROP_params struct treating even entries as keys and odd entries as
// values. "active_scene_index" (int) switches the live scene;
// "object_params" (JSON string) is the generic per-object update channel -
// see apply_object_params(). The scene list itself is read-only (only
// ever published, never accepted from outside).
void handle_output_props(App &app, const spa_pod *param) {
  const auto *params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr || params_prop->value.type != SPA_TYPE_Struct)
    return;

  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  bool changed = false;
  SPA_POD_FOREACH(static_cast<spa_pod *>(SPA_POD_BODY(&params_prop->value)),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key != nullptr && std::strcmp(key, "active_scene_index") == 0) {
      int32_t value = 0;
      if (spa_pod_get_int(child, &value) == 0 && !app.scenes.empty()) {
        const int clamped =
            std::clamp(value, 0, static_cast<int>(app.scenes.size()) - 1);
        app.active_scene_index.store(clamped, std::memory_order_relaxed);
        changed = true;
      }
    } else if (key != nullptr && std::strcmp(key, "object_params") == 0) {
      const char *json_text = nullptr;
      if (child->type == SPA_TYPE_String &&
          spa_pod_get_string(child, &json_text) == 0 && json_text != nullptr)
        apply_object_params(app, json_text);
    }
    ++index;
  }

  if (changed)
    publish_scene_params(app); // echo confirmed state back
}

// Once the concrete output format is negotiated, declare the buffer size we
// actually need and that buffers carry SPA_META_Header. Without ParamBuffers,
// PipeWire may hand us buffers whose maxsize is too small for a full frame -
// our own maxsize guard in on_output_process then correctly refuses to
// write into them, but the net effect is every buffer stays empty (chunk
// size 0), which consumers like GStreamer's pipewiresrc silently drop.
// Without ParamMeta(Header), consumers have no per-buffer validity/timing
// metadata and drop buffers for that reason instead.
void on_output_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &app = *static_cast<App *>(data);
  if (param == nullptr)
    return;

  if (id == SPA_PARAM_Props) {
    if (app.props_enabled)
      handle_output_props(app, param);
    return;
  }
  if (id != SPA_PARAM_Format)
    return;

  const uint32_t stride = app.canvas_width * kBytesPerPixel;
  std::array<uint8_t, 512> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  const spa_pod *params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers,
          SPA_PARAM_BUFFERS_buffers, SPA_POD_CHOICE_RANGE_Int(4, 2, 8),
          SPA_PARAM_BUFFERS_blocks, SPA_POD_Int(1), SPA_PARAM_BUFFERS_size,
          SPA_POD_Int(stride * app.canvas_height), SPA_PARAM_BUFFERS_stride,
          SPA_POD_Int(stride))),
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamMeta, SPA_PARAM_Meta,
          SPA_PARAM_META_type, SPA_POD_Id(SPA_META_Header), SPA_PARAM_META_size,
          SPA_POD_Int(sizeof(spa_meta_header))))};
  pw_stream_update_params(app.out_stream, params, 2);

  if (app.props_enabled)
    publish_scene_params(app); // initial publish: scene list + index 0
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

struct Args {
  std::vector<std::string> scene_paths; // if non-empty, scene mode - all other fields below are ignored
  // Mandatory alongside scene_paths - see camera_config.hpp. The stable,
  // scene-independent list of input slots (count, size, name) shared
  // across every loaded scene.
  std::string inputs_path;

  // Empty by default, reproducing today's exact node names unchanged (so
  // existing single-instance scripts/manual pw-link usage keeps working).
  // Set via --instance-name to run two (or more) compositor processes
  // side by side without their PipeWire node names colliding - see
  // node_name() below.
  std::string instance_name;

  uint32_t canvas_width = 1280;
  uint32_t canvas_height = 720;
  // CLI (non-scene) mode only - scene mode gets each input's size from
  // --inputs instead (see camera_config.hpp). 0 = "not explicitly set",
  // CLI mode falls back to 960x720 at the point of use.
  std::array<uint32_t, kCliInputCount> in_width{0, 0};
  std::array<uint32_t, kCliInputCount> in_height{0, 0};
  std::array<double, kCliInputCount> in_scale{0.5, 0.5};
  std::array<std::string, kCliInputCount> in_target{};
};

// "se.video-compositor.<suffix>" by default, or
// "se.video-compositor.<instance_name>.<suffix>" when --instance-name is
// given - lets two (or more) instances coexist in the same PipeWire graph.
std::string node_name(const Args &args, const std::string &suffix) {
  return args.instance_name.empty()
             ? "se.video-compositor." + suffix
             : "se.video-compositor." + args.instance_name + "." + suffix;
}

uint32_t parse_u32(const std::string &value) {
  return static_cast<uint32_t>(std::strtoul(value.c_str(), nullptr, 10));
}

double parse_double(const std::string &value) {
  return std::strtod(value.c_str(), nullptr);
}

bool parse_args(int argc, char **argv, Args &args) {
  auto next = [&](int &i) -> std::string {
    if (i + 1 >= argc)
      return {};
    return argv[++i];
  };

  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    if (arg == "--scene") {
      if (args.scene_paths.size() >= kMaxScenes) {
        std::cerr << "too many --scene arguments (max " << kMaxScenes << ")\n";
        return false;
      }
      // Made absolute relative to *our* cwd right away - scene.file gets
      // published verbatim over Props (see publish_scene_params), and
      // whichever process reads it back (e.g. SonicEddy's Streaming
      // Controls window) almost certainly has a different cwd than us, so
      // a relative path would silently fail to resolve there.
      args.scene_paths.push_back(
          std::filesystem::absolute(next(i)).string());
    } else if (arg == "--inputs")
      args.inputs_path = next(i);
    else if (arg == "--instance-name")
      args.instance_name = next(i);
    else if (arg == "--canvas-width")
      args.canvas_width = parse_u32(next(i));
    else if (arg == "--canvas-height")
      args.canvas_height = parse_u32(next(i));
    else if (arg == "--in0-width")
      args.in_width[0] = parse_u32(next(i));
    else if (arg == "--in0-height")
      args.in_height[0] = parse_u32(next(i));
    else if (arg == "--in0-scale")
      args.in_scale[0] = parse_double(next(i));
    else if (arg == "--in0-target")
      args.in_target[0] = next(i);
    else if (arg == "--in1-width")
      args.in_width[1] = parse_u32(next(i));
    else if (arg == "--in1-height")
      args.in_height[1] = parse_u32(next(i));
    else if (arg == "--in1-scale")
      args.in_scale[1] = parse_double(next(i));
    else if (arg == "--in1-target")
      args.in_target[1] = next(i);
    else {
      std::cerr << "unknown argument: " << arg << '\n';
      return false;
    }
  }
  return true;
}

void print_usage() {
  std::cerr << "usage: pw-video-compositor [--instance-name NAME] --inputs <inputs.json> --scene <scene.json> [--scene <scene2.json> ...] (up to "
            << kMaxScenes << ")\n"
               "   or: pw-video-compositor [--instance-name NAME] "
               "--canvas-width W --canvas-height H "
               "--in0-width W --in0-height H --in0-scale S [--in0-target NAME] "
               "--in1-width W --in1-height H --in1-scale S [--in1-target NAME]\n"
               "--instance-name suffixes all node names (se.video-compositor.<NAME>.*) "
               "so multiple instances can run side by side.\n"
               "--inputs <inputs.json> is mandatory in scene mode: the stable, "
               "scene-independent list of input slots (count, size, name) - see "
               "camera_config.hpp.\n";
}

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
      "Sonic Eddy video compositor", nullptr);
  if (!target_object.empty())
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT, target_object.c_str());

  auto *stream = pw_stream_new_simple(loop, name, properties, events, user_data);
  if (stream == nullptr)
    return nullptr;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  // framerate.denom == 0 means "unconstrained" in the EnumFormat pod (see
  // spa_format_video_raw_build) - a fixed SPA_FRACTION(0, 1) would instead
  // require peers to negotiate exactly 0/1, which real sources can't match.
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = SPA_VIDEO_FORMAT_RGBA,
                                            .size = SPA_RECTANGLE(width, height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {
      spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  const auto result = pw_stream_connect(
      stream, direction, PW_ID_ANY,
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS |
                                   PW_STREAM_FLAG_RT_PROCESS),
      params, 1);
  if (result < 0) {
    std::cerr << name << ": pw_stream_connect failed: " << result << '\n';
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

} // namespace

int main(int argc, char **argv) {
  Args args;
  if (!parse_args(argc, argv, args)) {
    print_usage();
    return 1;
  }

  App app;
  // node_names[idx] / target_objects[idx]: the PipeWire input stream to
  // open for app.camera_sources[idx]. Always input_count (scene mode, from
  // --inputs) or kCliInputCount (CLI mode) entries.
  std::vector<std::string> node_names;
  std::vector<std::string> target_objects;

  // Build phase: layout, sample maps, and scratch buffers - all up front,
  // before any PipeWire stream exists. Nothing below this block allocates.
  if (!args.scene_paths.empty()) {
    app.props_enabled = true;

    if (args.inputs_path.empty()) {
      std::cerr << "scene mode requires --inputs <file.json>\n";
      return 1;
    }
    auto inputs = camera_config::load(args.inputs_path);
    if (!inputs)
      return 1;
    const size_t input_count = inputs->size();

    std::vector<scene::SceneConfig> scene_configs;
    scene_configs.reserve(args.scene_paths.size());
    for (const auto &path : args.scene_paths) {
      auto loaded = scene::load_scene(path);
      if (!loaded)
        return 1;
      scene_configs.push_back(std::move(*loaded));
    }

    app.canvas_width = scene_configs.front().canvas_width;
    app.canvas_height = scene_configs.front().canvas_height;
    for (const auto &cfg : scene_configs) {
      if (cfg.canvas_width != app.canvas_width ||
          cfg.canvas_height != app.canvas_height) {
        std::cerr << "scene: all --scene files must share the same "
                     "canvas_width/canvas_height (the output format is "
                     "negotiated once at startup)\n";
        return 1;
      }
    }

    for (const auto &cfg : scene_configs) {
      for (const auto &object : cfg.objects) {
        if (object.type != scene::ObjectType::Camera)
          continue;
        if (object.target_camera_index >= input_count) {
          std::cerr << "scene: target_camera_index "
                     << object.target_camera_index << " is out of range (0.."
                     << (input_count - 1) << ")\n";
          return 1;
        }
      }
    }

    // Camera sources are shared across every scene - always build and
    // (later) connect all input_count of them (per --inputs), regardless of
    // which scenes actually reference each index.
    app.camera_sources.resize(input_count);
    for (size_t idx = 0; idx < input_count; ++idx) {
      auto &src = app.camera_sources[idx];
      src.width = (*inputs)[idx].width;
      src.height = (*inputs)[idx].height;
      src.frame.assign(
          static_cast<size_t>(src.width) * src.height * kBytesPerPixel, 0);
    }
    node_names.assign(input_count, "");
    target_objects.assign(input_count, "");
    for (size_t idx = 0; idx < input_count; ++idx)
      node_names[idx] = node_name(args, "in" + std::to_string(idx));

    for (size_t scene_i = 0; scene_i < scene_configs.size(); ++scene_i) {
      const auto &cfg = scene_configs[scene_i];
      app.scenes.emplace_back();
      auto &scene = app.scenes.back();
      scene.name = cfg.name;
      scene.file = args.scene_paths[scene_i];

      for (const auto &object : cfg.objects) {
        scene.render_slots.emplace_back();
        auto &slot = scene.render_slots.back();

        slot.dst_x = object.x < 0 ? 0 : static_cast<uint32_t>(object.x);
        slot.dst_y = object.y < 0 ? 0 : static_cast<uint32_t>(object.y);
        slot.dst_width = slot.dst_x < app.canvas_width
                             ? std::min(object.width, app.canvas_width - slot.dst_x)
                             : 0;
        slot.dst_height = slot.dst_y < app.canvas_height
                              ? std::min(object.height, app.canvas_height - slot.dst_y)
                              : 0;
        slot.flip_horizontal = object.flip_horizontal;
        slot.flip_vertical = object.flip_vertical;
        slot.rotate = object.rotate;

        uint32_t src_width = 0;
        uint32_t src_height = 0;
        if (object.type == scene::ObjectType::Camera) {
          slot.source = &app.camera_sources[object.target_camera_index];
          src_width = slot.source->width;
          src_height = slot.source->height;
        } else {
          int width = 0, height = 0, channels = 0;
          auto *pixels = stbi_load(object.image_file.c_str(), &width, &height,
                                   &channels, 4);
          if (pixels == nullptr) {
            std::cerr << "scene: failed to load image \"" << object.image_file
                       << "\": " << stbi_failure_reason() << '\n';
            return 1;
          }
          app.image_sources.emplace_back();
          auto &img = app.image_sources.back();
          img.width = static_cast<uint32_t>(width);
          img.height = static_cast<uint32_t>(height);
          img.frame.assign(pixels, pixels + static_cast<size_t>(width) * height *
                                               kBytesPerPixel);
          stbi_image_free(pixels);
          img.has_frame = true;
          for (size_t px = 3; px < img.frame.size(); px += kBytesPerPixel) {
            if (img.frame[px] != 255) {
              img.has_alpha = true;
              break;
            }
          }
          slot.source = &img;
          src_width = img.width;
          src_height = img.height;
        }

        // build_sample_maps maps dst -> src via x/scale, so scale here is
        // dst-per-src (stretch-to-fit the destination box, independent x/y
        // factors - scene objects aren't required to preserve source aspect
        // ratio). Rotation swaps which source axis dst width/height map onto.
        const bool rotated90 = slot.rotate == 90 || slot.rotate == 270;
        const uint32_t rotated_src_width = rotated90 ? src_height : src_width;
        const uint32_t rotated_src_height = rotated90 ? src_width : src_height;
        slot.scale_x = slot.dst_width / static_cast<double>(rotated_src_width);
        slot.scale_y = slot.dst_height / static_cast<double>(rotated_src_height);
        build_sample_maps(slot, src_width, src_height);
      }

      scene.paint_order.resize(scene.render_slots.size());
      for (size_t i = 0; i < scene.paint_order.size(); ++i)
        scene.paint_order[i] = i;
      std::stable_sort(scene.paint_order.begin(), scene.paint_order.end(),
                       [&](size_t a, size_t b) {
                         return cfg.objects[a].z < cfg.objects[b].z;
                       });
    }
  } else {
    app.canvas_width = args.canvas_width;
    app.canvas_height = args.canvas_height;

    node_names.reserve(kCliInputCount);
    target_objects.reserve(kCliInputCount);
    app.camera_sources.resize(kCliInputCount);

    app.scenes.emplace_back();
    auto &scene = app.scenes.back();
    for (size_t idx = 0; idx < kCliInputCount; ++idx) {
      auto &src = app.camera_sources[idx];
      // CLI mode's own default (960x720) when not explicitly given.
      src.width = args.in_width[idx] != 0 ? args.in_width[idx] : 960;
      src.height = args.in_height[idx] != 0 ? args.in_height[idx] : 720;
      if (src.width == 0 || src.height == 0 || args.in_scale[idx] <= 0.0) {
        std::cerr << "invalid configuration for input " << idx << '\n';
        return 1;
      }
      src.frame.assign(
          static_cast<size_t>(src.width) * src.height * kBytesPerPixel, 0);

      scene.render_slots.emplace_back();
      auto &slot = scene.render_slots.back();
      slot.source = &src;
      slot.scale_x = args.in_scale[idx];
      slot.scale_y = args.in_scale[idx];
      slot.dst_x = idx == 0 ? 0 : app.canvas_width / 2;
      slot.dst_y = 0;
      slot.dst_width = std::min<uint32_t>(
          static_cast<uint32_t>(src.width * slot.scale_x), app.canvas_width / 2);
      slot.dst_height = std::min<uint32_t>(
          static_cast<uint32_t>(src.height * slot.scale_y), app.canvas_height);
      build_sample_maps(slot, src.width, src.height);

      node_names.push_back(node_name(args, "in" + std::to_string(idx)));
      target_objects.push_back(args.in_target[idx]);
    }
    scene.paint_order.resize(kCliInputCount);
    for (size_t i = 0; i < kCliInputCount; ++i)
      scene.paint_order[i] = i;
  }

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr) {
    std::cerr << "pw_main_loop_new failed\n";
    return 1;
  }
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  // node_names.size() here: it's kCliInputCount (2) in non-scene CLI mode
  // but input_count (from --inputs) in scene mode - camera_sources is
  // always sized to match, but only the entries this mode actually
  // populated node_names for get a stream connected.
  for (size_t idx = 0; idx < node_names.size(); ++idx) {
    if (node_names[idx].empty())
      continue;
    auto &src = app.camera_sources[idx];
    src.stream = connect_video_stream(loop, node_names[idx].c_str(),
                                      "Stream/Input/Video", PW_DIRECTION_INPUT,
                                      &input_stream_events, &src, src.width,
                                      src.height, target_objects[idx]);
    if (src.stream == nullptr)
      return 1;
  }

  const std::string out_node_name = node_name(args, "out");
  app.out_stream = connect_video_stream(
      loop, out_node_name.c_str(), "Stream/Output/Video",
      PW_DIRECTION_OUTPUT, &output_stream_events, &app, app.canvas_width,
      app.canvas_height);
  if (app.out_stream == nullptr)
    return 1;

  // Also publish right away, not just reactively once a consumer links and
  // negotiates a format (on_output_param_changed's SPA_PARAM_Format branch)
  // - a control UI needs to read the scene list before any video consumer
  // exists, not only after one happens to connect first.
  if (app.props_enabled)
    publish_scene_params(app);

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  std::cout << "pw-video-compositor running (canvas " << app.canvas_width << "x"
            << app.canvas_height << ", " << app.scenes.size() << " scene(s))\n"
            << std::flush;
  pw_main_loop_run(app.main_loop);

  for (auto &src : app.camera_sources)
    if (src.stream != nullptr)
      pw_stream_destroy(src.stream);
  pw_stream_destroy(app.out_stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
