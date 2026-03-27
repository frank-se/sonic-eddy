#pragma once

#include "MidiVersion.h"
#include "midi/Messages.h"

#include <condition_variable>
#include <pipewire/pipewire.h>

#include <boost/lockfree/spsc_queue.hpp>

#include <thread>

namespace midi {

using LockFreeQueueReceiver =
    boost::lockfree::spsc_queue<Message, boost::lockfree::capacity<128>>;

class Receiver {
public:
  Receiver(std::string pmx_purpose, std::string pmx_tag, std::string name,
           pw_main_loop *loop, const MidiVersion midi_version,
           std::mutex *queue_wait_mutex,
           std::condition_variable *queue_wait_condition)
      : _pmx_purpose(std::move(pmx_purpose)), _pmx_tag(std::move(pmx_tag)),
        _name(std::move(name)), _loop(loop), _midi_version(midi_version),
        _queue_wait_mutex(queue_wait_mutex),
        _queue_wait_condition(queue_wait_condition) {}

  ~Receiver() {
    if (_stream != nullptr) {
      pw_stream_destroy(_stream);
      _stream = nullptr;
    }
  }

  void process();

  void setup();

  std::optional<Message> pop();

private:
  std::string _pmx_purpose;
  std::string _pmx_tag;
  std::string _name;
  pw_main_loop *_loop;
  MidiVersion _midi_version;
  pw_stream *_stream = nullptr;
  LockFreeQueueReceiver _queue{};

  std::mutex *_queue_wait_mutex = nullptr;
  std::condition_variable *_queue_wait_condition;
};

using ReceiverPointer = std::shared_ptr<Receiver>;
using Receivers = std::vector<ReceiverPointer>;

} // namespace midi
