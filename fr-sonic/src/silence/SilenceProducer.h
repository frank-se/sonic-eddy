#pragma once

#include <cstdint>
#include <pipewire/pipewire.h>

namespace silence {

class SilenceProducer {
public:
    explicit SilenceProducer(uint64_t target_serial, pw_loop *loop)
        : _target_serial(target_serial), _loop(loop) {}

    ~SilenceProducer() {
        if (_stream) {
            pw_stream_destroy(_stream);
            _stream = nullptr;
        }
    }

    void setup();
    void process();

    [[nodiscard]] pw_stream *stream() const { return _stream; }

private:
    uint64_t _target_serial;
    pw_loop *_loop;
    pw_stream *_stream = nullptr;
};

} // namespace silence
