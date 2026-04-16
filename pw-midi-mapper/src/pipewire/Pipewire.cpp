#include "pipewire/Pipewire.h"

void pipewire::Pipewire::setup_pipewire(
    controllers::MidiControlChangeUpdateCallback callback) {
  logging::log<logging::LogLevel::Trace>("Pipewire::setup_pipewire");

  pw_init(nullptr, nullptr);

  _loop = pw_main_loop_new(nullptr);

  pw_loop_add_signal(pw_main_loop_get_loop(_loop), SIGINT, do_quit, this);
  pw_loop_add_signal(pw_main_loop_get_loop(_loop), SIGTERM, do_quit, this);

  _context = pw_context_new(pw_main_loop_get_loop(_loop), nullptr, 0);
  _core = pw_context_connect(_context, nullptr, 0);
  _registry =
      std::make_unique<registry::Registry>(_loop, _core, std::move(callback));
}

void pipewire::Pipewire::do_quit(void *user_data, int signal_number) {
  logging::log<logging::LogLevel::Trace>("Pipewire::do_quit");

  const auto pipewire = static_cast<Pipewire *>(user_data);
  pipewire->quit_main_loop();
}
