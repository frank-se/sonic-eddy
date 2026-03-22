#include "frmidimapper.h"
#include "layers/LayerManager.h"

#include <iostream>
#include <ostream>
#include <unistd.h>

void layer_select_callback(const size_t midi_port_id, const size_t layer_id) {
  std::cout << "layer_select_callback" << std::endl;
  std::cout << midi_port_id << std::endl;
  std::cout << layer_id << std::endl;
}

void channel_select_callback(const size_t midi_port_id,
                             const size_t channel_id) {
  std::cout << "channel_select_callback" << std::endl;
  std::cout << midi_port_id << std::endl;
  std::cout << channel_id << std::endl;
}

void dial_selection_mode_callback(const size_t midi_port_id,
                                  const size_t channel_id,
                                  const controllers::DialMode mode) {
  std::cout << "dial_selection_mode_callback" << std::endl;
  std::cout << midi_port_id << std::endl;
  std::cout << channel_id << std::endl;
}

void filter_params_section_select_callback(const size_t midi_port_id,
                                           const size_t channel_id,
                                           const size_t section_id) {
  std::cout << "filter_params_section_select_callback" << std::endl;
  std::cout << midi_port_id << std::endl;
  std::cout << channel_id << std::endl;
}

void filter_params_move_right_callback(const size_t midi_port_id,
                                       const uint64_t step_count) {
  std::cout << "filter_params_move_right_callback" << std::endl;
  std::cout << midi_port_id << std::endl;
  std::cout << step_count << std::endl;
}

void filter_params_move_left_callback(const size_t midi_port_id,
                                      const uint64_t step_count) {
  std::cout << "filter_params_move_left_callback" << std::endl;
  std::cout << midi_port_id << std::endl;
  std::cout << step_count << std::endl;
}

int main(int argc, char **argv) {
  init();
  start();

  auto _ = create_mm_1_port(
      "midi-controller", "MIDI Mix MIDI 1 (playback)", layer_select_callback,
      channel_select_callback, dial_selection_mode_callback,
      filter_params_section_select_callback, filter_params_move_right_callback,
      filter_params_move_left_callback);

  sleep(1000);

  return 0;
}