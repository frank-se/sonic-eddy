// Unified GPU-native replacement for pw-video-compositor (A+B) +
// video-blender + downstream-compositor + av_sync_record, merged into one
// process so composited frames never leave the GPU (or the CPU, for
// PipeWire-buffer round-trips) between stages. See project conversation
// 2026-09-09 for the full design rationale (GL/EGL over Vulkan given the
// scope actually needed here; dmabuf import for camera ingestion; VA-API
// hardware encode for recording).
//
// Step 2 of the build-up (step 1 was just the headless EGL context, see git
// history/task tracker): the actual per-object render pass replacing
// main.cpp's composite_input's CPU blend math with a GL shader. This file
// self-tests that shader numerically (renders a known texture with a known
// transform/gain/opacity, reads back specific pixels, compares against the
// exact same formula composite_input uses) before any PipeWire/scene-JSON
// wiring is added on top - the shader math itself needs to be proven
// correct in isolation first.
#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <deque>
#include <string>
#include <thread>
#include <vector>

#include <raylib.h>

#include <fcntl.h>
#include <unistd.h>

#include <gbm.h>

#include <EGL/egl.h>
#include <EGL/eglext.h>
#include <GLES2/gl2.h>
#include <GLES2/gl2ext.h>

#include <libdrm/drm_fourcc.h>
#include <spa/debug/pod.h>
#include <spa/param/buffers.h>
#include <spa/param/props.h>

#include <boost/lockfree/spsc_value.hpp>

#include <pipewire/keys.h>
#include <pipewire/pipewire.h>
#include <spa/param/audio/format-utils.h>
#include <spa/param/video/format-utils.h>
#include <spa/pod/builder.h>
#include <spa/pod/iter.h>

#include <nlohmann/json.hpp>

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

#include "scene.hpp"
#include "video_config.hpp"

namespace {

constexpr const char *kRenderNode = "/dev/dri/renderD128";

bool has_egl_ext(EGLDisplay display, const char *name) {
  const char *exts = eglQueryString(display, EGL_EXTENSIONS);
  return exts != nullptr && std::strstr(exts, name) != nullptr;
}

EGLDisplay g_display = EGL_NO_DISPLAY;
EGLContext g_context = EGL_NO_CONTEXT;
gbm_device *g_gbm = nullptr;
int g_drm_fd = -1;

bool init_egl() {
  g_drm_fd = open(kRenderNode, O_RDWR);
  if (g_drm_fd < 0) {
    std::perror("open renderD128");
    return false;
  }
  g_gbm = gbm_create_device(g_drm_fd);
  if (g_gbm == nullptr) {
    std::fprintf(stderr, "gbm_create_device failed\n");
    return false;
  }
  auto get_platform_display = reinterpret_cast<PFNEGLGETPLATFORMDISPLAYEXTPROC>(
      eglGetProcAddress("eglGetPlatformDisplayEXT"));
  if (get_platform_display == nullptr) {
    std::fprintf(stderr, "eglGetPlatformDisplayEXT not available\n");
    return false;
  }
  g_display = get_platform_display(EGL_PLATFORM_GBM_KHR, g_gbm, nullptr);
  if (g_display == EGL_NO_DISPLAY) {
    std::fprintf(stderr, "eglGetPlatformDisplayEXT failed\n");
    return false;
  }
  EGLint major = 0, minor = 0;
  if (eglInitialize(g_display, &major, &minor) == EGL_FALSE) {
    std::fprintf(stderr, "eglInitialize failed: 0x%x\n", eglGetError());
    return false;
  }
  if (!has_egl_ext(g_display, "EGL_KHR_surfaceless_context")) {
    std::fprintf(stderr, "EGL_KHR_surfaceless_context not supported\n");
    return false;
  }
  if (eglBindAPI(EGL_OPENGL_ES_API) == EGL_FALSE) {
    std::fprintf(stderr, "eglBindAPI(GLES) failed\n");
    return false;
  }
  const EGLint config_attribs[] = {
      EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT,
      EGL_RED_SIZE, 8, EGL_GREEN_SIZE, 8, EGL_BLUE_SIZE, 8, EGL_ALPHA_SIZE, 8,
      EGL_NONE};
  EGLConfig config;
  EGLint num_configs = 0;
  if (eglChooseConfig(g_display, config_attribs, &config, 1, &num_configs) ==
          EGL_FALSE ||
      num_configs == 0) {
    std::fprintf(stderr, "eglChooseConfig failed\n");
    return false;
  }
  const EGLint context_attribs[] = {EGL_CONTEXT_CLIENT_VERSION, 2, EGL_NONE};
  g_context = eglCreateContext(g_display, config, EGL_NO_CONTEXT, context_attribs);
  if (g_context == EGL_NO_CONTEXT) {
    std::fprintf(stderr, "eglCreateContext failed: 0x%x\n", eglGetError());
    return false;
  }
  if (eglMakeCurrent(g_display, EGL_NO_SURFACE, EGL_NO_SURFACE, g_context) ==
      EGL_FALSE) {
    std::fprintf(stderr, "eglMakeCurrent failed: 0x%x\n", eglGetError());
    return false;
  }
  return true;
}

GLuint compile_shader(GLenum type, const char *src) {
  GLuint shader = glCreateShader(type);
  glShaderSource(shader, 1, &src, nullptr);
  glCompileShader(shader);
  GLint ok = 0;
  glGetShaderiv(shader, GL_COMPILE_STATUS, &ok);
  if (!ok) {
    char log[2048];
    glGetShaderInfoLog(shader, sizeof(log), nullptr, log);
    std::fprintf(stderr, "shader compile failed: %s\n", log);
    return 0;
  }
  return shader;
}

// Vertex shader: takes a per-object transform (position/size in canvas
// pixels -> NDC, plus a UV transform for scale/flip/rotate) - mirrors
// build_sample_maps' scale_x/scale_y/flip_horizontal/flip_vertical/rotate
// semantics from main.cpp, but as a UV remap instead of a CPU index table
// (see project conversation 2026-09-09 on why this collapses cleanly - it's
// a uniform affine transform, not an arbitrary resample).
constexpr const char *kVertexShaderSrc = R"(
attribute vec2 a_pos;      // quad corner in [0,1]x[0,1]
uniform vec2 u_canvas_size;
uniform vec2 u_dst_pos;    // dst_x, dst_y in canvas pixels
uniform vec2 u_dst_size;   // dst_width, dst_height in canvas pixels
uniform mat2 u_uv_rotate;  // handles rotate 0/90/180/270 as a UV-space rotation
uniform vec2 u_uv_flip;    // -1/+1 per axis for flip_horizontal/flip_vertical
varying vec2 v_uv;
void main() {
  vec2 pixel_pos = u_dst_pos + a_pos * u_dst_size;
  vec2 ndc = (pixel_pos / u_canvas_size) * 2.0 - 1.0;
  ndc.y = -ndc.y; // GL's +Y is up, our canvas +Y is down (dst_y grows downward)
  gl_Position = vec4(ndc, 0.0, 1.0);
  vec2 uv = a_pos;
  uv = uv * 2.0 - 1.0;      // center for rotation
  uv = u_uv_rotate * uv;
  uv = (uv + 1.0) * 0.5;    // back to [0,1]
  uv = 0.5 + (uv - 0.5) * u_uv_flip;
  v_uv = uv;
}
)";

// Fragment shader: replicates composite_input's blend_channel formula
// exactly - dst = src*weight + dst_existing*(1-weight), weight = opacity *
// (has_alpha ? srcAlpha : 1.0). Alpha is NOT forced to opaque - real
// transparency in the output is fine and often wanted (e.g. downstream
// consumers that composite this over something else); the old "never
// see-through" framing only applied to a live Wayland preview window
// specifically honoring surface alpha, not to a plain PipeWire video
// buffer/encoder input, which has no windowing system involved at all. gain
// is a straight per-channel multiply, same as apply_gain.
constexpr const char *kFragmentShaderSrc = R"(
precision mediump float;
varying vec2 v_uv;
uniform sampler2D u_source;
uniform vec3 u_gain;       // red_gain, green_gain, blue_gain
uniform float u_opacity;
uniform bool u_has_alpha;
void main() {
  vec4 src = texture2D(u_source, v_uv);
  vec3 gained = clamp(src.rgb * u_gain, 0.0, 1.0);
  float weight = u_opacity * (u_has_alpha ? src.a : 1.0);
  gl_FragColor = vec4(gained, weight); // alpha used as GL_SRC_ALPHA blend factor below
}
)";

GLuint g_program = 0;
GLint g_loc_pos = -1, g_loc_canvas_size = -1, g_loc_dst_pos = -1, g_loc_dst_size = -1,
      g_loc_uv_rotate = -1, g_loc_uv_flip = -1, g_loc_source = -1, g_loc_gain = -1,
      g_loc_opacity = -1, g_loc_has_alpha = -1;

bool init_program() {
  GLuint vs = compile_shader(GL_VERTEX_SHADER, kVertexShaderSrc);
  GLuint fs = compile_shader(GL_FRAGMENT_SHADER, kFragmentShaderSrc);
  if (vs == 0 || fs == 0)
    return false;
  g_program = glCreateProgram();
  glAttachShader(g_program, vs);
  glAttachShader(g_program, fs);
  glBindAttribLocation(g_program, 0, "a_pos");
  glLinkProgram(g_program);
  GLint ok = 0;
  glGetProgramiv(g_program, GL_LINK_STATUS, &ok);
  if (!ok) {
    char log[2048];
    glGetProgramInfoLog(g_program, sizeof(log), nullptr, log);
    std::fprintf(stderr, "program link failed: %s\n", log);
    return false;
  }
  g_loc_pos = 0;
  g_loc_canvas_size = glGetUniformLocation(g_program, "u_canvas_size");
  g_loc_dst_pos = glGetUniformLocation(g_program, "u_dst_pos");
  g_loc_dst_size = glGetUniformLocation(g_program, "u_dst_size");
  g_loc_uv_rotate = glGetUniformLocation(g_program, "u_uv_rotate");
  g_loc_uv_flip = glGetUniformLocation(g_program, "u_uv_flip");
  g_loc_source = glGetUniformLocation(g_program, "u_source");
  g_loc_gain = glGetUniformLocation(g_program, "u_gain");
  g_loc_opacity = glGetUniformLocation(g_program, "u_opacity");
  g_loc_has_alpha = glGetUniformLocation(g_program, "u_has_alpha");
  return true;
}

// Blend stage (video-blender equivalent): full-canvas quad, no per-object
// transform needed (both inputs are already the same canvas size) - just
// samples both textures and linearly cross-dissolves by blend_position (t),
// matching on_output_process's `av*(1-t) + bv*t` formula. Applied uniformly
// across all 4 channels including alpha - deliberately NOT forcing alpha to
// opaque (see the fragment shader comment above kFragmentShaderSrc and the
// 2026-09-09 design conversation: the old CPU code's alpha-forcing was only
// ever justified for a live Wayland preview window honoring real
// transparency, not for a plain buffer feeding another internal stage).
constexpr const char *kBlendVertexShaderSrc = R"(
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  gl_Position = vec4(a_pos * 2.0 - 1.0, 0.0, 1.0);
  v_uv = a_pos;
}
)";

constexpr const char *kBlendFragmentShaderSrc = R"(
precision mediump float;
varying vec2 v_uv;
uniform sampler2D u_tex_a;
uniform sampler2D u_tex_b;
uniform float u_t;
void main() {
  vec4 a = texture2D(u_tex_a, v_uv);
  vec4 b = texture2D(u_tex_b, v_uv);
  gl_FragColor = a * (1.0 - u_t) + b * u_t;
}
)";

GLuint g_blend_program = 0;
GLint g_blend_loc_pos = -1, g_blend_loc_tex_a = -1, g_blend_loc_tex_b = -1, g_blend_loc_t = -1;

bool init_blend_program() {
  GLuint vs = compile_shader(GL_VERTEX_SHADER, kBlendVertexShaderSrc);
  GLuint fs = compile_shader(GL_FRAGMENT_SHADER, kBlendFragmentShaderSrc);
  if (vs == 0 || fs == 0)
    return false;
  g_blend_program = glCreateProgram();
  glAttachShader(g_blend_program, vs);
  glAttachShader(g_blend_program, fs);
  glBindAttribLocation(g_blend_program, 0, "a_pos");
  glLinkProgram(g_blend_program);
  GLint ok = 0;
  glGetProgramiv(g_blend_program, GL_LINK_STATUS, &ok);
  if (!ok) {
    char log[2048];
    glGetProgramInfoLog(g_blend_program, sizeof(log), nullptr, log);
    std::fprintf(stderr, "blend program link failed: %s\n", log);
    return false;
  }
  g_blend_loc_pos = 0;
  g_blend_loc_tex_a = glGetUniformLocation(g_blend_program, "u_tex_a");
  g_blend_loc_tex_b = glGetUniformLocation(g_blend_program, "u_tex_b");
  g_blend_loc_t = glGetUniformLocation(g_blend_program, "u_t");
  return true;
}

// Draws the cross-dissolve into whatever FBO is currently bound - caller
// owns framebuffer binding/viewport, same convention as draw_object.
void draw_blend(GLuint tex_a, GLuint tex_b, float t) {
  glUseProgram(g_blend_program);
  glActiveTexture(GL_TEXTURE0);
  glBindTexture(GL_TEXTURE_2D, tex_a);
  glUniform1i(g_blend_loc_tex_a, 0);
  glActiveTexture(GL_TEXTURE1);
  glBindTexture(GL_TEXTURE_2D, tex_b);
  glUniform1i(g_blend_loc_tex_b, 1);
  glUniform1f(g_blend_loc_t, t);

  const float quad[8] = {0, 0, 1, 0, 0, 1, 1, 1};
  glVertexAttribPointer(g_blend_loc_pos, 2, GL_FLOAT, GL_FALSE, 0, quad);
  glEnableVertexAttribArray(g_blend_loc_pos);
  glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);
}

// UV rotation matrix for rotate in {0,90,180,270} - matches
// build_sample_maps' transposed/rotate semantics as a 2D rotation instead
// of a per-axis index table.
std::array<float, 4> rotate_matrix(uint32_t rotate_degrees) {
  switch (rotate_degrees) {
  case 90:
    return {0.0f, -1.0f, 1.0f, 0.0f};
  case 180:
    return {-1.0f, 0.0f, 0.0f, -1.0f};
  case 270:
    return {0.0f, 1.0f, -1.0f, 0.0f};
  default:
    return {1.0f, 0.0f, 0.0f, 1.0f};
  }
}

struct ObjectParams {
  float canvas_w = 0, canvas_h = 0;
  float dst_x = 0, dst_y = 0, dst_w = 0, dst_h = 0;
  uint32_t rotate = 0;
  bool flip_h = false, flip_v = false;
  float red_gain = 1, green_gain = 1, blue_gain = 1;
  float opacity = 1;
  bool has_alpha = false;
};

// One render pass: draws `source_tex` into whatever FBO/renderbuffer is
// currently bound, per `p`. Caller owns framebuffer binding/clearing/
// viewport and glBlendFunc state (see main()'s render_scene()) - this
// function only issues the one draw call, mirroring composite_input's
// per-object loop body but as a GPU draw instead of a CPU pixel loop.
void draw_object(GLuint source_tex, const ObjectParams &p) {
  glUseProgram(g_program);
  glUniform2f(g_loc_canvas_size, p.canvas_w, p.canvas_h);
  glUniform2f(g_loc_dst_pos, p.dst_x, p.dst_y);
  glUniform2f(g_loc_dst_size, p.dst_w, p.dst_h);
  const auto rot = rotate_matrix(p.rotate);
  glUniformMatrix2fv(g_loc_uv_rotate, 1, GL_FALSE, rot.data());
  glUniform2f(g_loc_uv_flip, p.flip_h ? -1.0f : 1.0f, p.flip_v ? -1.0f : 1.0f);
  glUniform3f(g_loc_gain, p.red_gain, p.green_gain, p.blue_gain);
  glUniform1f(g_loc_opacity, p.opacity);
  glUniform1i(g_loc_has_alpha, p.has_alpha ? 1 : 0);

  glActiveTexture(GL_TEXTURE0);
  glBindTexture(GL_TEXTURE_2D, source_tex);
  glUniform1i(g_loc_source, 0);

  const float quad[8] = {0, 0, 1, 0, 0, 1, 1, 1};
  glVertexAttribPointer(g_loc_pos, 2, GL_FLOAT, GL_FALSE, 0, quad);
  glEnableVertexAttribArray(g_loc_pos);
  glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);
}

GLuint make_texture(uint32_t w, uint32_t h, const uint8_t *rgba) {
  GLuint tex = 0;
  glGenTextures(1, &tex);
  glBindTexture(GL_TEXTURE_2D, tex);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, w, h, 0, GL_RGBA, GL_UNSIGNED_BYTE, rgba);
  return tex;
}

// CPU reference implementation of exactly the formula composite_input uses
// (blend_channel/apply_gain), for the same test case the shader renders -
// this is the ground truth the GL output gets checked against below.
uint8_t cpu_blend_channel(uint8_t src, float gain, uint8_t dst, float weight) {
  const float gained = std::clamp(static_cast<float>(src) * gain, 0.0f, 255.0f);
  return static_cast<uint8_t>(
      std::clamp(gained * weight + static_cast<float>(dst) * (1.0f - weight), 0.0f, 255.0f));
}

} // namespace

namespace {

bool close_enough(uint8_t a, uint8_t b) {
  return std::abs(static_cast<int>(a) - static_cast<int>(b)) <= 2; // rounding tolerance
}

constexpr uint32_t kCanvasW = 64, kCanvasH = 64;

// (x, image_row) in top-down "canvas image" coordinates (row 0 = top,
// matching dst_y's convention) - converted to GL's bottom-up framebuffer
// row order, since glReadPixels' row 0 is the bottom of the viewport, not
// the top. This conversion belongs at readback time (test or real export
// code), not in the shader: the shader's job is proven correct once the
// geometry lands in the right NDC location per GL's own rules, independent
// of whatever final Y-convention a later readback/encoder-export step
// settles on.
std::array<uint8_t, 4> pixel_at(const std::array<uint8_t, kCanvasW * kCanvasH * 4> &pixels,
                                 uint32_t x, uint32_t image_row) {
  const uint32_t buffer_row = kCanvasH - 1 - image_row;
  const size_t idx = (buffer_row * kCanvasW + x) * 4;
  return {pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]};
}

void setup_fbo(GLuint &fbo, GLuint &color_tex) {
  glGenFramebuffers(1, &fbo);
  glBindFramebuffer(GL_FRAMEBUFFER, fbo);
  glGenTextures(1, &color_tex);
  glBindTexture(GL_TEXTURE_2D, color_tex);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, kCanvasW, kCanvasH, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, color_tex, 0);
}

// Test 1: opacity/gain blend math, verified against the exact CPU formula
// composite_input uses (blend_channel/apply_gain) - a 1x1 solid-color
// source at half-canvas size onto a known non-black background (so
// opacity<1 errors are visible in R/G/B, not masked by blending onto black).
bool test_opacity_and_gain() {
  const uint8_t src_pixel[4] = {200, 100, 50, 255};
  GLuint source_tex = make_texture(1, 1, src_pixel);

  GLuint fbo = 0, color_tex = 0;
  setup_fbo(fbo, color_tex);
  if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
    std::fprintf(stderr, "FBO incomplete\n");
    return false;
  }

  glViewport(0, 0, kCanvasW, kCanvasH);
  const float bg_r = 40.0f / 255.0f, bg_g = 60.0f / 255.0f, bg_b = 80.0f / 255.0f;
  glClearColor(bg_r, bg_g, bg_b, 1.0f);
  glClear(GL_COLOR_BUFFER_BIT);
  glEnable(GL_BLEND);
  glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

  ObjectParams p;
  p.canvas_w = kCanvasW;
  p.canvas_h = kCanvasH;
  p.dst_w = kCanvasW / 2.0f;
  p.dst_h = kCanvasH / 2.0f;
  p.red_gain = 0.5f;
  p.opacity = 0.75f;
  draw_object(source_tex, p);
  glFinish();

  std::array<uint8_t, kCanvasW * kCanvasH * 4> pixels{};
  glReadPixels(0, 0, kCanvasW, kCanvasH, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());

  const auto inside = pixel_at(pixels, 10, 10);
  const uint8_t expected_r = cpu_blend_channel(src_pixel[0], 0.5f, static_cast<uint8_t>(bg_r * 255), 0.75f);
  const uint8_t expected_g = cpu_blend_channel(src_pixel[1], 1.0f, static_cast<uint8_t>(bg_g * 255), 0.75f);
  const uint8_t expected_b = cpu_blend_channel(src_pixel[2], 1.0f, static_cast<uint8_t>(bg_b * 255), 0.75f);
  std::printf("[opacity/gain] inside quad:  got (%d,%d,%d,%d)  expected (%d,%d,%d)\n",
              inside[0], inside[1], inside[2], inside[3], expected_r, expected_g, expected_b);

  const auto outside = pixel_at(pixels, 50, 50);
  const uint8_t bg_r8 = static_cast<uint8_t>(bg_r * 255), bg_g8 = static_cast<uint8_t>(bg_g * 255),
                bg_b8 = static_cast<uint8_t>(bg_b * 255);
  std::printf("[opacity/gain] outside quad: got (%d,%d,%d,%d)  expected (%d,%d,%d)\n",
              outside[0], outside[1], outside[2], outside[3], bg_r8, bg_g8, bg_b8);

  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);
  glDeleteTextures(1, &source_tex);

  return close_enough(inside[0], expected_r) && close_enough(inside[1], expected_g) &&
         close_enough(inside[2], expected_b) && close_enough(outside[0], bg_r8) &&
         close_enough(outside[1], bg_g8) && close_enough(outside[2], bg_b8);
}

// Test 2: rotate=180 on a 2x2 four-quadrant-colored source, full canvas
// size, no blending (opacity=1, opaque background so blend math can't mask
// a geometry bug). A correct 180 rotation swaps DIAGONALLY opposite
// quadrants - a convention-independent check (true regardless of whether
// "rotate" is defined clockwise or counterclockwise, unlike 90/270 which
// would need a direction convention to check unambiguously). A pure
// mirror/flip bug would only swap quadrants along one axis, not
// diagonally, and a no-op bug would leave them unchanged - both easily
// distinguished from a correct 180 rotation by this check.
bool test_rotate_180() {
  // 2x2 texture, row-major: (0,0)=red top-left, (1,0)=green top-right,
  // (0,1)=blue bottom-left, (1,1)=yellow bottom-right (as uploaded - GL's
  // own texture v-coordinate convention doesn't matter here since we only
  // care about relative quadrant swapping, not matching an external image
  // format's row order).
  const uint8_t quad_tex[2 * 2 * 4] = {
      255, 0,   0,   255, // (0,0) red
      0,   255, 0,   255, // (1,0) green
      0,   0,   255, 255, // (0,1) blue
      255, 255, 0,   255, // (1,1) yellow
  };
  GLuint source_tex = make_texture(2, 2, quad_tex);

  GLuint fbo = 0, color_tex = 0;
  setup_fbo(fbo, color_tex);
  if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
    std::fprintf(stderr, "FBO incomplete\n");
    return false;
  }

  glViewport(0, 0, kCanvasW, kCanvasH);
  glDisable(GL_BLEND);
  glClearColor(0, 0, 0, 1);
  glClear(GL_COLOR_BUFFER_BIT);

  ObjectParams p;
  p.canvas_w = kCanvasW;
  p.canvas_h = kCanvasH;
  p.dst_w = kCanvasW;
  p.dst_h = kCanvasH;
  p.rotate = 180;
  draw_object(source_tex, p);
  glFinish();

  std::array<uint8_t, kCanvasW * kCanvasH * 4> pixels{};
  glReadPixels(0, 0, kCanvasW, kCanvasH, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());

  // Sample near each canvas corner (not the exact edge, to avoid
  // interpolation at the quadrant boundary).
  const auto top_left = pixel_at(pixels, 4, 4);
  const auto top_right = pixel_at(pixels, kCanvasW - 5, 4);
  const auto bottom_left = pixel_at(pixels, 4, kCanvasH - 5);
  const auto bottom_right = pixel_at(pixels, kCanvasW - 5, kCanvasH - 5);

  auto is_color = [](const std::array<uint8_t, 4> &px, uint8_t r, uint8_t g, uint8_t b) {
    return close_enough(px[0], r) && close_enough(px[1], g) && close_enough(px[2], b);
  };

  std::printf("[rotate180] top_left=(%d,%d,%d) top_right=(%d,%d,%d) "
              "bottom_left=(%d,%d,%d) bottom_right=(%d,%d,%d)\n",
              top_left[0], top_left[1], top_left[2], top_right[0], top_right[1], top_right[2],
              bottom_left[0], bottom_left[1], bottom_left[2], bottom_right[0], bottom_right[1],
              bottom_right[2]);

  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);
  glDeleteTextures(1, &source_tex);

  // Whichever corner the un-rotated source's red/yellow (diagonal pair) and
  // green/blue (diagonal pair) land on, rotate=180 must swap each into its
  // diagonal opposite - i.e. whatever pair of colors ends up
  // top_left/bottom_right must be the SAME pair (in some order) as
  // top_right/bottom_left would be WITHOUT rotation. Simplest unambiguous
  // check: top_left and bottom_right must be different colors from each
  // other (still two distinct quadrants, not collapsed), AND the set of 4
  // corner colors observed must be exactly the 4 source colors (nothing
  // lost/duplicated) AND diagonal opposites must differ from their
  // *adjacent* (non-diagonal) corners in the same way the source's
  // diagonal pairs did - concretely: top_left must equal what was
  // ORIGINALLY bottom_right (yellow) and vice versa, since 180 rotation
  // maps each corner to its diagonal opposite.
  return is_color(top_left, 255, 255, 0) &&  // was yellow (bottom-right) before rotation
         is_color(bottom_right, 255, 0, 0) && // was red (top-left) before rotation
         is_color(top_right, 0, 0, 255) &&    // was blue (bottom-left) before rotation
         is_color(bottom_left, 0, 255, 0);    // was green (top-right) before rotation
}

// Test 3: flip_horizontal on a 2x1 (red|green) source, full canvas size -
// with the flip, red (originally left) must end up on the right half and
// green (originally right) on the left half.
bool test_flip_horizontal() {
  const uint8_t src_tex[2 * 1 * 4] = {
      255, 0, 0, 255, // left texel: red
      0, 255, 0, 255, // right texel: green
  };
  GLuint source_tex = make_texture(2, 1, src_tex);

  GLuint fbo = 0, color_tex = 0;
  setup_fbo(fbo, color_tex);
  if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
    std::fprintf(stderr, "FBO incomplete\n");
    return false;
  }

  glViewport(0, 0, kCanvasW, kCanvasH);
  glDisable(GL_BLEND);
  glClearColor(0, 0, 0, 1);
  glClear(GL_COLOR_BUFFER_BIT);

  ObjectParams p;
  p.canvas_w = kCanvasW;
  p.canvas_h = kCanvasH;
  p.dst_w = kCanvasW;
  p.dst_h = kCanvasH;
  p.flip_h = true;
  draw_object(source_tex, p);
  glFinish();

  std::array<uint8_t, kCanvasW * kCanvasH * 4> pixels{};
  glReadPixels(0, 0, kCanvasW, kCanvasH, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());

  const auto left = pixel_at(pixels, 4, kCanvasH / 2);
  const auto right = pixel_at(pixels, kCanvasW - 5, kCanvasH / 2);
  std::printf("[flip_h] left=(%d,%d,%d) right=(%d,%d,%d) (expect left=green, right=red)\n",
              left[0], left[1], left[2], right[0], right[1], right[2]);

  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);
  glDeleteTextures(1, &source_tex);

  return close_enough(left[0], 0) && close_enough(left[1], 255) && close_enough(left[2], 0) &&
         close_enough(right[0], 255) && close_enough(right[1], 0) && close_enough(right[2], 0);
}

// Test: blend stage (video-blender equivalent) - two solid full-canvas
// textures with distinct, non-round RGBA values (avoids masking a formula
// bug that happens to cancel out at round numbers), cross-dissolved at
// t=0.3, checked against the exact lerp formula including alpha (alpha is
// NOT forced to opaque here - see kBlendFragmentShaderSrc comment).
bool test_blend_stage() {
  const uint8_t color_a[4] = {200, 50, 25, 100};
  const uint8_t color_b[4] = {10, 220, 90, 200};
  GLuint tex_a = make_texture(1, 1, color_a);
  GLuint tex_b = make_texture(1, 1, color_b);

  GLuint fbo = 0, color_tex = 0;
  setup_fbo(fbo, color_tex);
  if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
    std::fprintf(stderr, "[blend] FBO incomplete\n");
    return false;
  }

  glViewport(0, 0, kCanvasW, kCanvasH);
  glDisable(GL_BLEND); // the blend shader itself computes the final color - no GL blend-func needed
  const float t = 0.3f;
  draw_blend(tex_a, tex_b, t);
  glFinish();

  std::array<uint8_t, kCanvasW * kCanvasH * 4> pixels{};
  glReadPixels(0, 0, kCanvasW, kCanvasH, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
  const auto got = pixel_at(pixels, kCanvasW / 2, kCanvasH / 2);

  uint8_t expected[4];
  for (int c = 0; c < 4; ++c)
    expected[c] = static_cast<uint8_t>(std::clamp(
        color_a[c] * (1.0f - t) + color_b[c] * t, 0.0f, 255.0f));

  std::printf("[blend] got (%d,%d,%d,%d) expected (%d,%d,%d,%d)\n", got[0], got[1], got[2], got[3],
              expected[0], expected[1], expected[2], expected[3]);

  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);
  glDeleteTextures(1, &tex_a);
  glDeleteTextures(1, &tex_b);

  return close_enough(got[0], expected[0]) && close_enough(got[1], expected[1]) &&
         close_enough(got[2], expected[2]) && close_enough(got[3], expected[3]);
}

// Live video ingestion: a PipeWire input stream, RT-safe handoff via the
// same boost::lockfree::spsc_value pattern as main.cpp's FrameSource (see
// feedback_rt_thread_no_locks memory) - on_input_process only memcpy's,
// never touches GL. The GPU worker/main thread later consumes the latest
// frame and uploads it via glTexSubImage2D - that upload is NOT itself
// RT-safe (arbitrary driver latency), which is fine here since it happens
// on this process's own render-driving thread, not inside a
// PW_STREAM_FLAG_RT_PROCESS callback.
constexpr uint32_t kMaxVideoWidth = 1920, kMaxVideoHeight = 1080;
constexpr size_t kMaxVideoFrameBytes = static_cast<size_t>(kMaxVideoWidth) * kMaxVideoHeight * 4;

struct IngestFrameSlot {
  std::array<uint8_t, kMaxVideoFrameBytes> data{};
  bool has_frame = false;
};

struct VideoInput {
  uint32_t width = 0;
  uint32_t height = 0;
  uint32_t bytes_per_pixel = 4; // 4 for RGBA (real cameras/scene pipeline), 3 for RGB (gradient_producer)
  pw_stream *stream = nullptr;
  boost::lockfree::spsc_value<IngestFrameSlot, boost::lockfree::allow_multiple_reads<true>> buffer;
  IngestFrameSlot write_scratch;
  GLuint texture = 0;
};

void on_video_input_process(void *data) {
  auto &input = *static_cast<VideoInput *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(input.stream);
  if (pw_buffer == nullptr)
    return;

  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas > 0 && buffer->datas[0].data != nullptr &&
      buffer->datas[0].chunk != nullptr && buffer->datas[0].chunk->size > 0) {
    auto &spa_data = buffer->datas[0];
    const size_t expected =
        static_cast<size_t>(input.width) * input.height * input.bytes_per_pixel;
    const size_t copy_size = std::min<size_t>(
        {expected, spa_data.chunk->size, static_cast<size_t>(spa_data.maxsize), kMaxVideoFrameBytes});
    if (copy_size > 0) {
      std::memcpy(input.write_scratch.data.data(), spa_data.data, copy_size);
      input.write_scratch.has_frame = true;
      input.buffer.write(input.write_scratch);
    }
  }
  pw_stream_queue_buffer(input.stream, pw_buffer);
}

const pw_stream_events kVideoInputEvents = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_video_input_process,
};

// Same connect_video_stream as main.cpp (target_object + AUTOCONNECT +
// node.dont-fallback/node.linger convention - see that file's comment for
// why both target_object and AUTOCONNECT are required together, and why
// dont-fallback/linger matter for a target that isn't up yet at connect
// time). format/bytes_per_pixel are parameterized (not hardcoded RGBA)
// specifically so this same function can validate against gradient_producer
// (an intentionally RGB test tool per its own header comment, distinct from
// the RGBA-everywhere convention real cameras/scenes use) without
// misrepresenting what the real ingestion path will actually request.
pw_stream *connect_video_input(pw_loop *loop, const char *name, void *user_data, uint32_t width,
                                uint32_t height, const std::string &target_object,
                                spa_video_format format) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Capture", PW_KEY_MEDIA_ROLE, "Video",
      PW_KEY_MEDIA_CLASS, "Stream/Input/Video", PW_KEY_NODE_NAME, name, PW_KEY_NODE_DESCRIPTION,
      "Sonic Eddy GPU compositor", nullptr);
  if (!target_object.empty()) {
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT, target_object.c_str());
    pw_properties_set(properties, "node.dont-fallback", "true");
    pw_properties_set(properties, "node.linger", "true");
  }

  auto *stream = pw_stream_new_simple(loop, name, properties, &kVideoInputEvents, user_data);
  if (stream == nullptr)
    return nullptr;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = format,
                                            .size = SPA_RECTANGLE(width, height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};

  auto flags = static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS | PW_STREAM_FLAG_RT_PROCESS);
  if (!target_object.empty())
    flags = static_cast<pw_stream_flags>(flags | PW_STREAM_FLAG_AUTOCONNECT);

  if (pw_stream_connect(stream, PW_DIRECTION_INPUT, PW_ID_ANY, flags, params, 1) < 0) {
    std::fprintf(stderr, "%s: pw_stream_connect failed\n", name);
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

// Real (non-test) video input pool: one VideoInput per video_config::InputDef
// slot, matching main.cpp's app.video_sources.resize(input_count) pattern.
// capacity is reserved up front and never grown afterward - pw_stream_new_
// simple is given each element's address as user_data, which must stay
// stable for the stream's whole lifetime (a std::vector reallocating after
// streams are connected would leave every stream holding a dangling
// pointer - see the earlier real segfault this session traced to exactly
// this class of mistake with a dangling pw_stream_events pointer).
// std::deque, not std::vector: VideoInput holds a boost::lockfree::spsc_value
// member, which is non-copyable/non-movable (as any lock-free primitive
// should be), so std::vector can't compile even with reserve() called first
// (vector's growth strategy requires the type be movable regardless of
// whether reallocation ever actually happens at runtime). deque never moves
// existing elements when growing, so it needs neither copy nor move, and it
// gives the same "address stays valid forever" guarantee this needs anyway.
struct InputPool {
  std::deque<VideoInput> inputs;
};

void build_input_pool_textures(InputPool &pool, const std::vector<video_config::InputDef> &defs) {
  pool.inputs.clear();
  for (const auto &def : defs) {
    pool.inputs.emplace_back();
    auto &input = pool.inputs.back();
    input.width = def.width;
    input.height = def.height;
    input.bytes_per_pixel = 4; // RGBA - the real convention every camera script in this repo uses
    input.texture = make_texture(def.width, def.height, nullptr);
  }
}

bool connect_input_pool(InputPool &pool, pw_loop *loop, const std::vector<video_config::InputDef> &defs,
                        const std::string &node_prefix) {
  for (size_t i = 0; i < pool.inputs.size(); ++i) {
    auto &input = pool.inputs[i];
    const std::string name = node_prefix + ".in" + std::to_string(i);
    input.stream = connect_video_input(loop, name.c_str(), &input, input.width, input.height,
                                       defs[i].target_object, SPA_VIDEO_FORMAT_RGBA);
    if (input.stream == nullptr)
      return false;
  }
  return true;
}

// Call once per render tick, before rendering any scene that references this
// pool - uploads whatever the RT thread most recently published for each
// input into that input's persistent texture (glTexSubImage2D, not a fresh
// glTexImage2D - the texture object itself is created once in
// build_input_pool_textures and reused for the input's whole lifetime).
void sync_input_pool_textures(InputPool &pool) {
  for (auto &input : pool.inputs) {
    IngestFrameSlot latest;
    bool got = false;
    input.buffer.consume([&](const IngestFrameSlot &slot) {
      latest = slot;
      got = true;
    });
    if (got && latest.has_frame) {
      glBindTexture(GL_TEXTURE_2D, input.texture);
      glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, static_cast<GLsizei>(input.width),
                      static_cast<GLsizei>(input.height), GL_RGBA, GL_UNSIGNED_BYTE,
                      latest.data.data());
    }
  }
}

// Real (non-test) scene runtime: unlike render_scene_objects (which reloads
// every image from disk and creates a fresh texture on every single call -
// fine for a one-shot test, wasteful for a continuous real-time loop),
// this loads/uploads each object's texture exactly ONCE at scene-load time
// and reuses it for every subsequent render. Video-type objects don't own a
// texture here at all - they reference an index into a separate InputPool,
// whose textures update independently via sync_input_pool_textures.
// Live-controllable per-object overrides, mutated from a Props param_changed
// callback (see apply_object_params) and read every render tick. Unlike
// main.cpp's RenderSlot::control_config (which needs a lock-free buffer
// because its CPU render loop runs on a genuinely different thread than the
// PW control-plane callbacks), this file's param_changed callbacks and its
// render loop both run on the same thread (both driven by this process's
// one pw_loop_iterate call in run_real_compositor) - plain atomics are
// enough, no cross-thread handoff needed. Seeded from base_params/scene.json
// defaults in build_scene_runtime, then overwritten field-by-field by
// whatever a real "object_params" Props update actually touches. dst_w/
// dst_h and rotate are intentionally NOT here - main.cpp's own
// apply_object_params never allows live resize/rotate either (position +
// visibility + color, matching "translation, hide/show etc.").
struct ObjectControlConfig {
  std::atomic<uint32_t> dst_x{0};
  std::atomic<uint32_t> dst_y{0};
  std::atomic<bool> visible{true};
  std::atomic<float> red_gain{1.0f};
  std::atomic<float> green_gain{1.0f};
  std::atomic<float> blue_gain{1.0f};
  std::atomic<float> opacity{1.0f};
  std::atomic<bool> flip_horizontal{false};
  std::atomic<bool> flip_vertical{false};
};

struct SceneObjectRuntime {
  bool is_image = false;
  GLuint image_texture = 0;      // valid only if is_image
  uint32_t target_input_index = 0; // valid only if !is_image
  ObjectParams base_params;      // dst_x/y/w/h/rotate/flip - canvas_w/h filled in at render time
  ObjectControlConfig control;   // live overrides - see comment above
};

// `objects` is DECLARATION order (matching cfg.objects) - object_params'
// "object" index (from the SonicEddy frontend) refers to this order, same
// as main.cpp's render_slots/paint_order split (see apply_object_params's
// comment). `paint_order` is a separate, z-sorted index list into `objects`
// used only for rendering. name/source_file are for the "scenes" Props
// readback (see publish_scene_params) - source_file is filled in by the
// caller after this returns, since this function doesn't know which CLI
// path it was loaded from.
struct SceneRuntime {
  uint32_t canvas_width = 0;
  uint32_t canvas_height = 0;
  std::string name;
  std::string source_file;
  std::deque<SceneObjectRuntime> objects;   // declaration order, non-movable (atomics)
  std::vector<size_t> paint_order;          // indices into `objects`, z-sorted back-to-front
};

bool build_scene_runtime(SceneRuntime &runtime, const scene::SceneConfig &cfg) {
  runtime.canvas_width = cfg.canvas_width;
  runtime.canvas_height = cfg.canvas_height;
  runtime.name = cfg.name;
  runtime.objects.clear();

  for (const auto &obj : cfg.objects) {
    runtime.objects.emplace_back();
    auto &rt = runtime.objects.back();
    rt.base_params.dst_x = static_cast<float>(obj.x);
    rt.base_params.dst_y = static_cast<float>(obj.y);
    rt.base_params.dst_w = static_cast<float>(obj.width);
    rt.base_params.dst_h = static_cast<float>(obj.height);
    rt.base_params.rotate = obj.rotate;
    rt.base_params.flip_h = obj.flip_horizontal;
    rt.base_params.flip_v = obj.flip_vertical;
    // Seed live control state from scene.json defaults - object_params
    // updates only ever overwrite the specific fields a real command
    // touches (see apply_object_params).
    rt.control.dst_x.store(static_cast<uint32_t>(obj.x));
    rt.control.dst_y.store(static_cast<uint32_t>(obj.y));
    rt.control.flip_horizontal.store(obj.flip_horizontal);
    rt.control.flip_vertical.store(obj.flip_vertical);

    if (obj.type == scene::ObjectType::Image) {
      rt.is_image = true;
      int w = 0, h = 0, channels = 0;
      auto *pixels = stbi_load(obj.image_file.c_str(), &w, &h, &channels, 4);
      if (pixels == nullptr) {
        std::fprintf(stderr, "[scene] failed to load image \"%s\": %s\n", obj.image_file.c_str(),
                     stbi_failure_reason());
        return false;
      }
      rt.image_texture = make_texture(static_cast<uint32_t>(w), static_cast<uint32_t>(h), pixels);
      stbi_image_free(pixels);
    } else {
      rt.is_image = false;
      rt.target_input_index = obj.target_input_index;
    }
  }

  runtime.paint_order.resize(runtime.objects.size());
  for (size_t i = 0; i < runtime.paint_order.size(); ++i)
    runtime.paint_order[i] = i;
  std::stable_sort(runtime.paint_order.begin(), runtime.paint_order.end(),
                   [&](size_t a, size_t b) { return cfg.objects[a].z < cfg.objects[b].z; });
  return true;
}

// Renders into whatever FBO is currently bound - caller owns framebuffer/
// blend state, same convention as draw_object/draw_blend/render_scene_
// objects. Zero disk I/O, zero texture creation - every texture referenced
// here already exists (image textures from build_scene_runtime, video
// textures from the InputPool, kept current by sync_input_pool_textures).
void render_scene_runtime(const SceneRuntime &runtime, const InputPool &pool) {
  for (size_t idx : runtime.paint_order) {
    const auto &obj = runtime.objects[idx];
    if (!obj.control.visible.load(std::memory_order_relaxed))
      continue;
    ObjectParams p = obj.base_params; // dst_w/dst_h/rotate: not live-controllable, see ObjectControlConfig comment
    p.canvas_w = static_cast<float>(runtime.canvas_width);
    p.canvas_h = static_cast<float>(runtime.canvas_height);
    p.dst_x = static_cast<float>(obj.control.dst_x.load(std::memory_order_relaxed));
    p.dst_y = static_cast<float>(obj.control.dst_y.load(std::memory_order_relaxed));
    p.flip_h = obj.control.flip_horizontal.load(std::memory_order_relaxed);
    p.flip_v = obj.control.flip_vertical.load(std::memory_order_relaxed);
    p.red_gain = obj.control.red_gain.load(std::memory_order_relaxed);
    p.green_gain = obj.control.green_gain.load(std::memory_order_relaxed);
    p.blue_gain = obj.control.blue_gain.load(std::memory_order_relaxed);
    p.opacity = obj.control.opacity.load(std::memory_order_relaxed);
    const GLuint tex = obj.is_image
                           ? obj.image_texture
                           : (obj.target_input_index < pool.inputs.size()
                                  ? pool.inputs[obj.target_input_index].texture
                                  : 0);
    if (tex != 0)
      draw_object(tex, p);
  }
}

// Applies a partial per-object update from an "object_params" Props JSON
// blob, e.g. {"object":0,"dst_x":100,"dst_y":50,"visible":false}. Ports
// main.cpp's apply_object_params exactly (same field set, same semantics:
// every field but "object" is optional, unknown/out-of-range object index
// is silently ignored, dst_x/dst_y are clamped against canvas bounds using
// the object's own fixed dst_w/dst_h from scene.json - no live resize).
// Unlike main.cpp, this doesn't need a lock-free config_buffer handoff or a
// "rebuild sample maps" step - see ObjectControlConfig's own comment for
// why plain atomics on this file's single control+render thread suffice.
void apply_object_params(SceneRuntime &scene, uint32_t canvas_width, uint32_t canvas_height,
                         const std::string &json_text) {
  nlohmann::json command;
  try {
    command = nlohmann::json::parse(json_text);
  } catch (const nlohmann::json::exception &) {
    return;
  }
  if (!command.is_object() || !command.contains("object") || !command["object"].is_number_integer())
    return;

  const int object_idx = command["object"].get<int>();
  if (object_idx < 0 || static_cast<size_t>(object_idx) >= scene.objects.size())
    return;
  auto &control = scene.objects[static_cast<size_t>(object_idx)].control;
  const float dst_w = scene.objects[static_cast<size_t>(object_idx)].base_params.dst_w;
  const float dst_h = scene.objects[static_cast<size_t>(object_idx)].base_params.dst_h;

  if (command.contains("dst_x") && command["dst_x"].is_number()) {
    const uint32_t requested = command["dst_x"].get<uint32_t>();
    const uint32_t width = static_cast<uint32_t>(dst_w);
    control.dst_x.store(canvas_width > width ? std::min(requested, canvas_width - width) : 0,
                        std::memory_order_relaxed);
  }
  if (command.contains("dst_y") && command["dst_y"].is_number()) {
    const uint32_t requested = command["dst_y"].get<uint32_t>();
    const uint32_t height = static_cast<uint32_t>(dst_h);
    control.dst_y.store(canvas_height > height ? std::min(requested, canvas_height - height) : 0,
                        std::memory_order_relaxed);
  }
  if (command.contains("visible") && command["visible"].is_boolean())
    control.visible.store(command["visible"].get<bool>(), std::memory_order_relaxed);
  if (command.contains("red_gain") && command["red_gain"].is_number())
    control.red_gain.store(command["red_gain"].get<float>(), std::memory_order_relaxed);
  if (command.contains("green_gain") && command["green_gain"].is_number())
    control.green_gain.store(command["green_gain"].get<float>(), std::memory_order_relaxed);
  if (command.contains("blue_gain") && command["blue_gain"].is_number())
    control.blue_gain.store(command["blue_gain"].get<float>(), std::memory_order_relaxed);
  if (command.contains("opacity") && command["opacity"].is_number())
    control.opacity.store(std::clamp(command["opacity"].get<float>(), 0.0f, 1.0f),
                          std::memory_order_relaxed);
  if (command.contains("flip_horizontal") && command["flip_horizontal"].is_boolean())
    control.flip_horizontal.store(command["flip_horizontal"].get<bool>(), std::memory_order_relaxed);
  if (command.contains("flip_vertical") && command["flip_vertical"].is_boolean())
    control.flip_vertical.store(command["flip_vertical"].get<bool>(), std::memory_order_relaxed);
}

// Publishes the scene list + active index as PipeWire Props on `stream`,
// mirroring main.cpp's publish_scene_params exactly (same "scenes"/
// "active_scene_index" keys, same {"name","file"} JSON shape - the
// SonicEddy frontend's CompositorParamParser/CompositorSceneInfo already
// expect precisely this wire format, see Fr.Sonic/Compositor/). Only used
// for A/B (scene-switchable) - downstream has no scene list to publish.
void publish_scene_params(pw_stream *stream, const std::deque<SceneRuntime> &scenes,
                          int active_scene_index, std::array<uint8_t, 4096> &params_buffer) {
  if (stream == nullptr)
    return;

  nlohmann::json scenes_array = nlohmann::json::array();
  for (const auto &scene : scenes)
    scenes_array.push_back({{"name", scene.name}, {"file", scene.source_file}});
  const std::string scenes_json = scenes_array.dump();

  spa_pod_builder builder{};
  spa_pod_builder_init(&builder, params_buffer.data(), params_buffer.size());

  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Props, SPA_PARAM_Props);
  spa_pod_builder_prop(&builder, SPA_PROP_params, 0);

  spa_pod_frame struct_frame{};
  spa_pod_builder_push_struct(&builder, &struct_frame);
  spa_pod_builder_string(&builder, "active_scene_index");
  spa_pod_builder_int(&builder, active_scene_index);
  spa_pod_builder_string(&builder, "scenes");
  spa_pod_builder_string(&builder, scenes_json.c_str());
  spa_pod_builder_pop(&builder, &struct_frame);

  const spa_pod *params[] = {static_cast<spa_pod *>(spa_pod_builder_pop(&builder, &object_frame))};
  pw_stream_update_params(stream, params, 1);
}

// Walks a SPA_TYPE_OBJECT_Props param's SPA_PROP_params struct, invoking
// fn(key, value_pod) once per (string key, value) pair - shared by every
// Props consumer in this file (A/B scene control, blend control, downstream
// object_params) so this walking logic (even index = key string, odd index
// = value pod) is written once. Mirrors main.cpp's handle_output_props /
// video_blender.cpp's own Props walker exactly.
template <typename Fn> void for_each_props_kv(const spa_pod *param, Fn &&fn) {
  const auto *params_prop = spa_pod_find_prop(param, nullptr, SPA_PROP_params);
  if (params_prop == nullptr || params_prop->value.type != SPA_TYPE_Struct)
    return;
  const char *key = nullptr;
  uint32_t index = 0;
  spa_pod *child = nullptr;
  SPA_POD_FOREACH(static_cast<spa_pod *>(SPA_POD_BODY(&params_prop->value)),
                  SPA_POD_BODY_SIZE(&params_prop->value), child) {
    if (index % 2 == 0) {
      key = nullptr;
      if (child->type == SPA_TYPE_String)
        spa_pod_get_string(child, &key);
    } else if (key != nullptr) {
      fn(key, child);
    }
    ++index;
  }
}

// Test 5: live video ingestion. Connects one real PipeWire input stream
// (named target_node, targeting target_object) and blits whatever frames
// arrive as a full-canvas object - no scene JSON involved, isolating "does
// the RT-thread-to-GL-texture handoff actually work with a live producer"
// from scene-loading/paint-order (already proven in test 4). Runs the
// PipeWire loop, uploading + re-rendering on every dispatch, for up to
// max_wait_seconds waiting for at least one real frame, then reads back and
// reports what it received - caller decides pass/fail against whatever
// producer it started (this function doesn't know what content to expect).
std::array<uint8_t, 4> test_video_ingestion(const std::string &node_name,
                                            const std::string &target_object, uint32_t width,
                                            uint32_t height, double max_wait_seconds,
                                            uint32_t sample_x, uint32_t sample_y,
                                            spa_video_format format, uint32_t bytes_per_pixel) {
  static VideoInput input; // static: kMaxVideoFrameBytes-sized array, too big for a stack frame
  input.width = width;
  input.height = height;
  input.bytes_per_pixel = bytes_per_pixel;

  pw_init(nullptr, nullptr);
  pw_main_loop *loop = pw_main_loop_new(nullptr);
  pw_loop *pw_loop_ptr = pw_main_loop_get_loop(loop);

  input.stream =
      connect_video_input(pw_loop_ptr, node_name.c_str(), &input, width, height, target_object, format);
  if (input.stream == nullptr) {
    pw_main_loop_destroy(loop);
    return {0, 0, 0, 0};
  }

  input.texture = make_texture(width, height, nullptr);

  GLuint fbo = 0, color_tex = 0;
  glGenFramebuffers(1, &fbo);
  glBindFramebuffer(GL_FRAMEBUFFER, fbo);
  glGenTextures(1, &color_tex);
  glBindTexture(GL_TEXTURE_2D, color_tex);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, color_tex, 0);
  glViewport(0, 0, static_cast<GLsizei>(width), static_cast<GLsizei>(height));
  glDisable(GL_BLEND);

  IngestFrameSlot latest;
  bool got_frame = false;
  const auto deadline = std::chrono::steady_clock::now() +
                        std::chrono::duration<double>(max_wait_seconds);
  while (std::chrono::steady_clock::now() < deadline) {
    pw_loop_iterate(pw_loop_ptr, 50);
    input.buffer.consume([&](const IngestFrameSlot &slot) { latest = slot; });
    if (latest.has_frame) {
      got_frame = true;
      break;
    }
  }

  std::array<uint8_t, 4> result = {0, 0, 0, 0};
  if (got_frame) {
    // GL has no tightly-packed 3-byte RGB texture upload path that matches
    // SPA's RGB layout cleanly (row alignment/format mismatches), so expand
    // to RGBA here for the (test-only) RGB case. Real cameras/scene sources
    // are RGBA already (bytes_per_pixel==4), so this is a no-op copy for
    // the actual production path.
    std::vector<uint8_t> upload_buf;
    const uint8_t *upload_src = latest.data.data();
    if (input.bytes_per_pixel == 3) {
      upload_buf.resize(static_cast<size_t>(width) * height * 4);
      const uint8_t *src = latest.data.data();
      for (size_t px = 0; px < static_cast<size_t>(width) * height; ++px) {
        upload_buf[px * 4 + 0] = src[px * 3 + 0];
        upload_buf[px * 4 + 1] = src[px * 3 + 1];
        upload_buf[px * 4 + 2] = src[px * 3 + 2];
        upload_buf[px * 4 + 3] = 255;
      }
      upload_src = upload_buf.data();
    }
    glBindTexture(GL_TEXTURE_2D, input.texture);
    glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, static_cast<GLsizei>(width), static_cast<GLsizei>(height),
                     GL_RGBA, GL_UNSIGNED_BYTE, upload_src);

    glClearColor(0, 0, 0, 1);
    glClear(GL_COLOR_BUFFER_BIT);
    ObjectParams p;
    p.canvas_w = static_cast<float>(width);
    p.canvas_h = static_cast<float>(height);
    p.dst_w = static_cast<float>(width);
    p.dst_h = static_cast<float>(height);
    draw_object(input.texture, p);
    glFinish();

    std::vector<uint8_t> pixels(static_cast<size_t>(width) * height * 4);
    glReadPixels(0, 0, static_cast<GLsizei>(width), static_cast<GLsizei>(height), GL_RGBA,
                 GL_UNSIGNED_BYTE, pixels.data());
    const uint32_t buffer_row = height - 1 - sample_y;
    const size_t idx = (static_cast<size_t>(buffer_row) * width + sample_x) * 4;
    result = {pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]};
  } else {
    std::fprintf(stderr, "[ingest] no frame received within %.1fs\n", max_wait_seconds);
  }

  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);
  glDeleteTextures(1, &input.texture);
  pw_stream_destroy(input.stream);
  pw_main_loop_destroy(loop);
  pw_deinit();
  return result;
}

// Test 4: real scene-file loading (scene::load_scene, the same JSON loader
// main.cpp uses) + paint-order compositing. Two overlapping image objects,
// bottom (z=1, solid red, full canvas) and top (z=2, solid blue, covers
// only the top-left quadrant) - a correct paint-order implementation must
// show blue where they overlap (top drawn last/on top) and red everywhere
// else. Video-type objects aren't exercised here (no live PipeWire source
// wired up yet - that's a separate, later task) - this test only has image
// objects, deliberately, to isolate "does scene loading + paint order work"
// from "does live video ingestion work".
// Shared paint-order render loop - used both for the regular scene test and
// the downstream-compositor test (task #4), since downstream_scene.hpp's
// SceneObject/SceneConfig schema is byte-for-byte identical to scene.hpp's
// (confirmed by diff - only the namespace and a couple of comments differ,
// baseline is never a scene object in either). Draws into whatever FBO is
// currently bound; caller owns framebuffer setup/blend state, same
// convention as draw_object/draw_blend. Appends every texture it creates to
// loaded_textures so the caller can clean them up after readback.
bool render_scene_objects(const scene::SceneConfig &cfg, std::vector<GLuint> &loaded_textures) {
  std::vector<size_t> paint_order(cfg.objects.size());
  for (size_t i = 0; i < paint_order.size(); ++i)
    paint_order[i] = i;
  std::stable_sort(paint_order.begin(), paint_order.end(),
                   [&](size_t a, size_t b) { return cfg.objects[a].z < cfg.objects[b].z; });

  for (size_t idx : paint_order) {
    const auto &obj = cfg.objects[idx];
    GLuint tex = 0;
    if (obj.type == scene::ObjectType::Image) {
      int w = 0, h = 0, channels = 0;
      auto *pixels = stbi_load(obj.image_file.c_str(), &w, &h, &channels, 4);
      if (pixels == nullptr) {
        std::fprintf(stderr, "[scene] failed to load image \"%s\": %s\n", obj.image_file.c_str(),
                     stbi_failure_reason());
        return false;
      }
      tex = make_texture(static_cast<uint32_t>(w), static_cast<uint32_t>(h), pixels);
      stbi_image_free(pixels);
    } else {
      // Video source, no live PipeWire ingestion wired into scene loading
      // yet - placeholder black texture, matching this codebase's existing
      // "no frame yet = treated as black" convention.
      const uint8_t black[4] = {0, 0, 0, 255};
      tex = make_texture(1, 1, black);
    }
    loaded_textures.push_back(tex);

    ObjectParams p;
    p.canvas_w = static_cast<float>(cfg.canvas_width);
    p.canvas_h = static_cast<float>(cfg.canvas_height);
    p.dst_x = static_cast<float>(obj.x);
    p.dst_y = static_cast<float>(obj.y);
    p.dst_w = static_cast<float>(obj.width);
    p.dst_h = static_cast<float>(obj.height);
    p.rotate = obj.rotate;
    p.flip_h = obj.flip_horizontal;
    p.flip_v = obj.flip_vertical;
    draw_object(tex, p);
  }
  return true;
}

bool test_scene_paint_order(const std::string &scene_path) {
  auto cfg = scene::load_scene(scene_path);
  if (!cfg.has_value()) {
    std::fprintf(stderr, "[scene] load_scene failed for %s\n", scene_path.c_str());
    return false;
  }

  GLuint fbo = 0, color_tex = 0;
  glGenFramebuffers(1, &fbo);
  glBindFramebuffer(GL_FRAMEBUFFER, fbo);
  glGenTextures(1, &color_tex);
  glBindTexture(GL_TEXTURE_2D, color_tex);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, cfg->canvas_width, cfg->canvas_height, 0, GL_RGBA,
               GL_UNSIGNED_BYTE, nullptr);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, color_tex, 0);
  if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
    std::fprintf(stderr, "[scene] FBO incomplete\n");
    return false;
  }

  glViewport(0, 0, static_cast<GLsizei>(cfg->canvas_width), static_cast<GLsizei>(cfg->canvas_height));
  glClearColor(0, 0, 0, 1);
  glClear(GL_COLOR_BUFFER_BIT);
  glEnable(GL_BLEND);
  glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

  std::vector<GLuint> loaded_textures;
  if (!render_scene_objects(*cfg, loaded_textures)) {
    for (GLuint tex : loaded_textures)
      glDeleteTextures(1, &tex);
    glDeleteFramebuffers(1, &fbo);
    glDeleteTextures(1, &color_tex);
    return false;
  }
  glFinish();

  std::vector<uint8_t> pixels(static_cast<size_t>(cfg->canvas_width) * cfg->canvas_height * 4);
  glReadPixels(0, 0, static_cast<GLsizei>(cfg->canvas_width), static_cast<GLsizei>(cfg->canvas_height),
               GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());

  auto sample = [&](uint32_t x, uint32_t image_row) {
    const uint32_t buffer_row = cfg->canvas_height - 1 - image_row;
    const size_t idx = (static_cast<size_t>(buffer_row) * cfg->canvas_width + x) * 4;
    return std::array<uint8_t, 4>{pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]};
  };

  const auto overlap = sample(8, 8);   // inside both - should be blue (top wins)
  const auto red_only = sample(48, 48); // bottom-right - only the red object covers this

  std::printf("[scene] overlap=(%d,%d,%d) expect blue; red_only=(%d,%d,%d) expect red\n",
              overlap[0], overlap[1], overlap[2], red_only[0], red_only[1], red_only[2]);

  for (GLuint tex : loaded_textures)
    glDeleteTextures(1, &tex);
  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);

  return close_enough(overlap[0], 0) && close_enough(overlap[1], 0) && close_enough(overlap[2], 255) &&
         close_enough(red_only[0], 255) && close_enough(red_only[1], 0) && close_enough(red_only[2], 0);
}

// Test: downstream compositing stage (task #4). downstream_scene.hpp's
// SceneConfig is the same schema as scene.hpp's (confirmed by diff), and
// the baseline is never a scene object in either - it's the previous
// stage's output (tex_blend in the real pipeline), drawn first as a plain
// full-canvas quad, then the downstream scene's own overlay objects paint
// on top via the same render_scene_objects used for scenes A/B. Baseline:
// solid green, full canvas. Overlay: one blue object covering the top-left
// quadrant only. A correct implementation shows green where the overlay
// doesn't reach (proving the baseline actually composites through, not
// just "overlay renders in isolation") and blue where it does.
bool test_downstream_compositing(const std::string &overlay_scene_path) {
  auto cfg = scene::load_scene(overlay_scene_path);
  if (!cfg.has_value()) {
    std::fprintf(stderr, "[downstream] load_scene failed for %s\n", overlay_scene_path.c_str());
    return false;
  }

  const uint8_t baseline_color[4] = {0, 200, 0, 255};
  GLuint baseline_tex = make_texture(1, 1, baseline_color);

  GLuint fbo = 0, color_tex = 0;
  glGenFramebuffers(1, &fbo);
  glBindFramebuffer(GL_FRAMEBUFFER, fbo);
  glGenTextures(1, &color_tex);
  glBindTexture(GL_TEXTURE_2D, color_tex);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, cfg->canvas_width, cfg->canvas_height, 0, GL_RGBA,
               GL_UNSIGNED_BYTE, nullptr);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, color_tex, 0);
  if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
    std::fprintf(stderr, "[downstream] FBO incomplete\n");
    return false;
  }

  glViewport(0, 0, static_cast<GLsizei>(cfg->canvas_width), static_cast<GLsizei>(cfg->canvas_height));
  glDisable(GL_BLEND); // baseline is an opaque full-canvas replace, not a blend
  ObjectParams baseline_params;
  baseline_params.canvas_w = static_cast<float>(cfg->canvas_width);
  baseline_params.canvas_h = static_cast<float>(cfg->canvas_height);
  baseline_params.dst_w = static_cast<float>(cfg->canvas_width);
  baseline_params.dst_h = static_cast<float>(cfg->canvas_height);
  draw_object(baseline_tex, baseline_params);

  glEnable(GL_BLEND);
  glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
  std::vector<GLuint> loaded_textures;
  const bool render_ok = render_scene_objects(*cfg, loaded_textures);
  glFinish();

  std::array<uint8_t, 4> baseline_area = {0, 0, 0, 0}, overlay_area = {0, 0, 0, 0};
  if (render_ok) {
    std::vector<uint8_t> pixels(static_cast<size_t>(cfg->canvas_width) * cfg->canvas_height * 4);
    glReadPixels(0, 0, static_cast<GLsizei>(cfg->canvas_width), static_cast<GLsizei>(cfg->canvas_height),
                 GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
    auto sample = [&](uint32_t x, uint32_t image_row) {
      const uint32_t buffer_row = cfg->canvas_height - 1 - image_row;
      const size_t idx = (static_cast<size_t>(buffer_row) * cfg->canvas_width + x) * 4;
      return std::array<uint8_t, 4>{pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]};
    };
    overlay_area = sample(8, 8);   // inside the blue overlay
    baseline_area = sample(48, 48); // outside the overlay - baseline should show through
    std::printf("[downstream] baseline_area=(%d,%d,%d) expect green; overlay_area=(%d,%d,%d) expect blue\n",
                baseline_area[0], baseline_area[1], baseline_area[2], overlay_area[0], overlay_area[1],
                overlay_area[2]);
  }

  for (GLuint tex : loaded_textures)
    glDeleteTextures(1, &tex);
  glDeleteTextures(1, &baseline_tex);
  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);

  return render_ok && close_enough(baseline_area[0], 0) && close_enough(baseline_area[1], 200) &&
         close_enough(baseline_area[2], 0) && close_enough(overlay_area[0], 0) &&
         close_enough(overlay_area[1], 0) && close_enough(overlay_area[2], 255);
}

// Audio-driven internal triggering (task #5). The real audio PipeWire
// stream is this process's clock - its RT callback does only an atomic
// increment, never touches GL. This replaces the old cross-process
// pw_stream_trigger_process()/pending_pts FIFO dance entirely - it existed
// only because compositing and recording used to be separate processes
// with no driver of their own; once it's one process, the audio callback
// just increments a counter that the render loop polls.
//
// This test polls the counter directly after each pw_loop_iterate() call,
// on the same thread - single-threaded, no separate GPU worker thread wired
// up yet in this test (that's a real production-architecture piece still
// to build, not implemented here). What this proves: the counter genuinely
// advances from a real external audio clock event, and a render triggered
// by that advance produces correct output - the tick->render *wiring*, not
// yet the full RT-thread/worker-thread separation the production version
// will need once actual GL work is heavy enough to matter (it isn't, for
// one draw_blend call).
struct AudioClock {
  pw_stream *stream = nullptr;
  std::atomic<uint64_t> tick_count{0};
};

void on_audio_clock_process(void *data) {
  auto &clock = *static_cast<AudioClock *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(clock.stream);
  if (pw_buffer == nullptr)
    return;
  clock.tick_count.fetch_add(1, std::memory_order_relaxed);
  pw_stream_queue_buffer(clock.stream, pw_buffer);
}

const pw_stream_events kAudioClockEvents = {
    .version = PW_VERSION_STREAM_EVENTS,
    .process = on_audio_clock_process,
};

pw_stream *connect_audio_clock(pw_loop *loop, void *user_data, const std::string &target_object,
                                uint32_t rate, uint32_t channels) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Audio", PW_KEY_MEDIA_CATEGORY, "Capture", PW_KEY_MEDIA_ROLE, "Production",
      PW_KEY_MEDIA_CLASS, "Stream/Input/Audio", PW_KEY_NODE_NAME, "se.gpu-compositor.audio-clock",
      nullptr);
  // Same convention as connect_video_input: only set target_object (plus
  // AUTOCONNECT/dont-fallback/linger) when a target was actually given.
  // target_object must name an ordinary Audio/Source, never a Sink -
  // capturing a Sink's monitor isn't real signal flow and needs a
  // WirePlumber policy exception to even link (found the hard way
  // 2026-09-09: target_object="Master Out", a Sink, left this stream
  // permanently "suspended", tick_count never advanced past 0, and the
  // whole render loop never ran once - looked like a rendering bug but was
  // actually zero process() calls ever happening). The real source is one
  // of the already-running loopback modules' *playback* side (see
  // master-out-split.conf: Master Out's combine-stream already fans out
  // into loopback_obs_sink/loopback_audio_sharing_sink, and each loopback
  // module bridges that internally to an ordinary Audio/Source -
  // loopback_obs_source / loopback_audio_sharing_source) - capturing from a
  // genuine Source is completely ordinary signal flow, the same shape as
  // gradient_producer -> se.gpu-compositor.a.in0.
  if (!target_object.empty()) {
    pw_properties_set(properties, PW_KEY_TARGET_OBJECT, target_object.c_str());
    pw_properties_set(properties, "node.dont-fallback", "true");
    pw_properties_set(properties, "node.linger", "true");
  }
  auto *stream = pw_stream_new_simple(loop, "se.gpu-compositor.audio-clock", properties,
                                      &kAudioClockEvents, user_data);
  if (stream == nullptr)
    return nullptr;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  spa_audio_info_raw audio_info{};
  audio_info.format = SPA_AUDIO_FORMAT_F32;
  audio_info.rate = rate;
  audio_info.channels = channels;
  const spa_pod *params[] = {spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &audio_info)};

  auto flags = static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS | PW_STREAM_FLAG_RT_PROCESS);
  if (!target_object.empty())
    flags = static_cast<pw_stream_flags>(flags | PW_STREAM_FLAG_AUTOCONNECT);
  if (pw_stream_connect(stream, PW_DIRECTION_INPUT, PW_ID_ANY, flags, params, 1) < 0) {
    std::fprintf(stderr, "audio clock: pw_stream_connect failed\n");
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

// Test: connects a real audio clock stream, waits for actual ticks to
// arrive (real hardware/silence_producer-driven, not a fake timer), and on
// each tick runs one full render (the same draw_blend call already proven
// correct in test_blend_stage) - proving a real external PipeWire audio
// event can now directly drive this process's internal GL render chain
// with no cross-process trigger_process/pending_pts machinery at all. Pass
// criteria: at least min_ticks real ticks arrive within max_wait_seconds
// (proves the real clock is actually driving this, not that it merely
// connected), AND the render triggered by those ticks produces the correct
// blended output (proves the tick->render wiring itself, not just that
// ticks are being counted).
bool test_audio_driven_render(const std::string &target_object, uint32_t rate, uint32_t channels,
                              double max_wait_seconds, uint64_t min_ticks) {
  static AudioClock clock; // static: consistent with this file's other PW-backed test globals

  pw_init(nullptr, nullptr);
  pw_main_loop *loop = pw_main_loop_new(nullptr);
  pw_loop *pw_loop_ptr = pw_main_loop_get_loop(loop);

  clock.stream = connect_audio_clock(pw_loop_ptr, &clock, target_object, rate, channels);
  if (clock.stream == nullptr) {
    pw_main_loop_destroy(loop);
    return false;
  }

  const uint8_t color_a[4] = {200, 50, 25, 100};
  const uint8_t color_b[4] = {10, 220, 90, 200};
  GLuint tex_a = make_texture(1, 1, color_a);
  GLuint tex_b = make_texture(1, 1, color_b);

  GLuint fbo = 0, color_tex = 0;
  setup_fbo(fbo, color_tex);
  glViewport(0, 0, kCanvasW, kCanvasH);
  glDisable(GL_BLEND);

  uint64_t last_seen_tick = 0;
  uint64_t render_count = 0;
  const float t = 0.3f;
  const auto deadline =
      std::chrono::steady_clock::now() + std::chrono::duration<double>(max_wait_seconds);
  while (std::chrono::steady_clock::now() < deadline) {
    pw_loop_iterate(pw_loop_ptr, 50);
    const uint64_t current_tick = clock.tick_count.load(std::memory_order_relaxed);
    if (current_tick != last_seen_tick) {
      last_seen_tick = current_tick;
      draw_blend(tex_a, tex_b, t); // the actual internal render chain, triggered by the real audio tick
      ++render_count;
    }
    if (last_seen_tick >= min_ticks)
      break;
  }
  glFinish();

  std::printf("[audio-clock] ticks observed=%llu renders=%llu (need >= %llu within %.1fs)\n",
              static_cast<unsigned long long>(last_seen_tick),
              static_cast<unsigned long long>(render_count),
              static_cast<unsigned long long>(min_ticks), max_wait_seconds);

  std::array<uint8_t, kCanvasW * kCanvasH * 4> pixels{};
  glReadPixels(0, 0, kCanvasW, kCanvasH, GL_RGBA, GL_UNSIGNED_BYTE, pixels.data());
  const auto got = pixel_at(pixels, kCanvasW / 2, kCanvasH / 2);
  uint8_t expected[4];
  for (int c = 0; c < 4; ++c)
    expected[c] =
        static_cast<uint8_t>(std::clamp(color_a[c] * (1.0f - t) + color_b[c] * t, 0.0f, 255.0f));
  std::printf("[audio-clock] render output got (%d,%d,%d,%d) expected (%d,%d,%d,%d)\n", got[0], got[1],
              got[2], got[3], expected[0], expected[1], expected[2], expected[3]);

  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);
  glDeleteTextures(1, &tex_a);
  glDeleteTextures(1, &tex_b);
  pw_stream_destroy(clock.stream);
  pw_main_loop_destroy(loop);
  pw_deinit();

  return last_seen_tick >= min_ticks && close_enough(got[0], expected[0]) &&
         close_enough(got[1], expected[1]) && close_enough(got[2], expected[2]) &&
         close_enough(got[3], expected[3]);
}

// Task #7: dmabuf camera ingestion, proof of concept. Deliberately hardcodes
// the DRM format modifier (0x0200000000082305) rather than reading it from
// PipeWire's negotiated SPA_FORMAT_VIDEO_modifier - confirmed via gst-launch
// 2026-09-09 that this exact GPU/driver only ever produces this one modifier
// for AR24/DMA_DRM output (forcing modifier 0x0/linear was rejected outright
// at caps negotiation) - since we know the one value this hardware will ever
// use, there is nothing to negotiate dynamically to prove the import
// mechanism works. A production version targeting unknown/multiple GPUs
// would need to read the real value instead.
//
// Also deliberately holds the one buffer it imports forever (never calls
// pw_stream_queue_buffer on it) rather than implementing real GPU-fence-
// based release timing - a real production consumer cannot safely hand a
// dmabuf-backed buffer back to PipeWire until the GPU has actually finished
// reading it (see the 2026-09-09 design conversation on buffer lifetime),
// but proving the import itself produces correct pixels doesn't need that
// - it needs exactly one buffer, used once, never recycled.
constexpr uint64_t kKnownDrmModifier = 0x0200000000082305ULL;

struct DmabufFrame {
  int fd = -1;
  int32_t offset = 0;
  int32_t stride = 0;
  uint32_t width = 0;
  uint32_t height = 0;
};

struct DmabufInput {
  pw_stream *stream = nullptr;
  uint32_t width = 0;
  uint32_t height = 0;
  std::atomic<bool> got_frame{false};
  std::atomic<uint32_t> last_seen_type{0}; // diagnostic only - what SPA_DATA_* type actually arrived
  DmabufFrame frame; // written once by the RT callback, read once by the test loop after got_frame is true
};

void on_dmabuf_input_process(void *data) {
  auto &input = *static_cast<DmabufInput *>(data);
  if (input.got_frame.load(std::memory_order_acquire))
    return; // already captured our one frame - let everything else flow through untouched
  auto *pw_buffer = pw_stream_dequeue_buffer(input.stream);
  if (pw_buffer == nullptr)
    return;
  auto *buffer = pw_buffer->buffer;
  // Only DmaBuf actually has a usable fd for EGL import - MemPtr (also
  // accepted in on_dmabuf_param_changed's negotiation, matching PipeWire's
  // own reference example) gives a real CPU pointer via .data instead, and
  // its .fd is not meaningful. If negotiation picks MemPtr over DmaBuf,
  // that's real information (means this specific producer/consumer pairing
  // doesn't actually end up exercising the dmabuf path at all) - surfaced
  // via the else branch below rather than silently misreading a pointer as
  // an fd.
  if (buffer->n_datas > 0 && buffer->datas[0].type == SPA_DATA_DmaBuf &&
      buffer->datas[0].fd >= 0) {
    input.frame.fd = static_cast<int>(buffer->datas[0].fd);
    input.frame.offset = static_cast<int32_t>(buffer->datas[0].chunk->offset);
    input.frame.stride = buffer->datas[0].chunk->stride;
    input.got_frame.store(true, std::memory_order_release);
    return; // do NOT requeue - we're keeping this buffer's fd alive indefinitely, see header comment
  }
  // Record whatever type actually arrived (even if not DmaBuf) so the test
  // loop can report it - a plain atomic store, not I/O, keeping this RT
  // callback RT-safe.
  if (buffer->n_datas > 0)
    input.last_seen_type.store(buffer->datas[0].type, std::memory_order_relaxed);
  pw_stream_queue_buffer(input.stream, pw_buffer);
}

// Declares SPA_DATA_DmaBuf as an acceptable buffer type - without this, a
// PipeWire consumer only ever gets offered SPA_DATA_MemPtr (plain mapped
// memory), regardless of what the producer is capable of. This is the one
// piece a normal (non-dmabuf) consumer in this codebase never needed.
void on_dmabuf_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &input = *static_cast<DmabufInput *>(data);
  if (param == nullptr || id != SPA_PARAM_Format)
    return;
  // PipeWire's own pw_buffers_negotiate()/param_filter() (impl-link.c,
  // buffers.c) intersects both sides' full ParamBuffers object - declaring
  // only dataType (tried 2026-09-09) produced an object too sparse to
  // intersect against at all ("error alloc buffers: Invalid argument", from
  // param_filter finding zero overlap). An exact naive linear size/stride
  // guess (width*4) also failed the same way, despite the format itself
  // (confirmed via spa_debug_pod above) negotiating correctly including the
  // real modifier - because a tiled surface's true memory footprint is
  // padded to some driver-internal alignment we have no way to compute from
  // width/height alone. Declaring a generous RANGE instead of one exact
  // guess lets the intersection succeed against whatever the real (unknown
  // to us) tiled footprint actually is, rather than requiring an exact
  // match to a guess that's probably wrong.
  const uint32_t width_bytes = input.width * 4;
  const uint32_t linear_size = width_bytes * input.height;
  std::array<uint8_t, 512> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  const spa_pod *params[] = {static_cast<const spa_pod *>(spa_pod_builder_add_object(
      &builder, SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers, SPA_PARAM_BUFFERS_buffers,
      SPA_POD_CHOICE_RANGE_Int(4, 2, 8), SPA_PARAM_BUFFERS_blocks, SPA_POD_Int(1),
      SPA_PARAM_BUFFERS_size, SPA_POD_CHOICE_RANGE_Int(linear_size, linear_size, linear_size * 4),
      SPA_PARAM_BUFFERS_stride, SPA_POD_CHOICE_RANGE_Int(width_bytes, width_bytes, width_bytes * 4),
      // MemPtr | DmaBuf, matching PipeWire's own reference dmabuf-consumer
      // example (video-play-fixate.c) exactly - NOT MemFd. Confirmed via
      // the daemon's own journal log that pipewiresink's advertised
      // capability here technically includes MemFd (value 4), and
      // accepting it did fix the earlier "error alloc buffers" negotiation
      // mismatch - but MemFd then crashed inside GStreamer's own
      // pipewiresink when handing off a real buffer (null-pointer
      // assertions, 2026-09-09). MemFd-for-a-dmabuf-sourced-stream is a
      // combination the reference example never exercises at all (it only
      // ever asks for MemPtr/DmaBuf), consistent with it being a rare,
      // lightly-tested path in GStreamer's own code rather than something
      // wrong on our end.
      SPA_PARAM_BUFFERS_dataType,
      SPA_POD_CHOICE_FLAGS_Int((1 << SPA_DATA_MemPtr) | (1 << SPA_DATA_DmaBuf))))};
  pw_stream_update_params(input.stream, params, 1);
}

// static storage duration, not a local - pw_stream_new_simple stores this
// pointer for the stream's entire lifetime, not a copy. A local variable
// here would dangle the moment this function returns (found via a real
// segfault 2026-09-09 - the first version of this function used a local
// `events`, which is exactly this bug).
const pw_stream_events kDmabufInputEvents = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_dmabuf_param_changed,
    .process = on_dmabuf_input_process,
};

pw_stream *connect_dmabuf_input(pw_loop *loop, void *user_data, const std::string &target_object,
                                uint32_t width, uint32_t height) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Capture", PW_KEY_MEDIA_ROLE, "Video",
      PW_KEY_MEDIA_CLASS, "Stream/Input/Video", PW_KEY_NODE_NAME, "se.gpu-compositor.dmabuf-test",
      PW_KEY_TARGET_OBJECT, target_object.c_str(), "node.dont-fallback", "true", "node.linger", "true",
      nullptr);
  auto *stream = pw_stream_new_simple(loop, "se.gpu-compositor.dmabuf-test", properties,
                                      &kDmabufInputEvents, user_data);
  if (stream == nullptr)
    return nullptr;

  // Built manually (not via spa_format_video_raw_build, which has no way to
  // add a modifier property) - one single mandatory modifier value, not an
  // enumerated list of candidates. Omitting this property entirely does NOT
  // mean "any modifier is fine" - it specifically means "assume the plain
  // unmodified/legacy layout", which this hardware can't produce (confirmed
  // empirically: negotiation fails with "no more input formats" without
  // this). Hardcoded to kKnownDrmModifier rather than dynamically
  // discovered/enumerated, since this GPU only ever produces this one real
  // value for this format (see kKnownDrmModifier's own comment) - no
  // flexible negotiation needed for what's already a known constant.
  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  spa_pod_frame object_frame{};
  spa_pod_builder_push_object(&builder, &object_frame, SPA_TYPE_OBJECT_Format, SPA_PARAM_EnumFormat);
  spa_pod_builder_add(&builder, SPA_FORMAT_mediaType, SPA_POD_Id(SPA_MEDIA_TYPE_video), 0);
  spa_pod_builder_add(&builder, SPA_FORMAT_mediaSubtype, SPA_POD_Id(SPA_MEDIA_SUBTYPE_raw), 0);
  spa_pod_builder_add(&builder, SPA_FORMAT_VIDEO_format, SPA_POD_Id(SPA_VIDEO_FORMAT_BGRA), 0);
  spa_pod_builder_prop(&builder, SPA_FORMAT_VIDEO_modifier, SPA_POD_PROP_FLAG_MANDATORY);
  spa_pod_builder_long(&builder, static_cast<int64_t>(kKnownDrmModifier));
  spa_rectangle rect_default = SPA_RECTANGLE(width, height);
  spa_rectangle rect_min = SPA_RECTANGLE(1, 1);
  spa_rectangle rect_max = SPA_RECTANGLE(8192, 8192);
  spa_pod_builder_add(
      &builder, SPA_FORMAT_VIDEO_size,
      SPA_POD_CHOICE_RANGE_Rectangle(&rect_default, &rect_min, &rect_max), 0);
  spa_fraction rate_default = SPA_FRACTION(0, 1);
  spa_fraction rate_min = SPA_FRACTION(0, 1);
  spa_fraction rate_max = SPA_FRACTION(1000, 1);
  spa_pod_builder_add(
      &builder, SPA_FORMAT_VIDEO_framerate,
      SPA_POD_CHOICE_RANGE_Fraction(&rate_default, &rate_min, &rate_max), 0);
  const spa_pod *params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_pop(&builder, &object_frame))};
  const auto flags = static_cast<pw_stream_flags>(PW_STREAM_FLAG_AUTOCONNECT | PW_STREAM_FLAG_RT_PROCESS);
  if (pw_stream_connect(stream, PW_DIRECTION_INPUT, PW_ID_ANY, flags, params, 1) < 0) {
    std::fprintf(stderr, "dmabuf input: pw_stream_connect failed\n");
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

// Note: PW_STREAM_FLAG_MAP_BUFFERS is deliberately omitted here (unlike
// every other consumer in this file) - mapping is what you want for
// SPA_DATA_MemPtr buffers you're going to memcpy from on the CPU; for
// SPA_DATA_DmaBuf we want the raw fd itself, not a CPU mapping of it.

std::array<uint8_t, 4> test_dmabuf_ingestion(const std::string &target_object, uint32_t width,
                                             uint32_t height, double max_wait_seconds,
                                             uint32_t sample_x, uint32_t sample_y) {
  static DmabufInput input;

  pw_init(nullptr, nullptr);
  pw_main_loop *loop = pw_main_loop_new(nullptr);
  pw_loop *pw_loop_ptr = pw_main_loop_get_loop(loop);

  input.width = width;
  input.height = height;
  input.stream = connect_dmabuf_input(pw_loop_ptr, &input, target_object, width, height);
  if (input.stream == nullptr) {
    pw_main_loop_destroy(loop);
    return {0, 0, 0, 0};
  }

  const auto deadline =
      std::chrono::steady_clock::now() + std::chrono::duration<double>(max_wait_seconds);
  while (std::chrono::steady_clock::now() < deadline) {
    pw_loop_iterate(pw_loop_ptr, 50);
    if (input.got_frame.load(std::memory_order_acquire))
      break;
  }
  if (!input.got_frame.load(std::memory_order_acquire)) {
    std::fprintf(stderr,
                "[dmabuf] no frame received within %.1fs (last seen SPA_DATA type: %u - 0=none, "
                "1=MemPtr, 2=MemFd, 3=DmaBuf)\n",
                max_wait_seconds, input.last_seen_type.load(std::memory_order_relaxed));
    pw_main_loop_destroy(loop);
    return {0, 0, 0, 0};
  }

  auto egl_create_image =
      reinterpret_cast<PFNEGLCREATEIMAGEKHRPROC>(eglGetProcAddress("eglCreateImageKHR"));
  auto egl_destroy_image =
      reinterpret_cast<PFNEGLDESTROYIMAGEKHRPROC>(eglGetProcAddress("eglDestroyImageKHR"));
  auto gl_egl_image_target_texture = reinterpret_cast<PFNGLEGLIMAGETARGETTEXTURE2DOESPROC>(
      eglGetProcAddress("glEGLImageTargetTexture2DOES"));
  if (egl_create_image == nullptr || egl_destroy_image == nullptr ||
      gl_egl_image_target_texture == nullptr) {
    std::fprintf(stderr, "[dmabuf] required EGL/GL extension functions not available\n");
    return {0, 0, 0, 0};
  }

  const EGLint attribs[] = {
      EGL_WIDTH, static_cast<EGLint>(width),
      EGL_HEIGHT, static_cast<EGLint>(height),
      EGL_LINUX_DRM_FOURCC_EXT, DRM_FORMAT_ARGB8888, // matches this file's format=BGRA request -
                                                      // DRM_FORMAT_ARGB8888 is little-endian B,G,R,A
                                                      // byte order, same memory layout as GL BGRA
      EGL_DMA_BUF_PLANE0_FD_EXT, input.frame.fd,
      EGL_DMA_BUF_PLANE0_OFFSET_EXT, input.frame.offset,
      EGL_DMA_BUF_PLANE0_PITCH_EXT, input.frame.stride,
      EGL_DMA_BUF_PLANE0_MODIFIER_LO_EXT, static_cast<EGLint>(kKnownDrmModifier & 0xFFFFFFFFu),
      EGL_DMA_BUF_PLANE0_MODIFIER_HI_EXT, static_cast<EGLint>(kKnownDrmModifier >> 32),
      EGL_NONE,
  };
  EGLImageKHR image =
      egl_create_image(g_display, EGL_NO_CONTEXT, EGL_LINUX_DMA_BUF_EXT, nullptr, attribs);
  if (image == EGL_NO_IMAGE_KHR) {
    std::fprintf(stderr, "[dmabuf] eglCreateImageKHR failed: 0x%x\n", eglGetError());
    pw_main_loop_destroy(loop);
    return {0, 0, 0, 0};
  }

  GLuint tex = 0;
  glGenTextures(1, &tex);
  glBindTexture(GL_TEXTURE_2D, tex);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  gl_egl_image_target_texture(GL_TEXTURE_2D, image);

  GLuint fbo = 0, color_tex = 0;
  glGenFramebuffers(1, &fbo);
  glBindFramebuffer(GL_FRAMEBUFFER, fbo);
  glGenTextures(1, &color_tex);
  glBindTexture(GL_TEXTURE_2D, color_tex);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, color_tex, 0);
  glViewport(0, 0, static_cast<GLsizei>(width), static_cast<GLsizei>(height));
  glDisable(GL_BLEND);
  ObjectParams p;
  p.canvas_w = static_cast<float>(width);
  p.canvas_h = static_cast<float>(height);
  p.dst_w = static_cast<float>(width);
  p.dst_h = static_cast<float>(height);
  draw_object(tex, p);
  glFinish();

  std::vector<uint8_t> pixels(static_cast<size_t>(width) * height * 4);
  glReadPixels(0, 0, static_cast<GLsizei>(width), static_cast<GLsizei>(height), GL_RGBA,
               GL_UNSIGNED_BYTE, pixels.data());
  const uint32_t buffer_row = height - 1 - sample_y;
  const size_t idx = (static_cast<size_t>(buffer_row) * width + sample_x) * 4;
  std::printf("[dmabuf] sampled pixel at (%u,%u): (%d,%d,%d,%d)\n", sample_x, sample_y, pixels[idx],
              pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]);

  egl_destroy_image(g_display, image);
  glDeleteTextures(1, &tex);
  glDeleteFramebuffers(1, &fbo);
  glDeleteTextures(1, &color_tex);
  pw_main_loop_destroy(loop);
  // Deliberately not closing input.frame.fd or destroying input.stream -
  // see header comment (this test never releases its one buffer).

  return {pixels[idx], pixels[idx + 1], pixels[idx + 2], pixels[idx + 3]};
}

// ---- Real continuous compositor (task #9) ----
//
// Bridges two independently-clocked things via a lock-free single-slot
// "latest frame" buffer, same pattern as midi_cube_main.cpp's render_thread/
// latest_frame (background thread produces, RT callback copies out) - just
// with the roles matching this program: the main loop (driven by real audio
// ticks, does all the GL rendering) is the non-RT producer; on_real_output_
// process (driven by PipeWire's own graph scheduling, whenever a downstream
// consumer wants a frame) is the RT consumer that only memcpy's. These two
// things advance at different, independent rates - that's exactly what this
// buffer is for.
constexpr uint32_t kOutputBytesPerPixel = 4;

struct LatestFrameSlot {
  std::array<uint8_t, kMaxVideoFrameBytes> data{};
  bool has_frame = false;
};

struct RealOutput {
  pw_stream *stream = nullptr;
  uint32_t width = 0;
  uint32_t height = 0;
  boost::lockfree::spsc_value<LatestFrameSlot, boost::lockfree::allow_multiple_reads<true>> buffer;
  LatestFrameSlot write_scratch; // main loop's own staging slot, never touched by the RT callback
  // Non-null only when downstream is configured - lets this stream's own
  // param_changed also serve as downstream's "object_params" Props target
  // (translation/visibility/etc, see apply_object_params), matching the old
  // downstream-compositor's own out node exactly (se.downstream.out is both
  // the video output AND the Props target for its overlay objects) -
  // downstream never gets active_scene_index/scene-switching (single fixed
  // scene, per 2026-09-09 scope decision), so no scenes-list publish here.
  SceneRuntime *downstream_scene = nullptr;
};

void on_real_output_process(void *data) {
  auto &output = *static_cast<RealOutput *>(data);
  auto *pw_buffer = pw_stream_dequeue_buffer(output.stream);
  if (pw_buffer == nullptr)
    return;
  auto *buffer = pw_buffer->buffer;
  if (buffer->n_datas == 0 || buffer->datas[0].data == nullptr || buffer->datas[0].chunk == nullptr) {
    pw_stream_queue_buffer(output.stream, pw_buffer);
    return;
  }
  auto &spa_data = buffer->datas[0];
  const uint32_t stride = output.width * kOutputBytesPerPixel;
  const size_t needed = static_cast<size_t>(stride) * output.height;
  if (spa_data.maxsize < needed) {
    pw_stream_queue_buffer(output.stream, pw_buffer);
    return;
  }
  LatestFrameSlot latest;
  output.buffer.consume([&](const LatestFrameSlot &slot) { latest = slot; });
  if (latest.has_frame)
    std::memcpy(spa_data.data, latest.data.data(), needed);
  else
    std::memset(spa_data.data, 0, needed); // nothing rendered yet - black, not garbage
  spa_data.chunk->offset = 0;
  spa_data.chunk->size = static_cast<uint32_t>(needed);
  spa_data.chunk->stride = static_cast<int32_t>(stride);
  spa_data.chunk->flags = 0;
  pw_stream_queue_buffer(output.stream, pw_buffer);
}

void on_real_output_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &output = *static_cast<RealOutput *>(data);
  if (param == nullptr)
    return;
  if (id == SPA_PARAM_Props) {
    if (output.downstream_scene == nullptr)
      return;
    for_each_props_kv(param, [&](const char *key, const spa_pod *value) {
      if (std::strcmp(key, "object_params") == 0) {
        const char *json_text = nullptr;
        if (value->type == SPA_TYPE_String && spa_pod_get_string(value, &json_text) == 0 &&
            json_text != nullptr)
          apply_object_params(*output.downstream_scene, output.width, output.height, json_text);
      }
    });
    return;
  }
  if (id != SPA_PARAM_Format)
    return;
  // Same pattern as video_blender.cpp's on_output_param_changed - without
  // this, consumers like GStreamer's pipewiresrc silently starve/drop every
  // buffer (see that file's own comment for why ParamMeta/Header matters
  // too).
  const uint32_t stride = output.width * kOutputBytesPerPixel;
  std::array<uint8_t, 512> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  const spa_pod *params[] = {
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamBuffers, SPA_PARAM_Buffers, SPA_PARAM_BUFFERS_buffers,
          SPA_POD_CHOICE_RANGE_Int(4, 2, 8), SPA_PARAM_BUFFERS_blocks, SPA_POD_Int(1),
          SPA_PARAM_BUFFERS_size, SPA_POD_Int(stride * output.height), SPA_PARAM_BUFFERS_stride,
          SPA_POD_Int(stride))),
      static_cast<const spa_pod *>(spa_pod_builder_add_object(
          &builder, SPA_TYPE_OBJECT_ParamMeta, SPA_PARAM_Meta, SPA_PARAM_META_type,
          SPA_POD_Id(SPA_META_Header), SPA_PARAM_META_size, SPA_POD_Int(sizeof(spa_meta_header))))};
  pw_stream_update_params(output.stream, params, 2);
}

const pw_stream_events kRealOutputEvents = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_real_output_param_changed,
    .process = on_real_output_process,
};

pw_stream *connect_real_output(pw_loop *loop, void *user_data, const std::string &name,
                               uint32_t width, uint32_t height) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Playback", PW_KEY_MEDIA_ROLE, "Video",
      PW_KEY_MEDIA_CLASS, "Stream/Output/Video", PW_KEY_NODE_NAME, name.c_str(),
      PW_KEY_NODE_DESCRIPTION, "Sonic Eddy GPU compositor", nullptr);
  auto *stream = pw_stream_new_simple(loop, name.c_str(), properties, &kRealOutputEvents, user_data);
  if (stream == nullptr)
    return nullptr;

  std::array<uint8_t, 1024> pod_buffer{};
  auto builder = SPA_POD_BUILDER_INIT(pod_buffer.data(), pod_buffer.size());
  auto video_info = SPA_VIDEO_INFO_RAW_INIT(.format = SPA_VIDEO_FORMAT_RGBA,
                                            .size = SPA_RECTANGLE(width, height),
                                            .framerate = SPA_FRACTION(0, 0));
  const spa_pod *params[] = {spa_format_video_raw_build(&builder, SPA_PARAM_EnumFormat, &video_info)};
  const auto flags =
      static_cast<pw_stream_flags>(PW_STREAM_FLAG_MAP_BUFFERS | PW_STREAM_FLAG_RT_PROCESS);
  if (pw_stream_connect(stream, PW_DIRECTION_OUTPUT, PW_ID_ANY, flags, params, 1) < 0) {
    std::fprintf(stderr, "%s: pw_stream_connect failed\n", name.c_str());
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

// Control-only node - never negotiates a real video format (no consumer
// ever links to it), exists purely as a named Props target matching the
// SonicEddy frontend's existing hardcoded node name (se.video-compositor.
// A.out / .B.out, see CompositorInstanceNames.cs / CompositorClient.cs) so
// the frontend needs zero changes to keep working against this unified
// process. Handles "active_scene_index" (switches which of this side's
// loaded scenes is live) and "object_params" (per-object translation/
// visibility/etc within whichever scene is currently active) - same wire
// protocol as main.cpp's handle_output_props/apply_object_params.
struct SceneControlState {
  pw_stream *stream = nullptr;
  std::deque<SceneRuntime> *scenes = nullptr;
  std::atomic<int> *active_scene_index = nullptr;
  uint32_t canvas_width = 0;
  uint32_t canvas_height = 0;
  std::array<uint8_t, 4096> params_buffer{};
};

void on_scene_control_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &state = *static_cast<SceneControlState *>(data);
  if (param == nullptr || id != SPA_PARAM_Props)
    return;
  bool scene_changed = false;
  for_each_props_kv(param, [&](const char *key, const spa_pod *value) {
    if (std::strcmp(key, "active_scene_index") == 0) {
      int32_t requested = 0;
      if (spa_pod_get_int(value, &requested) == 0 && !state.scenes->empty()) {
        const int clamped = std::clamp(requested, 0, static_cast<int>(state.scenes->size()) - 1);
        state.active_scene_index->store(clamped, std::memory_order_relaxed);
        scene_changed = true;
      }
    } else if (std::strcmp(key, "object_params") == 0) {
      const char *json_text = nullptr;
      if (value->type == SPA_TYPE_String && spa_pod_get_string(value, &json_text) == 0 &&
          json_text != nullptr) {
        const int idx = state.active_scene_index->load(std::memory_order_relaxed);
        if (idx >= 0 && static_cast<size_t>(idx) < state.scenes->size())
          apply_object_params((*state.scenes)[static_cast<size_t>(idx)], state.canvas_width,
                              state.canvas_height, json_text);
      }
    }
  });
  if (scene_changed)
    publish_scene_params(state.stream, *state.scenes,
                         state.active_scene_index->load(std::memory_order_relaxed),
                         state.params_buffer);
}

const pw_stream_events kSceneControlEvents = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_scene_control_param_changed,
};

pw_stream *connect_scene_control(pw_loop *loop, void *user_data, const std::string &name) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Playback", PW_KEY_MEDIA_ROLE, "Video",
      PW_KEY_MEDIA_CLASS, "Stream/Output/Video", PW_KEY_NODE_NAME, name.c_str(), nullptr);
  auto *stream = pw_stream_new_simple(loop, name.c_str(), properties, &kSceneControlEvents, user_data);
  if (stream == nullptr)
    return nullptr;
  if (pw_stream_connect(stream, PW_DIRECTION_OUTPUT, PW_ID_ANY, PW_STREAM_FLAG_MAP_BUFFERS, nullptr,
                        0) < 0) {
    std::fprintf(stderr, "%s: pw_stream_connect failed\n", name.c_str());
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

// Same control-only-node idea as connect_scene_control, but for the T-bar's
// "blend_position" (float 0..1) - matches video_blender.cpp's own Props
// handling exactly, one-way (no readback, per the frontend's
// VideoBlenderClient - see 2026-09-09 frontend research).
struct BlendControlState {
  std::atomic<float> *blend_position = nullptr;
};

void on_blend_control_param_changed(void *data, uint32_t id, const spa_pod *param) {
  auto &state = *static_cast<BlendControlState *>(data);
  if (param == nullptr || id != SPA_PARAM_Props)
    return;
  for_each_props_kv(param, [&](const char *key, const spa_pod *value) {
    if (std::strcmp(key, "blend_position") == 0) {
      float requested = 0.0f;
      if (spa_pod_get_float(value, &requested) == 0)
        state.blend_position->store(std::clamp(requested, 0.0f, 1.0f), std::memory_order_relaxed);
    }
  });
}

const pw_stream_events kBlendControlEvents = {
    .version = PW_VERSION_STREAM_EVENTS,
    .param_changed = on_blend_control_param_changed,
};

pw_stream *connect_blend_control(pw_loop *loop, void *user_data, const std::string &name) {
  auto *properties = pw_properties_new(
      PW_KEY_MEDIA_TYPE, "Video", PW_KEY_MEDIA_CATEGORY, "Playback", PW_KEY_MEDIA_ROLE, "Video",
      PW_KEY_MEDIA_CLASS, "Stream/Output/Video", PW_KEY_NODE_NAME, name.c_str(), nullptr);
  auto *stream = pw_stream_new_simple(loop, name.c_str(), properties, &kBlendControlEvents, user_data);
  if (stream == nullptr)
    return nullptr;
  if (pw_stream_connect(stream, PW_DIRECTION_OUTPUT, PW_ID_ANY, PW_STREAM_FLAG_MAP_BUFFERS, nullptr,
                        0) < 0) {
    std::fprintf(stderr, "%s: pw_stream_connect failed\n", name.c_str());
    pw_stream_destroy(stream);
    return nullptr;
  }
  return stream;
}

// One "side" (A or B) of the T-bar: its own input pool + one-or-more scene
// runtimes (A/B are scene-switchable; downstream always has exactly one and
// never gets a control_stream, see run_real_compositor), rendering into its
// own FBO. All scenes on one side must share canvas dimensions (checked in
// setup_compositor_side, same constraint main.cpp's own --scene enforces).
struct CompositorSide {
  InputPool inputs;
  std::deque<SceneRuntime> scenes; // always >= 1
  std::atomic<int> active_scene_index{0};
  GLuint fbo = 0;
  GLuint color_tex = 0;
  pw_stream *control_stream = nullptr; // null for downstream (no scene-switching there)
};

SceneRuntime &current_scene(CompositorSide &side) {
  int idx = side.active_scene_index.load(std::memory_order_relaxed);
  if (idx < 0 || static_cast<size_t>(idx) >= side.scenes.size())
    idx = 0;
  return side.scenes[static_cast<size_t>(idx)];
}

bool setup_compositor_side(CompositorSide &side, pw_loop *loop, const std::string &inputs_path,
                           const std::vector<std::string> &scene_paths,
                           const std::string &node_prefix) {
  if (scene_paths.empty()) {
    std::fprintf(stderr, "setup_compositor_side: at least one scene is required\n");
    return false;
  }
  auto input_defs = video_config::load(inputs_path);
  if (!input_defs.has_value())
    return false;

  build_input_pool_textures(side.inputs, *input_defs);
  if (!connect_input_pool(side.inputs, loop, *input_defs, node_prefix))
    return false;

  side.scenes.clear();
  for (const auto &scene_path : scene_paths) {
    auto scene_cfg = scene::load_scene(scene_path);
    if (!scene_cfg.has_value())
      return false;
    side.scenes.emplace_back();
    auto &runtime = side.scenes.back();
    if (!build_scene_runtime(runtime, *scene_cfg))
      return false;
    runtime.source_file = scene_path;
    if (runtime.canvas_width != side.scenes.front().canvas_width ||
        runtime.canvas_height != side.scenes.front().canvas_height) {
      std::fprintf(stderr, "setup_compositor_side: all scenes for %s must share the same canvas "
                            "dimensions (%s is %ux%u, expected %ux%u)\n",
                   node_prefix.c_str(), scene_path.c_str(), runtime.canvas_width,
                   runtime.canvas_height, side.scenes.front().canvas_width,
                   side.scenes.front().canvas_height);
      return false;
    }
  }

  const auto &first_scene = side.scenes.front();
  glGenFramebuffers(1, &side.fbo);
  glBindFramebuffer(GL_FRAMEBUFFER, side.fbo);
  glGenTextures(1, &side.color_tex);
  glBindTexture(GL_TEXTURE_2D, side.color_tex);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, static_cast<GLsizei>(first_scene.canvas_width),
              static_cast<GLsizei>(first_scene.canvas_height), 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, side.color_tex, 0);
  return glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE;
}

// ---- Live monitor window (Program/Preview, broadcast T-bar convention) ----
//
// "Program" = the actual live output (blend + downstream) - what's really
// being published. "Preview" = the minority-weighted side (whichever of A/B
// you'd cut to next - same convention video_blender.cpp used: side = B when
// t<0.5 else A), ALSO with downstream's overlays composited on top - a
// capability the old separate-process architecture never had, since
// downstream-compositor only ever saw video-blender's single program
// output, never the "other" side.
//
// Runs on its own dedicated thread, never the main render loop - that loop
// is genuinely realtime-priority in this process (PipeWire elevates
// whichever thread dispatches an RT_PROCESS-flagged stream, confirmed via
// this session's own "acquired realtime priority 83" log lines), and ANY
// Wayland protocol round-trip on an RT thread risks the same priority-
// inversion freeze already hit 6 times in this project's history - that
// risk is about Wayland calls in general, not specifically GStreamer, so a
// native raylib window still needs the exact same isolation. Uses raylib
// (already a proven dependency in this codebase via midi-cube) instead of
// GStreamer/waylandsink - fewer moving parts for this path, and unlike a
// GStreamer-owned window, this is a real foundation for the actual UI
// controls this window will eventually need.
struct PreviewSlot {
  std::array<uint8_t, kMaxVideoFrameBytes> program{};
  std::array<uint8_t, kMaxVideoFrameBytes> preview{};
  bool has_frame = false;
};

struct PreviewState {
  boost::lockfree::spsc_value<PreviewSlot, boost::lockfree::allow_multiple_reads<true>> buffer;
  PreviewSlot write_scratch; // main loop's own staging slot, never touched by the preview thread
  std::atomic<bool> thread_running{false};
  std::thread thread;
  uint32_t canvas_width = 0;
  uint32_t canvas_height = 0;
};

// Persistent last_slot pushed every tick regardless of whether a new frame
// arrived - the exact fix already proven necessary in video_blender.cpp's
// preview_thread_main: "no data until we have real data" was itself a
// freeze bug there (a mapped-but-never-committed surface wedged the
// compositor), not a benign startup gap.
void preview_thread_main(PreviewState *state_ptr) {
  auto &state = *state_ptr;
  constexpr auto kWakePollInterval = std::chrono::milliseconds(50);

  const int pane_w = static_cast<int>(state.canvas_width);   // full canvas res - textures stay this size
  const int pane_h = static_cast<int>(state.canvas_height);
  // Window itself is displayed at 1/4 size (1/16 area) - a full 1920x1080-
  // per-pane window (2160px tall stacked) is unwieldy as a monitor overlay;
  // textures still hold full-resolution data, only the on-screen draw is
  // scaled down (DrawTexturePro below), so this doesn't lose any quality
  // the eye could actually resolve at this window size anyway.
  const int window_pane_w = std::max(1, pane_w / 4);
  const int window_pane_h = std::max(1, pane_h / 4);

  SetConfigFlags(FLAG_WINDOW_TOPMOST);
  InitWindow(window_pane_w, window_pane_h * 2, "Sonic Eddy - Program / Preview");

  Image blank_image{};
  blank_image.width = pane_w;
  blank_image.height = pane_h;
  blank_image.mipmaps = 1;
  blank_image.format = PIXELFORMAT_UNCOMPRESSED_R8G8B8A8;
  std::vector<uint8_t> blank(static_cast<size_t>(pane_w) * pane_h * 4, 0);
  blank_image.data = blank.data();
  Texture2D program_tex = LoadTextureFromImage(blank_image);
  Texture2D preview_tex = LoadTextureFromImage(blank_image);

  PreviewSlot last_slot;

  auto tick = [&] {
    // Single call, not a drain loop: this is a "latest value" slot, not a
    // queue. Wrapping it in while(consume(...)) {} (copied from a queue-
    // draining idiom that doesn't apply here) spun forever on the very
    // first call - allow_multiple_reads<true> means consume() keeps
    // succeeding on every call once any value has ever been written, it
    // doesn't mean "false once drained". Matches on_real_output_process's
    // own single, unlooped consume() call exactly (2026-09-09 bug: this
    // was the entire reason the preview thread never got past its first
    // tick - 100% CPU on that thread, zero ticks logged, window stuck
    // showing whatever raylib's backbuffer was before any draw call ever
    // ran).
    state.buffer.consume([&last_slot](const PreviewSlot &slot) { last_slot = slot; });
    if (last_slot.has_frame) {
      UpdateTexture(program_tex, last_slot.program.data());
      UpdateTexture(preview_tex, last_slot.preview.data());
    }
    BeginDrawing();
    ClearBackground(BLACK);
    // DrawTexturePro, not DrawTexture: source rect is the full-res texture,
    // dest rect is the 1/4-size window pane - this is what actually scales
    // the display down (window_pane_w/h alone only changes the window's
    // dimensions, not what gets drawn into it).
    const Rectangle program_src{0, 0, static_cast<float>(pane_w), static_cast<float>(pane_h)};
    const Rectangle program_dst{0, 0, static_cast<float>(window_pane_w),
                                static_cast<float>(window_pane_h)};
    const Rectangle preview_dst{0, static_cast<float>(window_pane_h), static_cast<float>(window_pane_w),
                                static_cast<float>(window_pane_h)};
    DrawTexturePro(program_tex, program_src, program_dst, {0, 0}, 0.0f, WHITE);
    DrawTexturePro(preview_tex, program_src, preview_dst, {0, 0}, 0.0f, WHITE);
    EndDrawing();
  };

  // Plain sleep, not a condition_variable wait - raylib's own
  // BeginDrawing/EndDrawing needs to run on a steady cadence to service the
  // window/present cycle regardless of whether a new frame arrived, same
  // reasoning as video_blender.cpp's own tick loop.
  while (state.thread_running.load(std::memory_order_relaxed) && !WindowShouldClose()) {
    tick();
    std::this_thread::sleep_for(kWakePollInterval);
  }

  UnloadTexture(program_tex);
  UnloadTexture(preview_tex);
  CloseWindow();
}

// Runs the actual continuous pipeline: scene A + scene B -> blend ->
// downstream (composited twice per tick when a monitor window is enabled -
// once with the live blend as baseline for "program", once with the
// minority side as baseline for "preview" - see the monitor-window comment
// above). Never returns until SIGINT/SIGTERM or a fatal setup error.
int run_real_compositor(const std::string &inputs_a, const std::vector<std::string> &scenes_a,
                        const std::string &inputs_b, const std::vector<std::string> &scenes_b,
                        const std::string &audio_target, const std::string &out_name,
                        uint32_t canvas_width, uint32_t canvas_height,
                        const std::string &downstream_inputs, const std::string &downstream_scene,
                        bool enable_preview) {
  const bool has_downstream = !downstream_inputs.empty() && !downstream_scene.empty();
  // EGL/GL already initialized by main() before dispatching here - see the
  // init_egl()/init_program()/init_blend_program() call at the top of
  // main(), which runs before any CLI-mode branch (including this one).
  pw_init(nullptr, nullptr);
  pw_main_loop *loop = pw_main_loop_new(nullptr);
  pw_loop *pw_loop_ptr = pw_main_loop_get_loop(loop);

  static CompositorSide side_a, side_b;
  if (!setup_compositor_side(side_a, pw_loop_ptr, inputs_a, scenes_a, "se.gpu-compositor.a") ||
      !setup_compositor_side(side_b, pw_loop_ptr, inputs_b, scenes_b, "se.gpu-compositor.b")) {
    std::fprintf(stderr, "run_real_compositor: failed to set up side A/B\n");
    return 1;
  }

  // Control-only nodes matching the SonicEddy frontend's existing hardcoded
  // node names exactly (se.video-compositor.A.out / .B.out - see
  // CompositorInstanceNames.cs) - active_scene_index + object_params for
  // each side, so the frontend's Streaming Controls window keeps working
  // against this unified process unchanged.
  static SceneControlState control_a, control_b;
  control_a.scenes = &side_a.scenes;
  control_a.active_scene_index = &side_a.active_scene_index;
  control_a.canvas_width = side_a.scenes.front().canvas_width;
  control_a.canvas_height = side_a.scenes.front().canvas_height;
  side_a.control_stream = connect_scene_control(pw_loop_ptr, &control_a, "se.video-compositor.A.out");
  control_a.stream = side_a.control_stream;

  control_b.scenes = &side_b.scenes;
  control_b.active_scene_index = &side_b.active_scene_index;
  control_b.canvas_width = side_b.scenes.front().canvas_width;
  control_b.canvas_height = side_b.scenes.front().canvas_height;
  side_b.control_stream = connect_scene_control(pw_loop_ptr, &control_b, "se.video-compositor.B.out");
  control_b.stream = side_b.control_stream;

  if (side_a.control_stream == nullptr || side_b.control_stream == nullptr) {
    std::fprintf(stderr, "run_real_compositor: failed to set up A/B scene control streams\n");
    return 1;
  }
  publish_scene_params(side_a.control_stream, side_a.scenes, 0, control_a.params_buffer);
  publish_scene_params(side_b.control_stream, side_b.scenes, 0, control_b.params_buffer);

  // Control-only node for the T-bar's "blend_position" - matches
  // video_blender.cpp's own se.video-blender.out node name exactly (see
  // VideoBlenderService.cs), so the frontend needs zero changes.
  static std::atomic<float> blend_position{0.5f};
  static BlendControlState blend_control;
  blend_control.blend_position = &blend_position;
  pw_stream *blend_control_stream =
      connect_blend_control(pw_loop_ptr, &blend_control, "se.video-blender.out");
  if (blend_control_stream == nullptr) {
    std::fprintf(stderr, "run_real_compositor: failed to set up blend control stream\n");
    return 1;
  }

  GLuint blend_fbo = 0, blend_tex = 0;
  glGenFramebuffers(1, &blend_fbo);
  glBindFramebuffer(GL_FRAMEBUFFER, blend_fbo);
  glGenTextures(1, &blend_tex);
  glBindTexture(GL_TEXTURE_2D, blend_tex);
  glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, static_cast<GLsizei>(canvas_width),
              static_cast<GLsizei>(canvas_height), 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
  glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
  glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, blend_tex, 0);
  if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
    std::fprintf(stderr, "run_real_compositor: blend FBO incomplete\n");
    return 1;
  }

  // Downstream is optional (per downstream_scene.hpp's own established
  // convention, its baseline - tex_blend here - is never a scene object,
  // it's the previous stage's output drawn first, then downstream's own
  // overlay objects paint on top). Skipped entirely if not provided -
  // run_real_compositor outputs tex_blend directly in that case, matching
  // this function's behavior before this stage was wired in.
  static CompositorSide downstream;
  if (has_downstream &&
      !setup_compositor_side(downstream, pw_loop_ptr, downstream_inputs, {downstream_scene},
                             "se.gpu-compositor.downstream")) {
    std::fprintf(stderr, "run_real_compositor: failed to set up downstream\n");
    return 1;
  }
  static AudioClock clock;
  clock.stream = connect_audio_clock(pw_loop_ptr, &clock, audio_target, 48000, 2);
  if (clock.stream == nullptr) {
    std::fprintf(stderr, "run_real_compositor: failed to connect audio clock\n");
    return 1;
  }

  static RealOutput output;
  output.width = canvas_width;
  output.height = canvas_height;
  // Downstream never gets scene-switching (single fixed scene, index 0
  // always) - object_params is handled directly on this same output
  // stream's param_changed, see RealOutput's own comment.
  if (has_downstream)
    output.downstream_scene = &current_scene(downstream);
  output.stream = connect_real_output(pw_loop_ptr, &output, out_name, canvas_width, canvas_height);
  if (output.stream == nullptr) {
    std::fprintf(stderr, "run_real_compositor: failed to connect output stream\n");
    return 1;
  }

  std::vector<uint8_t> readback_buf(static_cast<size_t>(canvas_width) * canvas_height * 4);
  uint64_t last_tick = 0;
  std::atomic<bool> quit{false};
  pw_loop_add_signal(pw_loop_ptr, SIGINT, [](void *d, int) { *static_cast<std::atomic<bool> *>(d) = true; }, &quit);
  pw_loop_add_signal(pw_loop_ptr, SIGTERM, [](void *d, int) { *static_cast<std::atomic<bool> *>(d) = true; }, &quit);

  // Spawned only after the signal handlers above are armed: pw_loop_add_signal
  // blocks SIGINT/SIGTERM in *this* thread's mask so PipeWire's own signalfd
  // handles them deterministically; a new std::thread inherits the creating
  // thread's mask at creation time, not retroactively. Spawning the preview
  // thread earlier left it with the original (unblocked, default-disposition)
  // mask, so the kernel could deliver SIGTERM straight to it instead of the
  // pw_loop - confirmed via a real run (2026-09-09): the process vanished
  // instantly with no "shutting down" line ever reaching the log, exactly the
  // signature of an unhandled-default-action kill racing the graceful path.
  static PreviewState preview_state;
  if (enable_preview) {
    preview_state.canvas_width = canvas_width;
    preview_state.canvas_height = canvas_height;
    preview_state.thread_running.store(true, std::memory_order_relaxed);
    preview_state.thread = std::thread(preview_thread_main, &preview_state);
  }

  std::printf("gpu-compositor running (canvas %ux%u)\n", canvas_width, canvas_height);
  std::fflush(stdout);

  while (!quit.load(std::memory_order_relaxed)) {
    pw_loop_iterate(pw_loop_ptr, 10);
    const uint64_t current_tick = clock.tick_count.load(std::memory_order_relaxed);
    if (current_tick == last_tick)
      continue;
    last_tick = current_tick;

    sync_input_pool_textures(side_a.inputs);
    sync_input_pool_textures(side_b.inputs);

    SceneRuntime &scene_a = current_scene(side_a);
    SceneRuntime &scene_b = current_scene(side_b);

    glBindFramebuffer(GL_FRAMEBUFFER, side_a.fbo);
    glViewport(0, 0, static_cast<GLsizei>(scene_a.canvas_width),
              static_cast<GLsizei>(scene_a.canvas_height));
    glClearColor(0, 0, 0, 1);
    glClear(GL_COLOR_BUFFER_BIT);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    render_scene_runtime(scene_a, side_a.inputs);

    glBindFramebuffer(GL_FRAMEBUFFER, side_b.fbo);
    glViewport(0, 0, static_cast<GLsizei>(scene_b.canvas_width),
              static_cast<GLsizei>(scene_b.canvas_height));
    glClearColor(0, 0, 0, 1);
    glClear(GL_COLOR_BUFFER_BIT);
    render_scene_runtime(scene_b, side_b.inputs);

    const float current_blend_position = blend_position.load(std::memory_order_relaxed);
    glBindFramebuffer(GL_FRAMEBUFFER, blend_fbo);
    glViewport(0, 0, static_cast<GLsizei>(canvas_width), static_cast<GLsizei>(canvas_height));
    glDisable(GL_BLEND); // the blend shader itself computes the final color
    draw_blend(side_a.color_tex, side_b.color_tex, current_blend_position);

    if (has_downstream)
      sync_input_pool_textures(downstream.inputs);

    // Renders baseline_tex + downstream's own overlay objects into
    // downstream.fbo (skipped when downstream isn't configured - baseline_tex
    // is used directly), then reads the result back into dst, row-flipped
    // (GL's glReadPixels returns bottom-up rows; every consumer - dump_
    // consumer, a real encoder, the preview window - expects standard top-
    // down order; every self-contained test in this file already accounted
    // for this at verification time via pixel_at()'s buffer_row = height-1-
    // image_row helper, but this real output-publish step was found missing
    // it via a real, reproducible bug: a downstream overlay confined to
    // specific rows looked undrawn because the published frame's rows didn't
    // correspond to what the overlay's own dst_y meant). Called twice per
    // tick when the preview window is enabled - once with the live blend as
    // baseline for "program", once with the minority side as baseline for
    // "preview" (see the monitor-window comment above) - downstream.fbo is
    // safely reused for both since each call's readback completes before the
    // next call's draw.
    auto render_and_read = [&](GLuint baseline_tex, std::array<uint8_t, kMaxVideoFrameBytes> &dst) {
      GLuint source_fbo = blend_fbo;
      if (has_downstream) {
        SceneRuntime &downstream_scene = current_scene(downstream);
        glBindFramebuffer(GL_FRAMEBUFFER, downstream.fbo);
        glViewport(0, 0, static_cast<GLsizei>(downstream_scene.canvas_width),
                  static_cast<GLsizei>(downstream_scene.canvas_height));
        glDisable(GL_BLEND); // baseline is an opaque full-canvas replace, not a blend
        ObjectParams baseline_params;
        baseline_params.canvas_w = static_cast<float>(downstream_scene.canvas_width);
        baseline_params.canvas_h = static_cast<float>(downstream_scene.canvas_height);
        baseline_params.dst_w = static_cast<float>(downstream_scene.canvas_width);
        baseline_params.dst_h = static_cast<float>(downstream_scene.canvas_height);
        draw_object(baseline_tex, baseline_params);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        render_scene_runtime(downstream_scene, downstream.inputs);
        source_fbo = downstream.fbo;
      }
      glBindFramebuffer(GL_FRAMEBUFFER, source_fbo);
      glFinish();
      glReadPixels(0, 0, static_cast<GLsizei>(canvas_width), static_cast<GLsizei>(canvas_height), GL_RGBA,
                  GL_UNSIGNED_BYTE, readback_buf.data());
      const uint32_t out_stride = canvas_width * kOutputBytesPerPixel;
      const size_t copy_size = std::min(readback_buf.size(), dst.size());
      for (uint32_t y = 0; y < canvas_height; ++y) {
        const uint32_t src_row = canvas_height - 1 - y;
        const size_t row_bytes = out_stride;
        const size_t dst_offset = static_cast<size_t>(y) * out_stride;
        const size_t src_offset = static_cast<size_t>(src_row) * out_stride;
        if (dst_offset + row_bytes > copy_size || src_offset + row_bytes > readback_buf.size())
          break;
        std::memcpy(dst.data() + dst_offset, readback_buf.data() + src_offset, row_bytes);
      }
    };

    render_and_read(blend_tex, output.write_scratch.data);
    output.write_scratch.has_frame = true;
    output.buffer.write(output.write_scratch);

    if (enable_preview) {
      // "program" pane is identical to what was just published above - reuse
      // it rather than re-rendering, since render_and_read's downstream.fbo
      // reuse would otherwise require a second identical draw just to read
      // the same pixels back again.
      preview_state.write_scratch.program = output.write_scratch.data;
      // Broadcast T-bar convention (matches video_blender.cpp): preview
      // shows whichever side is NOT the majority of the current blend -
      // i.e. the side you'd cut TO next.
      const GLuint preview_source_tex =
          (current_blend_position < 0.5f) ? side_b.color_tex : side_a.color_tex;
      render_and_read(preview_source_tex, preview_state.write_scratch.preview);
      preview_state.write_scratch.has_frame = true;
      preview_state.buffer.write(preview_state.write_scratch);
    }
  }

  if (enable_preview) {
    preview_state.thread_running.store(false, std::memory_order_relaxed);
    if (preview_state.thread.joinable())
      preview_state.thread.join();
  }

  std::printf("gpu-compositor shutting down\n");
  pw_stream_destroy(output.stream);
  pw_stream_destroy(clock.stream);
  pw_stream_destroy(side_a.control_stream);
  pw_stream_destroy(side_b.control_stream);
  pw_stream_destroy(blend_control_stream);
  for (auto &input : side_a.inputs.inputs)
    if (input.stream != nullptr)
      pw_stream_destroy(input.stream);
  for (auto &input : side_b.inputs.inputs)
    if (input.stream != nullptr)
      pw_stream_destroy(input.stream);
  if (has_downstream)
    for (auto &input : downstream.inputs.inputs)
      if (input.stream != nullptr)
        pw_stream_destroy(input.stream);
  pw_main_loop_destroy(loop);
  pw_deinit();
  return 0;
}

} // namespace

int main(int argc, char **argv) {
  if (!init_egl() || !init_program() || !init_blend_program())
    return 1;

  // Separate mode, not run by default alongside the render-only tests below
  // - needs a live PipeWire producer process actually running and pushing
  // frames, which is a live-system action requiring confirmation before
  // running (see feedback_no_unannounced_process_launches memory), unlike
  // the self-contained render tests below.
  // Usage: gpu-compositor --ingest <node_name> <target_object> <width>
  //   <height> <sample_x> <sample_y> [max_wait_seconds] [--rgb]
  // --rgb: negotiate SPA_VIDEO_FORMAT_RGB/3bpp instead of the default RGBA -
  // only for testing against gradient_producer, which is intentionally RGB
  // per its own header comment (real cameras/scenes are RGBA, the default).
  if (argc > 1 && std::string(argv[1]) == "--ingest") {
    if (argc < 8) {
      std::fprintf(stderr, "usage: %s --ingest <node_name> <target_object> <width> <height> "
                            "<sample_x> <sample_y> [max_wait_seconds] [--rgb]\n",
                   argv[0]);
      return 1;
    }
    const std::string node_name = argv[2];
    const std::string target_object = argv[3];
    const uint32_t width = static_cast<uint32_t>(std::strtoul(argv[4], nullptr, 10));
    const uint32_t height = static_cast<uint32_t>(std::strtoul(argv[5], nullptr, 10));
    const uint32_t sample_x = static_cast<uint32_t>(std::strtoul(argv[6], nullptr, 10));
    const uint32_t sample_y = static_cast<uint32_t>(std::strtoul(argv[7], nullptr, 10));
    double max_wait = 5.0;
    bool use_rgb = false;
    for (int i = 8; i < argc; ++i) {
      if (std::string(argv[i]) == "--rgb")
        use_rgb = true;
      else
        max_wait = std::strtod(argv[i], nullptr);
    }
    const spa_video_format format = use_rgb ? SPA_VIDEO_FORMAT_RGB : SPA_VIDEO_FORMAT_RGBA;
    const uint32_t bytes_per_pixel = use_rgb ? 3 : 4;
    const auto pixel = test_video_ingestion(node_name, target_object, width, height, max_wait,
                                            sample_x, sample_y, format, bytes_per_pixel);
    std::printf("[ingest] sampled pixel at (%u,%u): (%d,%d,%d,%d)\n", sample_x, sample_y, pixel[0],
                pixel[1], pixel[2], pixel[3]);
    return 0;
  }

  // Same live-producer caveat as --ingest above.
  // Usage: gpu-compositor --audio-clock <target_object> [rate] [channels]
  //   [max_wait_seconds] [min_ticks]
  if (argc > 1 && std::string(argv[1]) == "--audio-clock") {
    if (argc < 3) {
      std::fprintf(stderr, "usage: %s --audio-clock <target_object> [rate] [channels] "
                            "[max_wait_seconds] [min_ticks]\n",
                   argv[0]);
      return 1;
    }
    const std::string target_object = argv[2];
    const uint32_t rate = argc > 3 ? static_cast<uint32_t>(std::strtoul(argv[3], nullptr, 10)) : 48000;
    const uint32_t channels = argc > 4 ? static_cast<uint32_t>(std::strtoul(argv[4], nullptr, 10)) : 2;
    const double max_wait = argc > 5 ? std::strtod(argv[5], nullptr) : 5.0;
    const uint64_t min_ticks = argc > 6 ? std::strtoull(argv[6], nullptr, 10) : 5;
    const bool ok = test_audio_driven_render(target_object, rate, channels, max_wait, min_ticks);
    std::printf("test_audio_driven_render: %s\n", ok ? "PASS" : "FAIL");
    return ok ? 0 : 1;
  }

  // Same live-producer caveat as --ingest above. target_object should be
  // the node name of a real dmabuf-producing PipeWire video source (see
  // 2026-09-09 design conversation - verified working via gst-launch-1.0
  // videotestsrc ! vapostproc ! "video/x-raw(memory:DMABuf),format=DMA_DRM"
  // ! pipewiresink mode=provide, no camera hardware needed).
  // Usage: gpu-compositor --dmabuf-test <target_object> <width> <height>
  //   <sample_x> <sample_y> [max_wait_seconds]
  if (argc > 1 && std::string(argv[1]) == "--dmabuf-test") {
    if (argc < 6) {
      std::fprintf(stderr, "usage: %s --dmabuf-test <target_object> <width> <height> <sample_x> "
                            "<sample_y> [max_wait_seconds]\n",
                   argv[0]);
      return 1;
    }
    const std::string target_object = argv[2];
    const uint32_t width = static_cast<uint32_t>(std::strtoul(argv[3], nullptr, 10));
    const uint32_t height = static_cast<uint32_t>(std::strtoul(argv[4], nullptr, 10));
    const uint32_t sample_x = static_cast<uint32_t>(std::strtoul(argv[5], nullptr, 10));
    const uint32_t sample_y = argc > 6 ? static_cast<uint32_t>(std::strtoul(argv[6], nullptr, 10)) : 0;
    const double max_wait = argc > 7 ? std::strtod(argv[7], nullptr) : 5.0;
    const auto pixel = test_dmabuf_ingestion(target_object, width, height, max_wait, sample_x, sample_y);
    std::printf("[dmabuf-test] result pixel: (%d,%d,%d,%d)\n", pixel[0], pixel[1], pixel[2], pixel[3]);
    return 0;
  }

  // The real, continuous compositor (task #9) - not a test, this is the
  // thing meant to eventually replace the 5 old binaries in
  // start-video-pipeline.fish. Runs until SIGINT/SIGTERM. Downstream is
  // optional (omit both --downstream-inputs/--downstream-scene to skip that
  // stage, output tex_blend directly). Flag-based (not positional) because
  // --scene-a/--scene-b are repeatable (A/B are scene-switchable via
  // "active_scene_index" Props, same as the old pw-video-compositor's own
  // repeatable --scene - see se.video-compositor.A.out/.B.out's own
  // comment). Also opens se.video-compositor.A.out/.B.out (scene switching +
  // object_params) and se.video-blender.out (blend_position) as control-only
  // nodes matching the SonicEddy frontend's existing hardcoded node names
  // exactly - no frontend changes needed.
  // Usage: gpu-compositor --real --inputs-a <f> --scene-a <f> [--scene-a <f> ...]
  //   --inputs-b <f> --scene-b <f> [--scene-b <f> ...] --audio-target <name>
  //   --out <name> --width <w> --height <h>
  //   [--downstream-inputs <f> --downstream-scene <f>] [--preview]
  if (argc > 1 && std::string(argv[1]) == "--real") {
    std::string inputs_a, inputs_b, audio_target, out_name, downstream_inputs, downstream_scene;
    std::vector<std::string> scenes_a, scenes_b;
    uint32_t width = 0, height = 0;
    bool enable_preview = false;
    for (int i = 2; i < argc; ++i) {
      const std::string arg = argv[i];
      auto next = [&]() { return std::string(i + 1 < argc ? argv[++i] : ""); };
      if (arg == "--inputs-a") inputs_a = next();
      else if (arg == "--scene-a") scenes_a.push_back(next());
      else if (arg == "--inputs-b") inputs_b = next();
      else if (arg == "--scene-b") scenes_b.push_back(next());
      else if (arg == "--audio-target") audio_target = next();
      else if (arg == "--out") out_name = next();
      else if (arg == "--width") width = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
      else if (arg == "--height") height = static_cast<uint32_t>(std::strtoul(next().c_str(), nullptr, 10));
      else if (arg == "--downstream-inputs") downstream_inputs = next();
      else if (arg == "--downstream-scene") downstream_scene = next();
      else if (arg == "--preview") enable_preview = true;
      else {
        std::fprintf(stderr, "--real: unknown argument \"%s\"\n", arg.c_str());
        return 1;
      }
    }
    if (inputs_a.empty() || scenes_a.empty() || inputs_b.empty() || scenes_b.empty() ||
        audio_target.empty() || out_name.empty() || width == 0 || height == 0) {
      std::fprintf(stderr,
                  "usage: %s --real --inputs-a <f> --scene-a <f> [--scene-a <f> ...] "
                  "--inputs-b <f> --scene-b <f> [--scene-b <f> ...] --audio-target <name> "
                  "--out <name> --width <w> --height <h> "
                  "[--downstream-inputs <f> --downstream-scene <f>] [--preview]\n",
                  argv[0]);
      return 1;
    }
    return run_real_compositor(inputs_a, scenes_a, inputs_b, scenes_b, audio_target, out_name, width,
                               height, downstream_inputs, downstream_scene, enable_preview);
  }

  const bool pass_opacity = test_opacity_and_gain();
  std::printf("test_opacity_and_gain: %s\n\n", pass_opacity ? "PASS" : "FAIL");

  const bool pass_rotate = test_rotate_180();
  std::printf("test_rotate_180: %s\n\n", pass_rotate ? "PASS" : "FAIL");

  const bool pass_flip = test_flip_horizontal();
  std::printf("test_flip_horizontal: %s\n\n", pass_flip ? "PASS" : "FAIL");

  const bool pass_blend = test_blend_stage();
  std::printf("test_blend_stage: %s\n\n", pass_blend ? "PASS" : "FAIL");

  bool pass_scene = true;
  if (argc > 1) {
    pass_scene = test_scene_paint_order(argv[1]);
    std::printf("test_scene_paint_order: %s\n\n", pass_scene ? "PASS" : "FAIL");
  } else {
    std::printf("test_scene_paint_order: SKIPPED (pass a scene.json path as argv[1])\n\n");
  }

  bool pass_downstream = true;
  if (argc > 2) {
    pass_downstream = test_downstream_compositing(argv[2]);
    std::printf("test_downstream_compositing: %s\n\n", pass_downstream ? "PASS" : "FAIL");
  } else {
    std::printf("test_downstream_compositing: SKIPPED (pass an overlay scene.json path as argv[2])\n\n");
  }

  const bool all_pass =
      pass_opacity && pass_rotate && pass_flip && pass_blend && pass_scene && pass_downstream;
  std::printf("%s\n", all_pass ? "ALL PASS" : "SOME FAILED");
  return all_pass ? 0 : 1;
}
