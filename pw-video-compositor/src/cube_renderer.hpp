// The only translation unit that includes raylib.h - kept isolated because
// raylib defines short, common type/macro names (Color, Rectangle,
// CloseWindow, ...) that collide with PipeWire/SPA headers used elsewhere in
// this project.
//
// CubeRenderer owns a hidden-window raylib/GL context and must only ever be
// constructed, used, and destroyed from a single thread (raylib's GL context
// is thread-affine) - see midi_cube_main.cpp's render thread.
#pragma once

#include <cstdint>
#include <vector>

namespace midi_cube {

// One MIDI note's visible lifetime: starts at note-on, closes at note-off.
// end_seconds is unset (has_end = false) while the note is still held - the
// renderer treats it as extending to "now" every frame.
struct NoteSpan {
  uint8_t channel = 0;
  uint8_t note_number = 0;
  uint8_t velocity = 0;
  double start_seconds = 0.0;
  double end_seconds = 0.0;
  bool has_end = false;
};

// Also carries the background clear color - not strictly camera state, but
// kept together to avoid a second small config struct for three bytes.
struct CameraConfig {
  float pos_x = -3.0f;
  float pos_y = 10.0f;
  float pos_z = 16.0f;
  float target_x = -8.0f;
  float target_y = 4.0f;
  float target_z = 2.0f;
  float fov_y = 55.0f;
  uint8_t background_r = 8;
  uint8_t background_g = 8;
  uint8_t background_b = 14;
};

class CubeRenderer {
public:
  CubeRenderer(uint32_t width, uint32_t height, double time_window_seconds,
               const CameraConfig &camera);
  ~CubeRenderer();

  CubeRenderer(const CubeRenderer &) = delete;
  CubeRenderer &operator=(const CubeRenderer &) = delete;

  // Draws all spans visible in the current time window, reads the result
  // back, and writes exactly width*height*4 RGBA bytes into out_rgba
  // (resized if needed). Must be called from the thread that constructed
  // this renderer.
  void render(const std::vector<NoteSpan> &spans, double now,
              std::vector<uint8_t> &out_rgba);

private:
  uint32_t _width;
  uint32_t _height;
  double _time_window_seconds;
  CameraConfig _camera;

  // Opaque storage for raylib types (RenderTexture2D, Mesh, Material,
  // Shader) - kept out of this header so nothing outside cube_renderer.cpp
  // needs raylib.h.
  struct Impl;
  Impl *_impl;
};

} // namespace midi_cube
