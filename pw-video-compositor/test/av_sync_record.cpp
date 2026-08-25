// Prototype: proves two things pw-video-compositor's own out_stream can't
// prove on its own -
//   1) the compositor's output has no driver, so nothing ticks it past the
//      first preroll frame unless something explicitly pulls it. Here the
//      audio input stream is the real (already hardware-driven) clock, and
//      on every audio tick we pw_stream_trigger_process() the video input
//      stream, which we mark PW_STREAM_FLAG_DRIVER for its otherwise
//      driver-less 2-node component (compositor out -> this process' in).
//   2) that raw buffers pulled this way can be handed straight into an
//      in-process GStreamer pipeline via appsrc and come out as a valid,
//      synced-A/V MP4 - no extra PipeWire hop, no separate gst-launch.
//
// Audio content is real (captured from --target-object, e.g. "Master Out")
// and written into the file alongside video - not just used as a tick
// source - since recording it is what actually proves synced A/V, not just
// video-pull mechanics alone.
#include <array>
#include <atomic>
#include <chrono>
#include <cstdio>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <string>
#include <thread>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/audio/format-utils.h>
#include <spa/param/video/format-utils.h>

#include <gst/app/gstappsrc.h>
#include <gst/gst.h>

#include <nlohmann/json.hpp>

namespace {

constexpr const char *kVideoStreamName = "se.av_sync_record.video";

// pw-link's own CLI only resolves node names (or the ephemeral registry
// id) - not object.serial, even though PW_KEY_TARGET_OBJECT itself accepts
// either. So a serial-based --video-target has to be resolved to a name
// once, up front, before the actual manual link (see link_video_node()
// below - WirePlumber's autoconnect policy is audio-tuned and was found
// unreliable for video in earlier testing in this project, so linking is
// done explicitly rather than relying on it).
std::string resolve_node_name(const std::string &target) {
  if (target.empty() ||
      target.find_first_not_of("0123456789") != std::string::npos)
    return target; // not purely numeric - already a name

  FILE *pipe = popen("pw-dump", "r");
  if (pipe == nullptr)
    return target;
  std::string output;
  std::array<char, 4096> buf{};
  size_t n = 0;
  while ((n = std::fread(buf.data(), 1, buf.size(), pipe)) > 0)
    output.append(buf.data(), n);
  pclose(pipe);

  auto dump = nlohmann::json::parse(output, nullptr, false);
  if (dump.is_discarded() || !dump.is_array())
    return target;
  const int64_t wanted_serial = std::strtoll(target.c_str(), nullptr, 10);
  for (const auto &obj : dump) {
    if (obj.value("type", "") != "PipeWire:Interface:Node")
      continue;
    const auto &props = obj["info"]["props"];
    // object.serial comes back as a JSON number from pw-dump (unlike
    // pw-cli's text output, which quotes everything as strings).
    if (props.value("object.serial", int64_t{-1}) == wanted_serial)
      return props.value("node.name", target);
  }
  return target;
}

struct Args {
  std::string video_target = "se.video-compositor.out";
  std::string target_object; // audio tick source - required, never autoconnect-to-default
  uint32_t width = 1280;
  uint32_t height = 720;
  uint32_t rate = 48000;
  uint32_t channels = 2;
  std::string out_path;
  double seconds = 0.0; // 0 = run until Ctrl+C / SIGTERM, no duration timer scheduled
  uint32_t fps = 30; // target *output* video rate - independent of the audio tick rate
  bool preview = false; // opens a window - see video_branch construction in main()
};

struct App {
  Args args;
  pw_main_loop *main_loop = nullptr;
  pw_stream *audio_stream = nullptr;
  pw_stream *video_stream = nullptr;

  GstElement *pipeline = nullptr;
  GstElement *video_appsrc = nullptr;
  GstElement *audio_appsrc = nullptr;

  std::atomic<bool> stopping{false};
  std::chrono::steady_clock::time_point epoch{};
  std::atomic<bool> epoch_set{false};

  // CFR grid state. next_frame_index is touched only from the audio
  // process callback (single PipeWire RT thread), so plain, not atomic.
  uint64_t next_frame_index = 0;

  // pw_stream_trigger_process() is documented as NOT guaranteed to
  // complete synchronously ("possible for the graph iteration to not
  // finish... trigger_process() needs to be called again") - so a
  // single pending-pts scalar could get clobbered if a second trigger
  // fires before the first one's process() callback lands. A small
  // fixed-size FIFO (pushed in trigger order, popped in process-callback
  // order) is RT-safe (no allocation) and correct either way.
  static constexpr size_t kPendingPtsCapacity = 16;
  std::array<GstClockTime, kPendingPtsCapacity> pending_pts{};
  size_t pending_pts_head = 0; // next slot to pop (video process callback)
  size_t pending_pts_tail = 0; // next slot to push (audio process callback)

  std::thread bus_thread;
};

void push_pending_pts(App &app, GstClockTime pts) {
  app.pending_pts[app.pending_pts_tail % App::kPendingPtsCapacity] = pts;
  ++app.pending_pts_tail;
}

GstClockTime pop_pending_pts(App &app) {
  const GstClockTime pts =
      app.pending_pts[app.pending_pts_head % App::kPendingPtsCapacity];
  ++app.pending_pts_head;
  return pts;
}

GstClockTime pts_since_epoch(App &app) {
  const auto now = std::chrono::steady_clock::now();
  bool expected = false;
  if (app.epoch_set.compare_exchange_strong(expected, true))
    app.epoch = now;
  const auto delta = now - app.epoch;
  return static_cast<GstClockTime>(
      std::chrono::duration_cast<std::chrono::nanoseconds>(delta).count());
}

void on_audio_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.audio_stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (app.stopping.load(std::memory_order_relaxed)) {
    pw_stream_queue_buffer(app.audio_stream, pw_buffer);
    return;
  }

  if (buffer->n_datas > 0 && buffer->datas[0].data != nullptr &&
      buffer->datas[0].chunk != nullptr && buffer->datas[0].chunk->size > 0) {
    const auto &spa_data = buffer->datas[0];
    const GstClockTime pts = pts_since_epoch(app);

    auto *gst_buffer = gst_buffer_new_allocate(nullptr, spa_data.chunk->size, nullptr);
    GstMapInfo map;
    gst_buffer_map(gst_buffer, &map, GST_MAP_WRITE);
    std::memcpy(map.data,
                static_cast<uint8_t *>(spa_data.data) + spa_data.chunk->offset,
                spa_data.chunk->size);
    gst_buffer_unmap(gst_buffer, &map);
    GST_BUFFER_PTS(gst_buffer) = pts;

    gst_app_src_push_buffer(GST_APP_SRC(app.audio_appsrc), gst_buffer);
  }

  pw_stream_queue_buffer(app.audio_stream, pw_buffer);

  if (app.stopping.load(std::memory_order_relaxed))
    return;

  // CFR grid: pull a new video frame for every ideal deadline
  // (frame_index * frame_duration) that's now in the past - usually one,
  // but loop in case the target fps ever exceeds the audio tick rate (e.g.
  // fps=60 against a 1024-sample/48kHz ~47Hz tick), so we catch up by
  // pulling more than once per tick instead of silently under-producing.
  // Recomputing the deadline via (index * GST_SECOND) / fps each time -
  // rather than accumulating a pre-rounded per-frame duration - means no
  // per-frame rounding error can accumulate into long-term drift.
  const GstClockTime now = pts_since_epoch(app);
  while (!app.stopping.load(std::memory_order_relaxed)) {
    const GstClockTime deadline = static_cast<GstClockTime>(
        (static_cast<uint64_t>(app.next_frame_index) * GST_SECOND) / app.args.fps);
    if (now < deadline)
      break;
    // Stamp with the ideal grid time, not `now` - actual sampling may
    // land a few ms after the deadline (bounded by the audio tick
    // period), but the label stays exactly on-grid either way.
    push_pending_pts(app, deadline);
    pw_stream_trigger_process(app.video_stream);
    ++app.next_frame_index;
  }
}

void on_video_process(void *data) {
  auto &app = *static_cast<App *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(app.video_stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  // Pop unconditionally even when the buffer's empty/we're stopping - it
  // was pushed 1:1 with the trigger_process() call that produced this
  // process() invocation, so the FIFO must stay in lockstep regardless.
  const GstClockTime pts = pop_pending_pts(app);
  if (buffer->n_datas > 0 && buffer->datas[0].data != nullptr &&
      buffer->datas[0].chunk != nullptr && buffer->datas[0].chunk->size > 0 &&
      !app.stopping.load(std::memory_order_relaxed)) {
    const auto &spa_data = buffer->datas[0];

    auto *gst_buffer = gst_buffer_new_allocate(nullptr, spa_data.chunk->size, nullptr);
    GstMapInfo map;
    gst_buffer_map(gst_buffer, &map, GST_MAP_WRITE);
    std::memcpy(map.data,
                static_cast<uint8_t *>(spa_data.data) + spa_data.chunk->offset,
                spa_data.chunk->size);
    gst_buffer_unmap(gst_buffer, &map);
    GST_BUFFER_PTS(gst_buffer) = pts;

    gst_app_src_push_buffer(GST_APP_SRC(app.video_appsrc), gst_buffer);
  }

  pw_stream_queue_buffer(app.video_stream, pw_buffer);
}

const pw_stream_events audio_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_audio_process,
};

const pw_stream_events video_stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_video_process,
};

// mp4mux only writes a valid moov atom at EOS - hard-killing the pipeline
// instead would leave an unplayable file, so shutdown must push EOS through
// both appsrcs and wait for it to actually reach the bus before quitting.
void begin_shutdown(App &app) {
  bool expected = false;
  if (!app.stopping.compare_exchange_strong(expected, true))
    return;
  std::cout << "shutting down, flushing encoder...\n" << std::flush;
  gst_app_src_end_of_stream(GST_APP_SRC(app.audio_appsrc));
  gst_app_src_end_of_stream(GST_APP_SRC(app.video_appsrc));
}

void on_quit_signal(void *data, int) { begin_shutdown(*static_cast<App *>(data)); }

void on_duration_timer(void *data, uint64_t) { begin_shutdown(*static_cast<App *>(data)); }

// Fired once, shortly after startup, to give our video stream's node time
// to register before we try to link it - see the comment above the video
// pw_stream_connect() call for why this is a manual pw-link rather than
// PW_STREAM_FLAG_AUTOCONNECT.
void on_link_timer(void *data, uint64_t) {
  auto &app = *static_cast<App *>(data);
  const std::string source_name = resolve_node_name(app.args.video_target);
  const std::string command = "pw-link \"" + source_name + ":output_1\" \"" +
                              std::string(kVideoStreamName) + ":input_1\"";
  const int result = std::system(command.c_str());
  if (result != 0)
    std::cerr << "warning: manual video link failed (source=" << source_name
              << "), command exit status " << result << '\n';
}

void watch_bus(App &app) {
  auto *bus = gst_element_get_bus(app.pipeline);
  while (true) {
    auto *msg = gst_bus_timed_pop_filtered(
        bus, GST_CLOCK_TIME_NONE,
        static_cast<GstMessageType>(GST_MESSAGE_EOS | GST_MESSAGE_ERROR));
    if (msg == nullptr)
      continue;
    if (GST_MESSAGE_TYPE(msg) == GST_MESSAGE_ERROR) {
      GError *err = nullptr;
      gchar *debug = nullptr;
      gst_message_parse_error(msg, &err, &debug);
      std::cerr << "gst error: " << (err != nullptr ? err->message : "?") << '\n';
      if (err != nullptr)
        g_error_free(err);
      g_free(debug);
    }
    gst_message_unref(msg);
    break; // EOS or ERROR both mean "stop"
  }
  gst_object_unref(bus);
  pw_main_loop_quit(app.main_loop);
}

bool parse_args(int argc, char **argv, Args &args) {
  for (int i = 1; i < argc; ++i) {
    const std::string arg = argv[i];
    auto next = [&]() { return std::string(i + 1 < argc ? argv[++i] : ""); };
    if (arg == "--video-target")
      args.video_target = next();
    else if (arg == "--target-object")
      args.target_object = next();
    else if (arg == "--width")
      args.width = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--height")
      args.height = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--rate")
      args.rate = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--channels")
      args.channels = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--out")
      args.out_path = next();
    else if (arg == "--seconds")
      args.seconds = std::strtod(next().c_str(), nullptr);
    else if (arg == "--fps")
      args.fps = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
    else if (arg == "--preview")
      args.preview = true;
    else {
      std::cerr << "unknown argument: " << arg << '\n';
      return false;
    }
  }
  if (args.target_object.empty() || args.out_path.empty() || args.width == 0 ||
      args.height == 0 || args.fps == 0) {
    std::cerr << "usage: av_sync_record --target-object <name-or-serial> "
                 "--out <file.mp4> [--video-target <name>] [--width W] "
                 "[--height H] [--rate R] [--channels C] "
                 "[--seconds N (default: run until Ctrl+C)] "
                 "[--fps F] [--preview]\n";
    return false;
  }
  return true;
}

} // namespace

int main(int argc, char **argv) {
  App app;
  if (!parse_args(argc, argv, app.args))
    return 1;

  gst_init(&argc, &argv);

  // caps values must be quoted - gst_parse_launch's mini-language uses
  // unquoted commas to separate element properties, which collides with the
  // commas inside a caps string; unquoted, the caps property silently ends
  // up wrong/unset and the encoder never gets a fixed format to negotiate.
  // --preview taps the video branch with a tee: one leg to the recorder as
  // before, one leg through a 1-deep leaky queue (drops stale frames, so
  // the sink always shows whatever's newest - no attempt at jitter-free
  // pacing) into a plain window, sync=false so it displays frames as they
  // arrive rather than pacing to the pipeline clock.
  //
  // waylandsink, not autovideosink: autovideosink's auto-selected sink
  // triggers a GStreamer-CRITICAL assertion in gst_value_collect_int_range
  // on this system, confirmed independent of anything in this tool
  // (`gst-launch-1.0 videotestsrc ! autovideosink` alone reproduces it -
  // some sink candidate's display/GL capability probing produces a
  // degenerate caps range). waylandsink (this is a Wayland session -
  // XDG_SESSION_TYPE=wayland) was confirmed clean on its own.
  const std::string video_branch =
      app.args.preview
          ? "! tee name=vtee "
            "vtee. ! queue ! videoconvert ! x264enc tune=zerolatency ! mux. "
            "vtee. ! queue leaky=downstream max-size-buffers=1 "
            "! videoconvert ! waylandsink sync=false "
          : "! videoconvert ! x264enc tune=zerolatency ! mux. ";
  const std::string pipeline_desc =
      "mp4mux name=mux ! filesink name=sink "
      "appsrc name=vsrc is-live=true format=time "
      "caps=\"video/x-raw,format=RGBA,width=" + std::to_string(app.args.width) +
      ",height=" + std::to_string(app.args.height) + ",framerate=" +
      std::to_string(app.args.fps) + "/1\" " + video_branch +
      "appsrc name=asrc is-live=true format=time "
      "caps=\"audio/x-raw,format=F32LE,rate=" + std::to_string(app.args.rate) +
      ",channels=" + std::to_string(app.args.channels) + ",layout=interleaved\" "
      "! audioconvert ! audioresample ! avenc_aac ! mux.";

  GError *parse_error = nullptr;
  app.pipeline = gst_parse_launch(pipeline_desc.c_str(), &parse_error);
  if (app.pipeline == nullptr) {
    std::cerr << "gst_parse_launch failed: "
              << (parse_error != nullptr ? parse_error->message : "?") << '\n';
    return 1;
  }
  auto *sink = gst_bin_get_by_name(GST_BIN(app.pipeline), "sink");
  g_object_set(sink, "location", app.args.out_path.c_str(), nullptr);
  gst_object_unref(sink);
  app.video_appsrc = gst_bin_get_by_name(GST_BIN(app.pipeline), "vsrc");
  app.audio_appsrc = gst_bin_get_by_name(GST_BIN(app.pipeline), "asrc");

  if (gst_element_set_state(app.pipeline, GST_STATE_PLAYING) ==
      GST_STATE_CHANGE_FAILURE) {
    std::cerr << "failed to start gstreamer pipeline\n";
    return 1;
  }
  app.bus_thread = std::thread(watch_bus, std::ref(app));

  pw_init(&argc, &argv);
  app.main_loop = pw_main_loop_new(nullptr);
  if (app.main_loop == nullptr)
    return 1;
  auto *loop = pw_main_loop_get_loop(app.main_loop);

  // Audio: a normal follower. It rides whatever already drives the existing
  // audio graph - no PW_STREAM_FLAG_DRIVER needed here, unlike video below.
  auto *audio_properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "Production", PW_KEY_MEDIA_CLASS, "Stream/Input/Audio",
      PW_KEY_NODE_NAME, "se.av_sync_record.audio", PW_KEY_TARGET_OBJECT,
      app.args.target_object.c_str(), nullptr);
  app.audio_stream = pw_stream_new_simple(loop, "se.av_sync_record.audio",
                                          audio_properties, &audio_stream_events, &app);
  if (app.audio_stream == nullptr)
    return 1;

  std::array<uint8_t, 1024> audio_pod_buffer{};
  auto audio_builder = SPA_POD_BUILDER_INIT(audio_pod_buffer.data(), audio_pod_buffer.size());
  spa_audio_info_raw audio_info{};
  audio_info.format = SPA_AUDIO_FORMAT_F32;
  audio_info.rate = app.args.rate;
  audio_info.channels = app.args.channels;
  const spa_pod *audio_params[] = {
      spa_format_audio_raw_build(&audio_builder, SPA_PARAM_EnumFormat, &audio_info)};

  if (pw_stream_connect(app.audio_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
                        static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                                     PW_STREAM_FLAG_MAP_BUFFERS |
                                                     PW_STREAM_FLAG_RT_PROCESS),
                        audio_params, 1) < 0) {
    std::cerr << "audio pw_stream_connect failed\n";
    return 1;
  }

  // Video: this 2-node component (compositor out -> here) has no driver of
  // its own, so we have to be one - trigger_process() is called from the
  // audio callback above, in lockstep with the real audio clock.
  //
  // Deliberately no PW_KEY_TARGET_OBJECT here (unlike audio above) - it
  // doesn't just get ignored the way plain "no autoconnect flag" would
  // suggest. WirePlumber's own policy reads target.object regardless of
  // PW_STREAM_FLAG_AUTOCONNECT and repeatedly retries linking it on every
  // internal rescan (confirmed via `journalctl --user`:
  // "wp-event-dispatcher: ... failed: tried to link on last rescan, not
  // retrying nil", flooding continuously) - it doesn't resolve our
  // object.serial the way pw-dump does, fails, and races against our own
  // manual pw-link below for the same port, which was breaking it
  // ("failed to link ports: Success"). Since linking is fully manual here
  // on purpose, target.object metadata is unnecessary and was actively
  // harmful - leave it unset.
  auto *video_properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Capture",
      PW_KEY_MEDIA_ROLE, "Video", PW_KEY_MEDIA_CLASS, "Stream/Input/Video",
      PW_KEY_NODE_NAME, kVideoStreamName, nullptr);
  app.video_stream = pw_stream_new_simple(loop, kVideoStreamName,
                                          video_properties, &video_stream_events, &app);
  if (app.video_stream == nullptr)
    return 1;

  std::array<uint8_t, 1024> video_pod_buffer{};
  auto video_builder = SPA_POD_BUILDER_INIT(video_pod_buffer.data(), video_pod_buffer.size());
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(
      .format = SPA_VIDEO_FORMAT_RGBA,
      .size = SPA_RECTANGLE(app.args.width, app.args.height),
      .framerate = SPA_FRACTION(0, 0));
  const spa_pod *video_params[] = {
      spa_format_video_raw_build(&video_builder, SPA_PARAM_EnumFormat, &video_info)};

  // No PW_STREAM_FLAG_AUTOCONNECT here on purpose - WirePlumber's default
  // linking policy is tuned for audio; targeting a video node via
  // target-object + autoconnect was found unreliable in earlier testing in
  // this project (sometimes silently linked to the wrong node instead of
  // failing). We link explicitly with `pw-link` after this node registers
  // (see below), matching the pattern that's actually proven reliable here.
  if (pw_stream_connect(app.video_stream, PW_DIRECTION_INPUT, PW_ID_ANY,
                        static_cast<pw_stream_flags>(PW_STREAM_FLAG_DRIVER |
                                                     PW_STREAM_FLAG_MAP_BUFFERS |
                                                     PW_STREAM_FLAG_RT_PROCESS),
                        video_params, 1) < 0) {
    std::cerr << "video pw_stream_connect failed\n";
    return 1;
  }

  pw_loop_add_signal(loop, SIGINT, on_quit_signal, &app);
  pw_loop_add_signal(loop, SIGTERM, on_quit_signal, &app);
  // one-shot: give our video node ~300ms to register before linking it
  auto *link_timer = pw_loop_add_timer(loop, on_link_timer, &app);
  const struct timespec link_ts = {0, 300'000'000};
  pw_loop_update_timer(loop, link_timer, const_cast<struct timespec *>(&link_ts),
                       nullptr, false);
  // --seconds 0 (default) means "no duration timer" - run until Ctrl+C.
  if (app.args.seconds > 0.0) {
    // one-shot: fire once after --seconds, never again
    auto *duration_timer = pw_loop_add_timer(loop, on_duration_timer, &app);
    const struct timespec duration_ts = {
        static_cast<long>(app.args.seconds),
        static_cast<long>((app.args.seconds - static_cast<long>(app.args.seconds)) * 1e9)};
    pw_loop_update_timer(loop, duration_timer, const_cast<struct timespec *>(&duration_ts),
                         nullptr, false);
  }

  std::cout << "av_sync_record running, video-target=" << app.args.video_target
            << " audio-target=" << app.args.target_object
            << " out=" << app.args.out_path << '\n' << std::flush;
  pw_main_loop_run(app.main_loop);

  app.bus_thread.join();

  pw_stream_destroy(app.video_stream);
  pw_stream_destroy(app.audio_stream);
  pw_main_loop_destroy(app.main_loop);
  pw_deinit();

  gst_element_set_state(app.pipeline, GST_STATE_NULL);
  gst_object_unref(app.video_appsrc);
  gst_object_unref(app.audio_appsrc);
  gst_object_unref(app.pipeline);

  std::cout << "wrote " << app.args.out_path << '\n';
  return 0;
}
