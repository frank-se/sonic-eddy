#include "SilenceProducer.h"

#include <cstring>
#include <format>
#include <spa/param/audio/format-utils.h>

static void on_process(void *user_data) {
    static_cast<silence::SilenceProducer *>(user_data)->process();
}

static const pw_stream_events stream_events = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_process,
};

void silence::SilenceProducer::setup() {
    const auto name   = std::format("silence-{}", _target_serial);
    const auto target = std::to_string(_target_serial);

    auto *props = pw_properties_new(
        PW_KEY_MEDIA_TYPE,     "Audio",
        PW_KEY_MEDIA_CATEGORY, "Playback",
        PW_KEY_MEDIA_ROLE,     "DSP",
        PW_KEY_NODE_NAME,      name.c_str(),
        PW_KEY_TARGET_OBJECT,  target.c_str(),
        "node.passive",        "false",
        NULL);

    _stream = pw_stream_new_simple(_loop, name.c_str(), props,
                                   &stream_events, this);

    uint8_t buffer[1024];
    auto b          = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));
    auto audio_info = SPA_AUDIO_INFO_RAW_INIT(.format = SPA_AUDIO_FORMAT_F32);
    const struct spa_pod *params[1];
    params[0] = spa_format_audio_raw_build(&b, SPA_PARAM_EnumFormat, &audio_info);

    pw_stream_connect(_stream, PW_DIRECTION_OUTPUT, PW_ID_ANY,
                      static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT |
                                                   PW_STREAM_FLAG_MAP_BUFFERS |
                                                   PW_STREAM_FLAG_RT_PROCESS),
                      params, 1);
}

void silence::SilenceProducer::process() {
    auto *pw_buf = pw_stream_dequeue_buffer(_stream);
    if (!pw_buf)
        return;

    auto *b   = pw_buf->buffer;
    auto *dst = static_cast<float *>(b->datas[0].data);
    if (dst) {
        const auto n_bytes = b->datas[0].maxsize;
        std::memset(dst, 0, n_bytes);
        b->datas[0].chunk->offset = 0;
        b->datas[0].chunk->size   = n_bytes;
        b->datas[0].chunk->stride = sizeof(float);
    }

    pw_stream_queue_buffer(_stream, pw_buf);
}
