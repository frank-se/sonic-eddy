#include "cube_renderer.hpp"

#include <algorithm>
#include <cstring>

#include <raylib.h>
#include <raymath.h>

#define RLIGHTS_IMPLEMENTATION
#include "rlights.h"

namespace midi_cube {

namespace {

// World-space layout constants for the cube - X (time) and Y (note number)
// extents, and the per-channel Z spacing that makes each MIDI channel read
// as its own plane.
constexpr float kCubeTimeExtent = 16.0f;
constexpr float kCubeNoteExtent = 10.0f;
constexpr float kChannelSpacing = 1.2f;
constexpr float kNoteCuboidHeight = 0.35f;
constexpr float kMinCuboidWidth = 0.08f; // a just-triggered note-on is still a visible sliver

// Vendored GLSL 330 basic lighting shader (raylib's standard examples/shaders
// pair, e.g. examples/shaders/resources/shaders/glsl330/lighting.vs/.fs) -
// inlined as string literals rather than loaded from disk, since this is a
// headless background service with no bundled resource directory.
constexpr const char *kLightingVs = R"glsl(
#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;
uniform mat4 matModel;
uniform mat4 matNormal;

out vec3 fragPosition;
out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragNormal;

void main()
{
    fragPosition = vec3(matModel * vec4(vertexPosition, 1.0));
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    fragNormal = normalize(vec3(matNormal * vec4(vertexNormal, 1.0)));
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
)glsl";

constexpr const char *kLightingFs = R"glsl(
#version 330
in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;

uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 viewPos;

#define MAX_LIGHTS 4
#define LIGHT_DIRECTIONAL 0
#define LIGHT_POINT 1

struct Light {
    int enabled;
    int type;
    vec3 position;
    vec3 target;
    vec4 color;
};

uniform Light lights[MAX_LIGHTS];
uniform vec4 ambient;

out vec4 finalColor;

void main()
{
    vec4 texelColor = texture(texture0, fragTexCoord) * fragColor;
    vec3 lightDot = vec3(0.0);
    vec3 normal = normalize(fragNormal);
    vec3 viewD = normalize(viewPos - fragPosition);
    vec3 specular = vec3(0.0);

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (lights[i].enabled == 1)
        {
            vec3 light = vec3(0.0);
            if (lights[i].type == LIGHT_DIRECTIONAL)
                light = -normalize(lights[i].target - lights[i].position);
            if (lights[i].type == LIGHT_POINT)
                light = normalize(lights[i].position - fragPosition);

            float NdotL = max(dot(normal, light), 0.0);
            lightDot += lights[i].color.rgb * NdotL;

            float specCo = 0.0;
            if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewD, reflect(-(light), normal))), 16.0);
            specular += specCo;
        }
    }

    finalColor = (texelColor * ((colDiffuse + vec4(specular, 1.0)) * vec4(lightDot, 1.0)));
    finalColor += texelColor * (ambient / 10.0) * colDiffuse;
    finalColor = pow(finalColor, vec4(1.0 / 2.2));
}
)glsl";

// Position (note/channel) -> hue+saturation, velocity -> brightness.
Color color_for(const NoteSpan &span) {
  const float hue = static_cast<float>(span.note_number % 12) * 30.0f;
  const float saturation = 0.55f + 0.45f * (static_cast<float>(span.channel) / 15.0f);
  const float value =
      std::clamp(static_cast<float>(span.velocity) / 127.0f, 0.15f, 1.0f);
  return ColorFromHSV(hue, saturation, value);
}

} // namespace

struct CubeRenderer::Impl {
  RenderTexture2D target{};
  Mesh cube_mesh{};
  Material cube_material{};
  Shader lighting_shader{};
  Light light{};
  Camera3D camera{};
};

CubeRenderer::CubeRenderer(uint32_t width, uint32_t height,
                           double time_window_seconds, const CameraConfig &camera)
    : _width(width), _height(height), _time_window_seconds(time_window_seconds),
      _camera(camera), _impl(new Impl) {
  SetConfigFlags(FLAG_WINDOW_HIDDEN);
  InitWindow(static_cast<int>(_width), static_cast<int>(_height), "midi-cube");

  _impl->target = LoadRenderTexture(static_cast<int>(_width), static_cast<int>(_height));
  _impl->cube_mesh = GenMeshCube(1.0f, 1.0f, 1.0f);
  _impl->lighting_shader = LoadShaderFromMemory(kLightingVs, kLightingFs);
  _impl->lighting_shader.locs[SHADER_LOC_VECTOR_VIEW] =
      GetShaderLocation(_impl->lighting_shader, "viewPos");

  _impl->cube_material = LoadMaterialDefault();
  _impl->cube_material.shader = _impl->lighting_shader;

  const int ambient_loc = GetShaderLocation(_impl->lighting_shader, "ambient");
  const float ambient_value[4] = {0.25f, 0.25f, 0.3f, 1.0f};
  SetShaderValue(_impl->lighting_shader, ambient_loc, ambient_value, SHADER_UNIFORM_VEC4);

  _impl->light = CreateLight(LIGHT_DIRECTIONAL, Vector3{10.0f, 12.0f, 8.0f},
                             Vector3{0.0f, 0.0f, 0.0f}, WHITE, _impl->lighting_shader);

  _impl->camera.position = Vector3{_camera.pos_x, _camera.pos_y, _camera.pos_z};
  _impl->camera.target = Vector3{_camera.target_x, _camera.target_y, _camera.target_z};
  _impl->camera.up = Vector3{0.0f, 1.0f, 0.0f};
  _impl->camera.fovy = _camera.fov_y;
  _impl->camera.projection = CAMERA_PERSPECTIVE;
}

CubeRenderer::~CubeRenderer() {
  UnloadMesh(_impl->cube_mesh);
  UnloadMaterial(_impl->cube_material); // also frees its (non-default) shader
  UnloadRenderTexture(_impl->target);
  CloseWindow();
  delete _impl;
}

void CubeRenderer::render(const std::vector<NoteSpan> &spans, double now,
                          std::vector<uint8_t> &out_rgba) {
  const float view_pos[3] = {_impl->camera.position.x, _impl->camera.position.y,
                             _impl->camera.position.z};
  SetShaderValue(_impl->lighting_shader,
                _impl->lighting_shader.locs[SHADER_LOC_VECTOR_VIEW], view_pos,
                SHADER_UNIFORM_VEC3);

  BeginTextureMode(_impl->target);
  ClearBackground(Color{_camera.background_r, _camera.background_g,
                        _camera.background_b, 255});
  BeginMode3D(_impl->camera);

  for (const auto &span : spans) {
    const double window_start = now - _time_window_seconds;
    const double clamped_start = std::max(span.start_seconds, window_start);
    const double end = span.has_end ? span.end_seconds : now;
    if (end < window_start)
      continue; // fully scrolled out

    const float time_from_now =
        static_cast<float>(now - clamped_start) / static_cast<float>(_time_window_seconds);
    const float width_fraction =
        static_cast<float>(std::max(0.0, end - span.start_seconds) / _time_window_seconds);

    const float world_x = -time_from_now * kCubeTimeExtent;
    const float world_width =
        std::max(width_fraction * kCubeTimeExtent, kMinCuboidWidth);
    const float world_y = (static_cast<float>(span.note_number) / 127.0f) * kCubeNoteExtent;
    const float world_z = static_cast<float>(span.channel) * kChannelSpacing;

    // Cuboids grow leftward from "now" (world_x=0) as they age - anchor the
    // mesh's own -X..+X extent at its trailing (older/left) edge, not its
    // center, so the note-on edge stays pinned at its start time.
    const Matrix transform = MatrixMultiply(
        MatrixScale(world_width, kNoteCuboidHeight, kChannelSpacing * 0.8f),
        MatrixTranslate(world_x - world_width * 0.5f, world_y, world_z));

    _impl->cube_material.maps[MATERIAL_MAP_DIFFUSE].color = color_for(span);
    DrawMesh(_impl->cube_mesh, _impl->cube_material, transform);
  }

  EndMode3D();
  EndTextureMode();

  Image img = LoadImageFromTexture(_impl->target.texture);
  ImageFlipVertical(&img); // RenderTexture2D content is bottom-up
  const size_t needed = static_cast<size_t>(_width) * _height * 4;
  out_rgba.resize(needed);
  std::memcpy(out_rgba.data(), img.data, needed);
  UnloadImage(img);
}

} // namespace midi_cube
