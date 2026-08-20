#include <algorithm>
#include <array>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <iostream>
#include <mutex>
#include <string>
#include <vector>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/buffers.h>
#include <spa/param/video/format-utils.h>

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

#include "scene.hpp"

namespace {

constexpr uint32_t kBytesPerPixel = 4; // SPA_VIDEO_FORMAT_RGBA
constexpr size_t kInputCount = 2;

// Cameras don't declare their native resolution in the scene file - PipeWire
// needs an exact width/height to negotiate a stream (a fixed SPA_RECTANGLE,
// not a range), so scene mode assumes every camera source has already been
// standardized on this (see cameras/stream-*.fish) rather than renegotiating
// per camera.
constexpr uint32_t kSceneCameraSourceWidth = 1920;
constexpr uint32_t kSceneCameraSourceHeight = 1080;

// One video source's fixed geometry, precomputed sample map, and the
// scratch buffer holding its last received frame. Everything here is
// sized and allocated once, up front, so process() never allocates.
// Camera-backed slots fill `frame` from a PipeWire stream (`stream` is
// non-null); image-backed slots (scene mode only) decode `frame` once at
// startup and leave `stream` null and `has_frame` permanently true.
struct InputSlot {
  uint32_t src_width = 0;
  uint32_t src_height = 0;
  double scale_x = 1.0;
  double scale_y = 1.0;

  bool flip_horizontal = false;
  bool flip_vertical = false;
  uint32_t rotate = 0; // 0, 90, 180 or 270
  bool transposed = false; // true for rotate 90/270 - set by build_sample_maps

  uint32_t dst_x = 0;
  uint32_t dst_y = 0;
  uint32_t dst_width = 0;
  uint32_t dst_height = 0;

  // Non-transposed: col_map (size dst_width) -> source column, row_map
  // (size dst_height) -> source row. Transposed (90/270 rotation): the
  // roles swap - col_map (indexed by dst x) holds the source ROW and
  // row_map (indexed by dst y) holds the source COLUMN, since a 90/270
  // rotation can't be expressed as two independent per-axis maps.
  std::vector<uint32_t> col_map;
  std::vector<uint32_t> row_map;

  std::vector<uint8_t> frame; // last received/decoded frame, RGBA
  std::mutex frame_mutex;
  bool has_frame = false;

  pw_stream *stream = nullptr;
};

struct App {
  pw_main_loop *main_loop = nullptr;
  uint32_t canvas_width = 1280;
  uint32_t canvas_height = 720;
  // deque, not vector: InputSlot holds a std::mutex (non-movable), and
  // vector's growth path requires MoveInsertable to even compile, even
  // when reserve() means no reallocation ever actually happens at
  // runtime. deque never relocates existing elements on growth, so
  // emplace_back works without that requirement, and operator[] stays
  // O(1) for the index-based access used throughout.
  std::deque<InputSlot> inputs;
  std::vector<size_t> paint_order; // indices into inputs, back-to-front
  pw_stream *out_stream = nullptr;
};

void build_sample_maps(InputSlot &in) {
  in.transposed = in.rotate == 90 || in.rotate == 270;
  const uint32_t rotated_width = in.transposed ? in.src_height : in.src_width;
  const uint32_t rotated_height = in.transposed ? in.src_width : in.src_height;

  // dst -> position within the rotated-but-unflipped frame.
  std::vector<uint32_t> rx(in.dst_width);
  for (uint32_t x = 0; x < in.dst_width; ++x) {
    uint32_t v = std::min<uint32_t>(static_cast<uint32_t>(x / in.scale_x),
                                    rotated_width - 1);
    rx[x] = in.flip_horizontal ? rotated_width - 1 - v : v;
  }
  std::vector<uint32_t> ry(in.dst_height);
  for (uint32_t y = 0; y < in.dst_height; ++y) {
    uint32_t v = std::min<uint32_t>(static_cast<uint32_t>(y / in.scale_y),
                                    rotated_height - 1);
    ry[y] = in.flip_vertical ? rotated_height - 1 - v : v;
  }

  in.col_map.resize(in.dst_width);
  in.row_map.resize(in.dst_height);

  if (!in.transposed) {
    // rotate 0: source = (rx, ry); rotate 180 = flip both axes.
    for (uint32_t x = 0; x < in.dst_width; ++x)
      in.col_map[x] = in.rotate == 180 ? in.src_width - 1 - rx[x] : rx[x];
    for (uint32_t y = 0; y < in.dst_height; ++y)
      in.row_map[y] = in.rotate == 180 ? in.src_height - 1 - ry[y] : ry[y];
  } else {
    // rotate 90 CW:  src_x = ry,             src_y = src_height-1-rx
    // rotate 270 CW: src_x = src_width-1-ry, src_y = rx
    // col_map (indexed by dst x = rx) carries src_y; row_map (indexed by
    // dst y = ry) carries src_x - composite_input swaps their roles.
    for (uint32_t x = 0; x < in.dst_width; ++x)
      in.col_map[x] = in.rotate == 90 ? in.src_height - 1 - rx[x] : rx[x];
    for (uint32_t y = 0; y < in.dst_height; ++y)
      in.row_map[y] = in.rotate == 90 ? ry[y] : in.src_width - 1 - ry[y];
  }
}

void on_input_process(void *data) {
  auto &in = *static_cast<InputSlot *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(in.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr ||
      buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(in.stream, pw_buffer);
    return;
  }

  auto &spa_data = buffer->datas[0];
  const size_t expected =
      static_cast<size_t>(in.src_width) * in.src_height * kBytesPerPixel;
  // chunk->size alone isn't trustworthy - maxsize is the actual mapped
  // buffer size and can be smaller (e.g. transitional negotiation buffers).
  const size_t copy_size = std::min<size_t>(
      {expected, spa_data.chunk->size, static_cast<size_t>(spa_data.maxsize)});
  if (copy_size > 0) {
    std::lock_guard<std::mutex> lock(in.frame_mutex);
    std::memcpy(in.frame.data(), spa_data.data, copy_size);
    in.has_frame = true;
  }

  pw_stream_queue_buffer(in.stream, pw_buffer);
}

void composite_input(InputSlot &in, uint8_t *dst, uint32_t dst_stride) {
  std::lock_guard<std::mutex> lock(in.frame_mutex);
  if (!in.has_frame)
    return;

  const uint32_t src_stride = in.src_width * kBytesPerPixel;
  for (uint32_t y = 0; y < in.dst_height; ++y) {
    uint8_t *dst_row = dst + static_cast<size_t>(in.dst_y + y) * dst_stride +
                       static_cast<size_t>(in.dst_x) * kBytesPerPixel;
    if (!in.transposed) {
      const uint8_t *src_row =
          in.frame.data() + static_cast<size_t>(in.row_map[y]) * src_stride;
      for (uint32_t x = 0; x < in.dst_width; ++x) {
        const uint8_t *src_px =
            src_row + static_cast<size_t>(in.col_map[x]) * kBytesPerPixel;
        std::memcpy(dst_row + static_cast<size_t>(x) * kBytesPerPixel, src_px,
                    kBytesPerPixel);
      }
    } else {
      const uint32_t src_col = in.row_map[y];
      for (uint32_t x = 0; x < in.dst_width; ++x) {
        const uint32_t src_row = in.col_map[x];
        const uint8_t *src_px = in.frame.data() +
            static_cast<size_t>(src_row) * src_stride +
            static_cast<size_t>(src_col) * kBytesPerPixel;
        std::memcpy(dst_row + static_cast<size_t>(x) * kBytesPerPixel, src_px,
                    kBytesPerPixel);
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

  for (auto idx : app.paint_order)
    composite_input(app.inputs[idx], dst, stride);

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
  if (param == nullptr || id != SPA_PARAM_Format)
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
  std::string scene_path; // if non-empty, scene mode - all other fields below are ignored

  uint32_t canvas_width = 1280;
  uint32_t canvas_height = 720;
  std::array<uint32_t, kInputCount> in_width{960, 960};
  std::array<uint32_t, kInputCount> in_height{720, 720};
  std::array<double, kInputCount> in_scale{0.5, 0.5};
  std::array<std::string, kInputCount> in_target{};
};

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
    if (arg == "--scene")
      args.scene_path = next(i);
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
  std::cerr << "usage: pw-video-compositor --scene <scene.json>\n"
               "   or: pw-video-compositor "
               "--canvas-width W --canvas-height H "
               "--in0-width W --in0-height H --in0-scale S [--in0-target NAME] "
               "--in1-width W --in1-height H --in1-scale S [--in1-target NAME]\n";
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
  // open for app.inputs[idx] - empty node name means no stream (an
  // image-backed slot in scene mode). Populated by whichever build path
  // below runs, then consumed uniformly after pw_init.
  std::vector<std::string> node_names;
  std::vector<std::string> target_objects;

  // Build phase: layout, sample maps, and scratch buffers - all up front,
  // before any PipeWire stream exists. Nothing below this block allocates.
  if (!args.scene_path.empty()) {
    auto loaded = scene::load_scene(args.scene_path);
    if (!loaded)
      return 1;
    const auto &scene_cfg = *loaded;

    app.canvas_width = scene_cfg.canvas_width;
    app.canvas_height = scene_cfg.canvas_height;

    std::array<bool, kInputCount> camera_index_used{};
    for (const auto &object : scene_cfg.objects) {
      if (object.type != scene::ObjectType::Camera)
        continue;
      if (object.target_camera_index >= kInputCount) {
        std::cerr << "scene: target_camera_index "
                   << object.target_camera_index << " is out of range (0.."
                   << (kInputCount - 1) << ")\n";
        return 1;
      }
      if (camera_index_used[object.target_camera_index]) {
        std::cerr << "scene: target_camera_index "
                   << object.target_camera_index
                   << " is used by more than one object\n";
        return 1;
      }
      camera_index_used[object.target_camera_index] = true;
    }

    node_names.reserve(scene_cfg.objects.size());
    target_objects.reserve(scene_cfg.objects.size());

    for (const auto &object : scene_cfg.objects) {
      app.inputs.emplace_back();
      auto &in = app.inputs.back();

      in.dst_x = object.x < 0 ? 0 : static_cast<uint32_t>(object.x);
      in.dst_y = object.y < 0 ? 0 : static_cast<uint32_t>(object.y);
      in.dst_width = in.dst_x < app.canvas_width
                         ? std::min(object.width, app.canvas_width - in.dst_x)
                         : 0;
      in.dst_height = in.dst_y < app.canvas_height
                          ? std::min(object.height, app.canvas_height - in.dst_y)
                          : 0;
      in.flip_horizontal = object.flip_horizontal;
      in.flip_vertical = object.flip_vertical;
      in.rotate = object.rotate;

      if (object.type == scene::ObjectType::Camera) {
        in.src_width = kSceneCameraSourceWidth;
        in.src_height = kSceneCameraSourceHeight;
        in.frame.assign(
            static_cast<size_t>(in.src_width) * in.src_height * kBytesPerPixel, 0);
        node_names.push_back("se.video-compositor.in" +
                             std::to_string(object.target_camera_index));
        target_objects.emplace_back();
      } else {
        int width = 0, height = 0, channels = 0;
        auto *pixels = stbi_load(object.image_file.c_str(), &width, &height,
                                 &channels, 4);
        if (pixels == nullptr) {
          std::cerr << "scene: failed to load image \"" << object.image_file
                     << "\": " << stbi_failure_reason() << '\n';
          return 1;
        }
        in.src_width = static_cast<uint32_t>(width);
        in.src_height = static_cast<uint32_t>(height);
        in.frame.assign(pixels, pixels + static_cast<size_t>(width) * height *
                                             kBytesPerPixel);
        stbi_image_free(pixels);
        in.has_frame = true;
        node_names.emplace_back();
        target_objects.emplace_back();
      }

      // build_sample_maps maps dst -> src via x/scale, so scale here is
      // dst-per-src (stretch-to-fit the destination box, independent x/y
      // factors - scene objects aren't required to preserve source aspect
      // ratio). Rotation swaps which source axis dst width/height map onto.
      const bool rotated90 = in.rotate == 90 || in.rotate == 270;
      const uint32_t rotated_src_width = rotated90 ? in.src_height : in.src_width;
      const uint32_t rotated_src_height = rotated90 ? in.src_width : in.src_height;
      in.scale_x = in.dst_width / static_cast<double>(rotated_src_width);
      in.scale_y = in.dst_height / static_cast<double>(rotated_src_height);
      build_sample_maps(in);
    }

    app.paint_order.resize(app.inputs.size());
    for (size_t i = 0; i < app.paint_order.size(); ++i)
      app.paint_order[i] = i;
    std::stable_sort(app.paint_order.begin(), app.paint_order.end(),
                     [&](size_t a, size_t b) {
                       return scene_cfg.objects[a].z < scene_cfg.objects[b].z;
                     });
  } else {
    app.canvas_width = args.canvas_width;
    app.canvas_height = args.canvas_height;

    node_names.reserve(kInputCount);
    target_objects.reserve(kInputCount);

    for (size_t idx = 0; idx < kInputCount; ++idx) {
      app.inputs.emplace_back();
      auto &in = app.inputs.back();
      in.src_width = args.in_width[idx];
      in.src_height = args.in_height[idx];
      in.scale_x = args.in_scale[idx];
      in.scale_y = args.in_scale[idx];
      if (in.src_width == 0 || in.src_height == 0 || in.scale_x <= 0.0) {
        std::cerr << "invalid configuration for input " << idx << '\n';
        return 1;
      }

      in.dst_x = idx == 0 ? 0 : app.canvas_width / 2;
      in.dst_y = 0;
      in.dst_width = std::min<uint32_t>(
          static_cast<uint32_t>(in.src_width * in.scale_x), app.canvas_width / 2);
      in.dst_height = std::min<uint32_t>(
          static_cast<uint32_t>(in.src_height * in.scale_y), app.canvas_height);
      build_sample_maps(in);

      in.frame.assign(static_cast<size_t>(in.src_width) * in.src_height * kBytesPerPixel, 0);

      node_names.push_back("se.video-compositor.in" + std::to_string(idx));
      target_objects.push_back(args.in_target[idx]);
    }

    app.paint_order.resize(app.inputs.size());
    for (size_t i = 0; i < app.paint_order.size(); ++i)
      app.paint_order[i] = i;
  }

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr) {
    std::cerr << "pw_main_loop_new failed\n";
    return 1;
  }
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  for (size_t idx = 0; idx < app.inputs.size(); ++idx) {
    if (node_names[idx].empty())
      continue; // image-backed slot, no PipeWire stream needed
    auto &in = app.inputs[idx];
    in.stream = connect_video_stream(loop, node_names[idx].c_str(),
                                     "Stream/Input/Video", PW_DIRECTION_INPUT,
                                     &input_stream_events, &in, in.src_width,
                                     in.src_height, target_objects[idx]);
    if (in.stream == nullptr)
      return 1;
  }

  app.out_stream = connect_video_stream(
      loop, "se.video-compositor.out", "Stream/Output/Video",
      PW_DIRECTION_OUTPUT, &output_stream_events, &app, app.canvas_width,
      app.canvas_height);
  if (app.out_stream == nullptr)
    return 1;

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  std::cout << "pw-video-compositor running (canvas " << app.canvas_width << "x"
            << app.canvas_height << ")\n"
            << std::flush;
  pw_main_loop_run(app.main_loop);

  for (auto &in : app.inputs)
    if (in.stream != nullptr)
      pw_stream_destroy(in.stream);
  pw_stream_destroy(app.out_stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
