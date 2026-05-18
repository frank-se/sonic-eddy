#pragma once
#include <spa/pod/pod.h>

namespace spa_helpers {
spa_pod *get_pod_body(const spa_pod *source_pod);
}
