#include "midi/ActionContainer/SetVolumesBehavior.h"

#include "audio/pan.h"

#include <complex>
#include <iostream>
#include <pipewire/pipewire.h>
#include <spa/debug/pod.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

void midi::action_container::SetVolumesBehavior::bind() {
  pw_loop_invoke(
      pw_main_loop_get_loop(_loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto behavior = static_cast<SetVolumesBehavior *>(user_data);
        behavior->setup_node_listener();
        return 0;
      },
      0, nullptr, 0, true, this);
}

void handle_params_changed_event(void *user_data, int sequence_number,
                                 uint32_t id, uint32_t index, uint32_t next,
                                 const spa_pod *pod) {
  const auto behavior =
      static_cast<midi::action_container::SetVolumesBehavior *>(user_data);

  if (SPA_POD_TYPE(pod) == SPA_TYPE_Object) {
    const auto channel_volumes_property =
        spa_pod_find_prop(pod, nullptr, SPA_PROP_channelVolumes);

    if (channel_volumes_property == nullptr)
      return;

    const auto channel_volumes_array = &channel_volumes_property->value;
    if (!spa_pod_is_array(channel_volumes_array))
      return;

    const auto number_of_channels =
        SPA_POD_ARRAY_N_VALUES(channel_volumes_array);

    if (number_of_channels != 2)
      return;

    const auto volume_type = SPA_POD_ARRAY_VALUE_TYPE(channel_volumes_array);

    if (volume_type != SPA_TYPE_Float)
      return;

    const auto channel_volumes =
        static_cast<float *>(SPA_POD_ARRAY_VALUES(channel_volumes_array));

    const auto left = channel_volumes[0];
    const auto right = channel_volumes[1];

    if (left < 0.0002 && right < 0.0002) {
      behavior->set_volume(0.0f);
      return;
    }

    auto [pan, volume] =
        audio::pan::get_pan_and_volume_from_gains({left, right});

    behavior->set_pan(pan);
    behavior->set_volume(volume);
  }
}

static constexpr struct pw_node_events node_events = {
    .version = PW_VERSION_NODE_EVENTS, .param = handle_params_changed_event};

void midi::action_container::SetVolumesBehavior::setup_node_listener() {
  _node = static_cast<pw_node *>(pw_registry_bind(
      _registry, _object_id, PW_TYPE_INTERFACE_Node, PW_VERSION_NODE, 0));

  pw_node_add_listener(_node, &_node_listener, &node_events, this);

  std::array<uint32_t, 1> parameter_ids = {SPA_PARAM_Props};
  pw_node_subscribe_params(_node, parameter_ids.data(), parameter_ids.size());

  pw_node_enum_params(_node, 0, PW_ID_ANY, 0, 0, nullptr);
}

struct set_volume_data {
  constexpr static size_t size = 128;
  std::uint8_t buffer[size]{};
  spa_pod *pod = nullptr;
  pw_node *node = nullptr;
  uint64_t object_id = 0;
};

void fill_set_value_pod(const std::array<float, 2> gains,
                        set_volume_data &props_data) {
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, &props_data.buffer, sizeof(props_data.buffer));

  spa_pod_frame frame{};
  spa_pod_builder_push_object(&builder, &frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);

  spa_pod_builder_prop(&builder, SPA_PROP_channelVolumes, 0);

  spa_pod_frame array_frame{};
  spa_pod_builder_push_array(&builder, &array_frame);

  spa_pod_builder_float(&builder, gains[0]);
  spa_pod_builder_float(&builder, gains[1]);

  spa_pod_builder_pop(&builder, &array_frame);

  props_data.pod =
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &frame));
}

void midi::action_container::SetVolumesBehavior::process(
    const ControlChangeMessages &control_change) {
  std::array<float, 2> gains{};
  std::visit(
      [this, &gains](auto &&message) {
        using T = std::decay_t<decltype(message)>;
        if constexpr (std::is_same_v<T, ControlChangeV1>) {
          constexpr auto max = 127.0;
          const auto current_volume_in_controller_units =
              std::floor(max * _volume);

          if (current_volume_in_controller_units == message.value)
            return;

          const auto new_volume_normalized =
              static_cast<float>(static_cast<double>(message.value) / max);

          gains = audio::pan::get_gains_from_pan_and_volume(
              audio::pan::PanAndVolume{.pan = _pan,
                                       .volume = new_volume_normalized});
        } else if (std::is_same_v<T, ControlChangeV2>) {
          constexpr auto max =
              static_cast<float>(std::numeric_limits<uint32_t>::max());
          const auto current_volume_in_controller_units =
              std::floor(max * _volume);

          if (current_volume_in_controller_units == message.value)
            return;

          const auto new_volume_normalized =
              static_cast<float>(static_cast<double>(message.value) / max);

          gains = audio::pan::get_gains_from_pan_and_volume(
              audio::pan::PanAndVolume{.pan = _pan,
                                       .volume = new_volume_normalized});
        }
      },
      control_change);

  auto *props_data =
      new set_volume_data{.node = _node, .object_id = _object_id};
  fill_set_value_pod(gains, *props_data);

  pw_loop_invoke(
      pw_main_loop_get_loop(_loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        auto set_props_data = static_cast<set_volume_data *>(user_data);

        pw_node_set_param(set_props_data->node, SPA_PARAM_Props, 0,
                          set_props_data->pod);

        delete set_props_data;
        return 0;
      },
      0, nullptr, 0, false, props_data);
}
