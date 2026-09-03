#include "midi_note_registry.hpp"

namespace midi_cube {

void NoteRegistry::note_on(uint8_t channel, uint8_t note_number, uint8_t velocity,
                           double t) {
  std::lock_guard<std::mutex> lock(_mutex);
  const NoteKey key{channel, note_number};

  // Retrigger without an intervening note-off (legato/duplicate note-on for
  // an already-active key): close the previous span immediately at the new
  // t, then open a fresh one - never leak the old _active_index entry.
  auto it = _active_index.find(key);
  if (it != _active_index.end()) {
    _spans[it->second].has_end = true;
    _spans[it->second].end_seconds = t;
    _active_index.erase(it);
  }

  NoteSpan span;
  span.channel = channel;
  span.note_number = note_number;
  span.velocity = velocity;
  span.start_seconds = t;
  span.has_end = false;
  _spans.push_back(span);
  _active_index[key] = _spans.size() - 1;
}

void NoteRegistry::note_off(uint8_t channel, uint8_t note_number, double t) {
  std::lock_guard<std::mutex> lock(_mutex);
  const NoteKey key{channel, note_number};
  auto it = _active_index.find(key);
  if (it == _active_index.end())
    return; // stray note-off (no active span for this key) - ignore
  _spans[it->second].has_end = true;
  _spans[it->second].end_seconds = t;
  _active_index.erase(it);
}

std::vector<NoteSpan> NoteRegistry::snapshot(double now, double window_seconds) {
  std::lock_guard<std::mutex> lock(_mutex);
  const double cutoff = now - window_seconds;

  // _spans is in arrival order, so fully-scrolled-out closed spans
  // accumulate at the front. A still-open span (e.g. a long-held note)
  // sitting at the front blocks further pruning behind it - that's fine,
  // it just means we temporarily keep more history than strictly needed,
  // not a correctness issue.
  bool pruned = false;
  while (!_spans.empty() && _spans.front().has_end &&
         _spans.front().end_seconds < cutoff) {
    _spans.pop_front();
    pruned = true;
  }
  if (pruned) {
    _active_index.clear();
    for (size_t i = 0; i < _spans.size(); ++i) {
      if (!_spans[i].has_end)
        _active_index[NoteKey{_spans[i].channel, _spans[i].note_number}] = i;
    }
  }

  return std::vector<NoteSpan>(_spans.begin(), _spans.end());
}

} // namespace midi_cube
