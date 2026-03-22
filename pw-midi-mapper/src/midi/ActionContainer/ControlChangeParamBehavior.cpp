#include "midi/ActionContainer/ControlChangeParamBehavior.h"

#include <cstring>
#include <iostream>
#include <spa/debug/pod.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

void midi::action_container::ControlChangeParamBehavior::bind() {
  pw_loop_invoke(
      pw_main_loop_get_loop(_loop),
      [](spa_loop *loop, bool async, std::uint32_t seq, const void *data,
         size_t size, void *user_data) {
        const auto behavior =
            static_cast<ControlChangeParamBehavior *>(user_data);
        behavior->setup_node_listener();
        return 0;
      },
      0, nullptr, 0, true, this);
}

spa_pod *get_pod_body(const spa_pod *source_pod) {
  return reinterpret_cast<spa_pod *>(reinterpret_cast<uintptr_t>(source_pod) +
                                     static_cast<ptrdiff_t>(sizeof(spa_pod)));
}

void handle_params_changed_event_cc_behavior(void *user_data,
                                             int sequence_number, uint32_t id,
                                             uint32_t index, uint32_t next,
                                             const spa_pod *pod) {
  const auto behavior =
      static_cast<midi::action_container::ControlChangeParamBehavior *>(
          user_data);

  if (SPA_POD_TYPE(pod) != SPA_TYPE_Object)
    return;

  const auto params_pod = spa_pod_find_prop(pod, nullptr, SPA_PROP_params);

  if (params_pod == nullptr)
    return;

  auto pod_body_pointer = get_pod_body(&params_pod->value);
  spa_pod *child = nullptr;
  size_t i = 0;
  float result = 0.0f;
  bool store_next = false;
  bool found = false;
  const char *key = nullptr;
  SPA_POD_FOREACH(pod_body_pointer, SPA_POD_BODY_SIZE(&params_pod->value),
                  child) {
    if (store_next) {
      spa_pod_get_float(child, &result);
      found = true;
      break;
    }

    if (i % 2 != 0) {
      i++;
      return;
    }

    if (child->type == SPA_TYPE_String) {
      spa_pod_get_string(child, &key);
      if (std::strcmp(key, behavior->parameter_name().c_str()) == 0) {
        store_next = true;
      }
    } else {
      std::cerr << "Error: Unsupported type for key" << std::endl;
      break;
    }

    i++;
  }

  if (found) {
    behavior->set_value(result);
  }
}

static constexpr struct pw_node_events node_events = {
    .version = PW_VERSION_NODE_EVENTS,
    .param = handle_params_changed_event_cc_behavior};

void midi::action_container::ControlChangeParamBehavior::setup_node_listener() {
  _node = static_cast<pw_node *>(pw_registry_bind(
      _registry, _object_id, PW_TYPE_INTERFACE_Node, PW_VERSION_NODE, 0));

  pw_node_add_listener(_node, &_node_listener, &node_events, this);

  std::array<uint32_t, 1> parameter_ids = {SPA_PARAM_Props};
  pw_node_subscribe_params(_node, parameter_ids.data(), parameter_ids.size());

  pw_node_enum_params(_node, 0, PW_ID_ANY, 0, 0, nullptr);
}

void midi::action_container::ControlChangeParamBehavior::build_set_params_pod(
    const float value, set_params_data &data) const {
  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, data.buffer, set_params_data::size);

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props,
                              SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);
  spa_pod_builder_string(&builder, _parameter_name.c_str());
  spa_pod_builder_float(&builder, value);

  spa_pod_builder_pop(&builder, &struct_frame);
  data.pod =
      static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame));
}

void midi::action_container::ControlChangeParamBehavior::process(
    const ControlChangeMessages &control_change) {
  std::visit(
      [this](auto &&arg) {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, ControlChangeV1>) {
          constexpr float midi_value_max = 127.0f;
          auto normalized_value = arg.value / midi_value_max;
          auto new_value = _min + normalized_value * (_max - _min);

          auto data = new set_params_data{
              .node = _node,
          };

          build_set_params_pod(new_value, *data);

          pw_loop_invoke(
              pw_main_loop_get_loop(_loop),
              [](spa_loop *loop, bool async, std::uint32_t seq,
                 const void *data, size_t size, void *user_data) {
                auto set_params_data =
                    static_cast<struct set_params_data *>(user_data);

                spa_debug_pod(2, nullptr, set_params_data->pod);

                pw_node_set_param(set_params_data->node, SPA_PARAM_Props, 0,
                                  set_params_data->pod);

                delete set_params_data;
                return 0;
              },
              0, nullptr, 0, false, data);
        } else if constexpr (std::is_same_v<T, ControlChangeV2>) {
        }
      },
      control_change);
}
