#pragma once
#include "registry/Registry.h"

#include <memory>
#include <pipewire/pipewire.h>

namespace pipewire {

class Pipewire {
public:
  Pipewire() { setup_pipewire(); };

  ~Pipewire() {
    _registry.reset();

    if (_core != nullptr)
      pw_core_disconnect(_core);

    _core = nullptr;

    if (_context != nullptr)
      pw_context_destroy(_context);

    _context = nullptr;

    if (_loop == nullptr)
      return;
    pw_main_loop_destroy(_loop);
    _loop = nullptr;
  }

  void run() const { pw_main_loop_run(_loop); }

  void quit_main_loop() const { pw_main_loop_quit(_loop); }

  [[nodiscard]] registry::Registry &registry() const { return *_registry; }

  [[nodiscard]] pw_main_loop *loop() const { return _loop; }

private:
  pw_main_loop *_loop = nullptr;
  pw_context *_context = nullptr;
  pw_core *_core = nullptr;

  /*
   * Lifetime management is necessary here, so no smart_ptr
   */
  std::unique_ptr<registry::Registry> _registry = nullptr;

  static void do_quit(void *user_data, int signal_number);

  void setup_pipewire();
};

} // namespace pipewire
