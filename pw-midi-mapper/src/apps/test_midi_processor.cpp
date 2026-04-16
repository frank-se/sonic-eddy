#include "frmidimapper.h"

#include <iostream>
#include <ostream>
#include <unistd.h>

void layer_select_callback(const size_t layer_id) {
  std::cout << "layer_select_callback" << std::endl;
  std::cout << "layer id: " << layer_id << std::endl;
}

void channel_select_callback(const controllers::ChannelType channel_type,
                             const size_t channel_id) {
  std::cout << "channel_select_callback" << std::endl;
  std::cout << "channel type: " << channel_type << std::endl;
  std::cout << "channel id: " << channel_id << std::endl;
}

void dial_selection_mode_callback(const controllers::ChannelType channel_type,
                                  const size_t channel_id,
                                  const controllers::DialMode mode) {
  std::cout << "dial_selection_mode_callback" << std::endl;
  std::cout << "channel type: " << channel_type << std::endl;
  std::cout << "channel id: " << channel_id << std::endl;
  std::cout << "dial mode: " << mode << std::endl;
}

void filter_params_section_select_callback(
    const controllers::ChannelType channel_type, const size_t channel_id,
    const size_t section_id) {
  std::cout << "filter_params_section_select_callback" << std::endl;
  std::cout << "channel type: " << channel_type << std::endl;
  std::cout << "channel id: " << channel_id << std::endl;
  std::cout << "section id: " << section_id << std::endl;
}

void filter_params_move_right_callback(const uint64_t step_count) {
  std::cout << "filter_params_move_right_callback" << std::endl;
  std::cout << "step count: " << step_count << std::endl;
}

void filter_params_move_left_callback(const uint64_t step_count) {
  std::cout << "filter_params_move_left_callback" << std::endl;
  std::cout << "step count: " << step_count << std::endl;
}

void midi_control_change_update_callback(
    const controllers::ChannelType channel_type, const ulong channel_id,
    const ulong object_serial, const char *parameter_name,
    const float normalized_controller_value, const float normalized_known_value,
    const bool catching_up) {
  std::cout << "midi_control_change_update_callback" << std::endl;
  std::cout << "channel type: " << channel_type << std::endl;
  std::cout << "channel id: " << channel_id << std::endl;
  std::cout << "object serial: " << object_serial << std::endl;
  std::cout << "parameter name: " << parameter_name << std::endl;
  std::cout << "controller value: " << normalized_controller_value << std::endl;
  std::cout << "known value: " << normalized_known_value << std::endl;
  std::cout << "catching up: " << catching_up << std::endl;
}

int main(int argc, char **argv) {
  init(midi_control_change_update_callback);
  start();

  [[maybe_unused]] auto mm_1_port_id = create_mm_1_port(
      "midi-controller", "CMD-MM1", layer_select_callback,
      channel_select_callback, dial_selection_mode_callback,
      filter_params_section_select_callback, filter_params_move_right_callback,
      filter_params_move_left_callback);

  [[maybe_unused]] auto midi_mix_port_id = create_midi_mix_port(
      "midi-controller", "MIDI-MIX", layer_select_callback,
      channel_select_callback, dial_selection_mode_callback,
      filter_params_section_select_callback);

  [[maybe_unused]] auto faderfox_port =
      create_fader_fox_pc4_port("midi-controller", "FaderFox");

  sleep(1000);

  return 0;
}