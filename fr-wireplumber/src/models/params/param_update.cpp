#include "models//params/param_update.h"

#include <cassert>
#include <iostream>
#include <ostream>
#include <spa/debug/pod.h>
#include <spa/pod/iter.h>

#include "models/params/param_type.h"
#include "spa_helpers/spa_pod_helpers.h"

void models::params::fill_params_from_pod(const spa_pod *params_pod,
                                          ParamUpdate &params) {
  auto count = count_params(params_pod);
  if (count == 0)
    return;
  params.count = count;
  fill_params(params, params_pod);
}

void models::params::fill_params(ParamUpdate &params,
                                 const spa_pod *params_pod) {
  auto pod_body_pointer = spa_helpers::get_pod_body(params_pod);
  spa_pod *child = nullptr;
  params.keys = new const char *[params.count]();
  params.types = new ParamType[params.count]();
  params.values = new uint64_t[params.count]();

  const char **key_ptr = params.keys;
  ParamType *type_ptr = params.types;
  uint64_t *value_ptr = params.values;
  size_t index = 0;
  SPA_POD_FOREACH(pod_body_pointer, SPA_POD_BODY_SIZE(params_pod), child) {
    if (index % 2 == 0) {
      if (child->type == SPA_TYPE_String) {
        spa_pod_get_string(child, key_ptr++);
      } else {
        std::cerr << "Error: Unsupported type for key" << std::endl;
      }
    } else {
      switch (child->type) {
      case SPA_TYPE_Bool:
        bool b;
        spa_pod_get_bool(child, &b);
        *type_ptr++ = ParamType::Bool;
        *reinterpret_cast<uint32_t *>(value_ptr) = b ? 1 : 0;
        break;
      case SPA_TYPE_Int:
        int32_t i;
        spa_pod_get_int(child, &i);
        *type_ptr++ = ParamType::Int;
        *reinterpret_cast<int32_t *>(value_ptr) = i;
        break;
      case SPA_TYPE_Float:
        float f;
        spa_pod_get_float(child, &f);
        *type_ptr++ = ParamType::Float;
        *reinterpret_cast<float *>(value_ptr) = f;
        break;
      case SPA_TYPE_Double:
        double d;
        spa_pod_get_double(child, &d);
        *type_ptr++ = ParamType::Double;
        *reinterpret_cast<double *>(value_ptr) = d;
        break;
      case SPA_TYPE_String:
        const char *s;
        spa_pod_get_string(child, &s);
        *type_ptr++ = ParamType::String;
        *reinterpret_cast<const char **>(value_ptr) = s;
        break;
      case SPA_TYPE_Long:
        long l;
        spa_pod_get_long(child, &l);
        *type_ptr++ = ParamType::Long;
        *reinterpret_cast<long *>(value_ptr) = l;
        break;
      default:
        std::cerr << "spa_pod: unknown type " << child->type << std::endl;
        break;
      }

      ++value_ptr;
    }

    index++;
  }
}

size_t models::params::count_params(const spa_pod *params_pod) {
  auto pod_body_pointer = spa_helpers::get_pod_body(params_pod);
  spa_pod *child = nullptr;
  size_t count(0);
  size_t index(0);
  SPA_POD_FOREACH(pod_body_pointer, SPA_POD_BODY_SIZE(params_pod), child) {
    if (index % 2 == 0) {
      index++;
      continue;
    }

    count++;
    index++;
  }

  return count;
}

void models::params::params_data_delete(ParamUpdate &param_update) {
  if (param_update.keys != nullptr) {
    delete param_update.keys;
    param_update.keys = nullptr;
  }

  if (param_update.types != nullptr) {
    delete param_update.types;
    param_update.types = nullptr;
  }

  if (param_update.values != nullptr) {
    delete param_update.values;
    param_update.values = nullptr;
  }
}
