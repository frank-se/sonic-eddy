#pragma once

#include "cube_renderer.hpp" // NoteSpan

#include <cstddef>
#include <cstdint>
#include <deque>
#include <mutex>
#include <unordered_map>
#include <vector>

namespace midi_cube {

struct NoteKey {
  uint8_t channel;
  uint8_t note_number;

  bool operator==(const NoteKey &other) const {
    return channel == other.channel && note_number == other.note_number;
  }
};

struct NoteKeyHash {
  size_t operator()(const NoteKey &key) const {
    return (static_cast<size_t>(key.channel) << 8) | key.note_number;
  }
};

// Tracks active + recently-closed notes, keyed by (channel, note_number).
// note_on()/note_off() are called from the MIDI input stream's RT process
// callback; snapshot() is called from the render thread. Mutex-guarded like
// pw-video-compositor/src/main.cpp's FrameSource/RenderSlot cross-thread
// state - this is a generative visualizer, not sample-accurate DSP, so a
// short lock in the MIDI RT callback is an acceptable tradeoff.
class NoteRegistry {
public:
  void note_on(uint8_t channel, uint8_t note_number, uint8_t velocity, double t);
  void note_off(uint8_t channel, uint8_t note_number, double t);

  // Render-thread only: prunes spans fully scrolled out of the window and
  // returns a snapshot copy for this frame.
  std::vector<NoteSpan> snapshot(double now, double window_seconds);

private:
  std::mutex _mutex;
  // Maps a currently-held key to the index of its open span in _spans.
  std::unordered_map<NoteKey, size_t, NoteKeyHash> _active_index;
  std::deque<NoteSpan> _spans;
};

} // namespace midi_cube
