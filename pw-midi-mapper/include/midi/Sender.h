#pragma once

#include "midi/Messages.h"

#include <pipewire/pipewire.h>

#include <boost/lockfree/spsc_queue.hpp>

#include <string>

namespace midi {

using LockFreeQueue =
    boost::lockfree::spsc_queue<Message, boost::lockfree::capacity<128>>;

class Sender {
public:
  explicit Sender(std::string pmx_purpose, std::string pmx_tag,
                  std::string name, pw_main_loop *loop)
      : _pmx_purpose(std::move(pmx_purpose)), _pmx_tag(std::move(pmx_tag)),
        _name(std::move(name)), _loop(loop) {}

  ~Sender() {
    if (_stream != nullptr) {
      pw_stream_destroy(_stream);
      _stream = nullptr;
    }
  }

  void process();

  void setup();

  void push(Message message);

private:
  std::string _pmx_purpose;
  std::string _pmx_tag;
  std::string _name;
  pw_main_loop *_loop;
  pw_stream *_stream = nullptr;
  LockFreeQueue _queue{};
};

using SenderPointer = std::shared_ptr<Sender>;
using Senders = std::vector<SenderPointer>;

} // namespace midi
