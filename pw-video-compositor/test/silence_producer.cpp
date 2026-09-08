// Standalone sibling of fr-sonic/src/silence/SilenceProducer.{h,cpp} - same
// idea (a continuous-zeros PW_DIRECTION_OUTPUT stream keeps a target node
// looking "active" so WirePlumber's idle-suspend never kicks in), but as its
// own process rather than a class embedded in the SonicEddy .NET app, since
// pw-video-compositor's tools (av_sync_record etc.) aren't in that process.
//
// Motivation: av_sync_record's audio_stream is a plain follower on
// --target-object (e.g. "Master Out"). If nothing else happens to be
// playing into that node at a given moment, WirePlumber can suspend it for
// being idle, and once suspended the follower stops getting process()
// calls at all - av_sync_record then looks frozen (no crash, just stuck),
// which is what this tool is meant to prevent: run it targeting the same
// node av_sync_record follows, so that node always has an active link and
// the audio graph always has something ticking it.
#include <cstdint>
#include <cstring>
#include <iostream>
#include <string>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/audio/format-utils.h>

namespace {

struct App {
  pw_main_loop *main_loop = nullptr;
  pw_stream *stream = nullptr;
};

void on_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  auto *dst = static_cast<float *>(buffer->datas[0].data);
  if (dst != nullptr) {
    const uint32_t n_bytes = buffer->datas[0].maxsize;
    std::memset(dst, 0, n_bytes);
    buffer->datas[0].chunk->offset = 0;
    buffer->datas[0].chunk->size = n_bytes;
    buffer->datas[0].chunk->stride = sizeof(float);
  }

  pw_stream_queue_buffer(app.stream, pw_buffer);
}

const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_process,
};

void on_quit_signal(void *data, int) {
  pw_main_loop_quit(static_cast<App *>(data)->main_loop);
}

} // namespace

int main(int argc, char **argv) {
  std::string target_object;
  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    if (arg == "--target-object" && i + 1 < argc)
      target_object = argv[++i];
  }
  if (target_object.empty()) {
    std::cerr << "usage: silence_producer --target-object <name-or-serial>\n";
    return 1;
  }

  pw_init(&argc, &argv);
  App app;
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr)
    return 1;
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  const std::string name = "se.silence_producer." + target_object;
  auto *props = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Playback",
      PW_KEY_MEDIA_ROLE, "DSP", PW_KEY_NODE_NAME, name.c_str(),
      PW_KEY_TARGET_OBJECT, target_object.c_str(), "node.passive", "false",
      // Every PW_KEY_TARGET_OBJECT stream in this codebase must pair it with
      // these two - see video_blender.cpp's connect_video_stream comment.
      "node.dont-fallback", "true", "node.linger", "true", nullptr);
  app.stream = pw_stream_new_simple(loop, name.c_str(), props, &stream_events, &app);
  if (app.stream == nullptr)
    return 1;

  uint8_t pod_buffer[1024];
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer, sizeof(pod_buffer));
  auto audio_info = SPA_AUDIO_INFO_RAW_INIT(.format = SPA_AUDIO_FORMAT_F32);
  const spa_pod *params[] = {
      spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &audio_info)};

  if (pw_stream_connect(app.stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
                        static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                                     PW_STREAM_FLAG_MAP_BUFFERS |
                                                     PW_STREAM_FLAG_RT_PROCESS),
                        params, 1) < 0) {
    std::cerr << "pw_stream_connect failed\n";
    return 1;
  }

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);

  std::cout << "silence_producer running, target-object=" << target_object << '\n'
            << std::flush;
  pw_main_loop_run(app.main_loop);

  pw_stream_destroy(app.stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();
  return 0;
}
