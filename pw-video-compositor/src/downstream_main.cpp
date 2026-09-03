// downstream-compositor: purpose-built compositor for the DSK/"downstream
// effects" node. Deliberately NOT pw-video-compositor's generic N-camera
// model, even though most of the machinery below mirrors main.cpp closely -
// kept as a separate binary on purpose (see the plan discussion this came
// from) because conflating "generic N-camera compositor" and "downstream
// effects, always fed by one baseline input" concepts was a real source of
// confusion, more costly than the modest code duplication here.
//
// Shape: exactly one baseline video input (App::base_video_source) -
// always present, never a scene object, never part of the --inputs pool,
// always composited first/full-canvas, fed by whatever's upstream (the A/B
// compositor via video-blender in the real system, but this binary doesn't
// know or care - it just optionally auto-links to a --baseline-target if
// given, otherwise stays a manual pw-link). On top of that: an optional
// --inputs-loaded pool of routable overlay video inputs ("video in can
// include ANYTHING", not just cameras) plus static images, both addressed
// as scene objects exactly like pw-video-compositor's scenes.
#include <algorithm>
#include <array>
#include <atomic>
#include <csignal>
#include <cstdint>
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

#include "downstream_camera_config.hpp"
#include "downstream_scene.hpp"

namespace {

constexpr uint32_t kBytesPerPixel = 4; // SPA_VIDEO_FORMAT_RGBA
constexpr size_t kMaxScenes = 5; // mirrors pw-video-compositor's kMaxScenes

// Mirrors pw-video-compositor/src/main.cpp's FrameSource.
struct FrameSource {
  uint32_t width = 0;
  uint32_t height = 0;
  std::vector<uint8_t> frame;
  std::mutex frame_mutex;
  bool has_frame = false;
  pw_stream *stream = nullptr;
  bool has_alpha = false;
};

// Mirrors pw-video-compositor/src/main.cpp's RenderSlot.
struct RenderSlot {
  FrameSource *source = nullptr;

  double scale_x = 1.0;
  double scale_y = 1.0;
  bool flip_horizontal = false;
  bool flip_vertical = false;
  uint32_t rotate = 0;
  bool transposed = false;

  uint32_t dst_x = 0;
  uint32_t dst_y = 0;
  uint32_t dst_width = 0;
  uint32_t dst_height = 0;

  bool visible = true;
  float red_gain = 1.0f;
  float green_gain = 1.0f;
  float blue_gain = 1.0f;
  float opacity = 1.0f;

  std::vector<uint32_t> col_map;
  std::vector<uint32_t> row_map;

  std::mutex layout_mutex;
};

struct Scene {
  std::string name;
  std::string file;
  std::deque<RenderSlot> render_slots;
  std::vector<size_t> paint_order;
};

struct App {
  pw_main_loop *main_loop = nullptr;
  uint32_t canvas_width = 1280;
  uint32_t canvas_height = 720;

  // The baseline - always present, always sized to canvas_width/height,
  // never part of video_sources/--inputs, never a scene object. See the
  // file header comment.
  FrameSource base_video_source;

  // The routable overlay pool, from --inputs - may be empty (no overlay
  // video inputs is a normal, common case for Downstream today).
  std::deque<FrameSource> video_sources;
  std::deque<FrameSource> image_sources;

  std::deque<Scene> scenes;
  std::atomic<int> active_scene_index{0};

  pw_stream *out_stream = nullptr;
  std::array<uint8_t, 4096> params_buffer{};
};

// Mirrors pw-video-compositor/src/main.cpp's build_sample_maps.
void build_sample_maps(RenderSlot &slot, uint32_t src_width, uint32_t src_height) {
  slot.transposed = slot.rotate == 90 || slot.rotate == 270;
  const uint32_t rotated_width = slot.transposed ? src_height : src_width;
  const uint32_t rotated_height = slot.transposed ? src_width : src_height;

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
    for (uint32_t x = 0; x < slot.dst_width; ++x)
      slot.col_map[x] = slot.rotate == 180 ? src_width - 1 - rx[x] : rx[x];
    for (uint32_t y = 0; y < slot.dst_height; ++y)
      slot.row_map[y] = slot.rotate == 180 ? src_height - 1 - ry[y] : ry[y];
  } else {
    for (uint32_t x = 0; x < slot.dst_width; ++x)
      slot.col_map[x] = slot.rotate == 90 ? src_height - 1 - rx[x] : rx[x];
    for (uint32_t y = 0; y < slot.dst_height; ++y)
      slot.row_map[y] = slot.rotate == 90 ? ry[y] : src_width - 1 - ry[y];
  }
}

// Mirrors pw-video-compositor/src/main.cpp's on_input_process.
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

inline uint8_t blend_channel(uint8_t src, uint8_t dst, float opacity) {
  return static_cast<uint8_t>(std::clamp(
      static_cast<float>(src) * opacity + static_cast<float>(dst) * (1.0f - opacity),
      0.0f, 255.0f));
}

// Mirrors pw-video-compositor/src/main.cpp's composite_input.
void composite_input(RenderSlot &slot, uint8_t *dst, uint32_t dst_stride) {
  std::lock_guard<std::mutex> layout_lock(slot.layout_mutex);
  if (!slot.visible)
    return;

  auto &source = *slot.source;
  std::lock_guard<std::mutex> lock(source.frame_mutex);
  if (!source.has_frame)
    return;

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

// Mirrors pw-video-compositor/src/main.cpp's on_output_process.
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
  if (spa_data.maxsize < needed) {
    pw_stream_queue_buffer(app.out_stream, pw_buffer);
    return;
  }
  auto *dst = static_cast<uint8_t *>(spa_data.data);

  // Baseline first, full-canvas, always the bottom layer - falls back to
  // black if nothing's connected to it yet (has_frame stays false), same
  // as pw-video-compositor's default when nothing has painted yet.
  {
    std::lock_guard<std::mutex> lock(app.base_video_source.frame_mutex);
    if (app.base_video_source.has_frame &&
        app.base_video_source.frame.size() >= needed) {
      std::memcpy(dst, app.base_video_source.frame.data(), needed);
    } else {
      std::memset(dst, 0, needed);
    }
  }

  int idx = app.active_scene_index.load(std::memory_order_relaxed);
  if (idx < 0 || static_cast<size_t>(idx) >= app.scenes.size())
    idx = 0;
  if (!app.scenes.empty()) {
    auto &scene = app.scenes[idx];
    for (auto slot_idx : scene.paint_order)
      composite_input(scene.render_slots[slot_idx], dst, stride);
  }

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

// Mirrors pw-video-compositor/src/main.cpp's publish_scene_params.
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

// Mirrors pw-video-compositor/src/main.cpp's apply_object_params.
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
  if (object_idx < 0 || static_cast<size_t>(object_idx) >= scene.render_slots.size())
    return;
  auto &slot = scene.render_slots[object_idx];

  std::lock_guard<std::mutex> lock(slot.layout_mutex);

  if (command.contains("dst_x") && command["dst_x"].is_number()) {
    const uint32_t requested = command["dst_x"].get<uint32_t>();
    slot.dst_x = app.canvas_width > slot.dst_width
                     ? std::min(requested, app.canvas_width - slot.dst_width)
                     : 0;
  }
  if (command.contains("dst_y") && command["dst_y"].is_number()) {
    const uint32_t requested = command["dst_y"].get<uint32_t>();
    slot.dst_y = app.canvas_height > slot.dst_height
                     ? std::min(requested, app.canvas_height - slot.dst_height)
                     : 0;
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

// Mirrors pw-video-compositor/src/main.cpp's handle_output_props.
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
    publish_scene_params(app);
}

// Mirrors pw-video-compositor/src/main.cpp's on_output_param_changed.
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

  publish_scene_params(app);
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
  std::vector<std::string> scene_paths;
  std::string inputs_path; // optional - empty pool if not given
  std::string baseline_target; // optional PW_KEY_TARGET_OBJECT for se.downstream.base
};

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
      args.scene_paths.push_back(std::filesystem::absolute(next(i)).string());
    } else if (arg == "--inputs") {
      args.inputs_path = next(i);
    } else if (arg == "--baseline-target") {
      args.baseline_target = next(i);
    } else {
      std::cerr << "unknown argument: " << arg << '\n';
      return false;
    }
  }
  return true;
}

void print_usage() {
  std::cerr << "usage: downstream-compositor --scene <scene.json> [--scene <scene2.json> ...] "
               "(up to " << kMaxScenes << ") [--inputs <inputs.json>] "
               "[--baseline-target NAME_OR_ID]\n"
               "--inputs is optional - omit it if the scene(s) have no \"video\"-type "
               "objects (only the always-on baseline input, se.downstream.base).\n"
               "--baseline-target auto-links se.downstream.base to the given node on "
               "startup (e.g. se.video-blender.out); omit to keep it a manual pw-link.\n";
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
      "Sonic Eddy downstream compositor", nullptr);
  if (!target_object.empty())
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT, target_object.c_str());

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
  // together (confirmed empirically - see pw-video-compositor's git log).
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
  Args args;
  if (!parse_args(argc, argv, args)) {
    print_usage();
    return 1;
  }
  if (args.scene_paths.empty()) {
    std::cerr << "at least one --scene is required\n";
    print_usage();
    return 1;
  }

  App app;

  std::vector<downstream_camera_config::InputDef> inputs;
  if (!args.inputs_path.empty()) {
    auto loaded = downstream_camera_config::load(args.inputs_path);
    if (!loaded)
      return 1;
    inputs = std::move(*loaded);
  }
  const size_t input_count = inputs.size();

  std::vector<downstream_scene::SceneConfig> scene_configs;
  scene_configs.reserve(args.scene_paths.size());
  for (const auto &path : args.scene_paths) {
    auto loaded = downstream_scene::load_scene(path);
    if (!loaded)
      return 1;
    scene_configs.push_back(std::move(*loaded));
  }

  app.canvas_width = scene_configs.front().canvas_width;
  app.canvas_height = scene_configs.front().canvas_height;
  for (const auto &cfg : scene_configs) {
    if (cfg.canvas_width != app.canvas_width || cfg.canvas_height != app.canvas_height) {
      std::cerr << "downstream_scene: all --scene files must share the same "
                   "canvas_width/canvas_height (the output format is "
                   "negotiated once at startup)\n";
      return 1;
    }
  }

  for (const auto &cfg : scene_configs) {
    for (const auto &object : cfg.objects) {
      if (object.type != downstream_scene::ObjectType::Video)
        continue;
      if (object.target_input_index >= input_count) {
        std::cerr << "downstream_scene: target_input_index "
                   << object.target_input_index << " is out of range (0.."
                   << (input_count == 0 ? 0 : input_count - 1) << ", pool size "
                   << input_count << ")\n";
        return 1;
      }
    }
  }

  // Baseline - always built, sized to canvas, regardless of scene content.
  app.base_video_source.width = app.canvas_width;
  app.base_video_source.height = app.canvas_height;
  app.base_video_source.frame.assign(
      static_cast<size_t>(app.canvas_width) * app.canvas_height * kBytesPerPixel, 0);

  // Routable overlay pool - shared across every loaded scene, same
  // "always build all of them regardless of which scenes reference each
  // index" reasoning as pw-video-compositor's camera_sources.
  app.video_sources.resize(input_count);
  for (size_t idx = 0; idx < input_count; ++idx) {
    auto &src = app.video_sources[idx];
    src.width = inputs[idx].width;
    src.height = inputs[idx].height;
    src.frame.assign(static_cast<size_t>(src.width) * src.height * kBytesPerPixel, 0);
  }

  for (size_t scene_i = 0; scene_i < scene_configs.size(); ++scene_i) {
    const auto &cfg = scene_configs[scene_i];
    app.scenes.emplace_back();
    auto &scene = app.scenes.back();
    scene.name = cfg.name;
    scene.file = args.scene_paths[scene_i];

    // Baseline is always object 0 of every scene's paint order - implicit,
    // not declared in the scene file at all, always the bottom layer.
    scene.render_slots.emplace_back();
    {
      auto &base_slot = scene.render_slots.back();
      base_slot.source = &app.base_video_source;
      base_slot.dst_x = 0;
      base_slot.dst_y = 0;
      base_slot.dst_width = app.canvas_width;
      base_slot.dst_height = app.canvas_height;
      base_slot.scale_x = 1.0;
      base_slot.scale_y = 1.0;
      build_sample_maps(base_slot, app.canvas_width, app.canvas_height);
    }

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
      if (object.type == downstream_scene::ObjectType::Video) {
        slot.source = &app.video_sources[object.target_input_index];
        src_width = slot.source->width;
        src_height = slot.source->height;
      } else {
        int width = 0, height = 0, channels = 0;
        auto *pixels = stbi_load(object.image_file.c_str(), &width, &height,
                                 &channels, 4);
        if (pixels == nullptr) {
          std::cerr << "downstream_scene: failed to load image \"" << object.image_file
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

      const bool rotated90 = slot.rotate == 90 || slot.rotate == 270;
      const uint32_t rotated_src_width = rotated90 ? src_height : src_width;
      const uint32_t rotated_src_height = rotated90 ? src_width : src_height;
      slot.scale_x = slot.dst_width / static_cast<double>(rotated_src_width);
      slot.scale_y = slot.dst_height / static_cast<double>(rotated_src_height);
      build_sample_maps(slot, src_width, src_height);
    }

    // paint_order indices are offset by 1 (index 0 in render_slots is
    // always the baseline, always painted first/bottom) - z only orders
    // cfg.objects, so the baseline (not in cfg.objects at all) is placed
    // explicitly at the front rather than sorted in.
    scene.paint_order.resize(scene.render_slots.size());
    scene.paint_order[0] = 0;
    for (size_t i = 0; i < cfg.objects.size(); ++i)
      scene.paint_order[i + 1] = i + 1;
    std::stable_sort(scene.paint_order.begin() + 1, scene.paint_order.end(),
                     [&](size_t a, size_t b) {
                       return cfg.objects[a - 1].z < cfg.objects[b - 1].z;
                     });
  }

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr) {
    std::cerr << "pw_main_loop_new failed\n";
    return 1;
  }
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  app.base_video_source.stream = connect_video_stream(
      loop, "se.downstream.base", "Stream/Input/Video", PW_DIRECTION_INPUT,
      &input_stream_events, &app.base_video_source, app.base_video_source.width,
      app.base_video_source.height, args.baseline_target);
  if (app.base_video_source.stream == nullptr)
    return 1;

  for (size_t idx = 0; idx < input_count; ++idx) {
    auto &src = app.video_sources[idx];
    const std::string node_name = "se.downstream.video" + std::to_string(idx);
    src.stream = connect_video_stream(loop, node_name.c_str(), "Stream/Input/Video",
                                      PW_DIRECTION_INPUT, &input_stream_events, &src,
                                      src.width, src.height, inputs[idx].target_object);
    if (src.stream == nullptr)
      return 1;
  }

  app.out_stream = connect_video_stream(loop, "se.downstream.out", "Stream/Output/Video",
                                        PW_DIRECTION_OUTPUT, &output_stream_events, &app,
                                        app.canvas_width, app.canvas_height);
  if (app.out_stream == nullptr)
    return 1;

  publish_scene_params(app);

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  std::cout << "downstream-compositor running (canvas " << app.canvas_width << "x"
            << app.canvas_height << ", " << app.scenes.size() << " scene(s), "
            << input_count << " overlay video input(s))\n" << std::flush;
  pw_main_loop_run(app.main_loop);

  pw_stream_destroy(app.base_video_source.stream);
  for (auto &src : app.video_sources)
    if (src.stream != nullptr)
      pw_stream_destroy(src.stream);
  pw_stream_destroy(app.out_stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
