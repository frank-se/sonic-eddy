#include "spa_helpers/spa_pod_helpers.h"

spa_pod *spa_helpers::get_pod_body(const spa_pod *source_pod) {
  return reinterpret_cast<spa_pod*>(reinterpret_cast<uintptr_t>(source_pod) +
    static_cast<ptrdiff_t>(sizeof(struct spa_pod)));
}
