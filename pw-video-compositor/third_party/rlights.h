/**********************************************************************************************
*
*   raylib.lights - Some useful functions to deal with lights data
*
*   CONFIGURATION:
*       #define RLIGHTS_IMPLEMENTATION
*           Generates the implementation of the library into the included file.
*
*   LICENSE: zlib/libpng
*
*   Copyright (c) 2017-2024 Victor Fisac (@victorfisac) and Ramon Santamaria (@raysan5)
*
*   This software is provided "as-is", without any express or implied warranty. In no event
*   will the authors be held liable for any damages arising from the use of this software.
*
*   Permission is granted to anyone to use this software for any purpose, including commercial
*   applications, and to alter it and redistribute it freely, subject to the following restrictions:
*
*     1. The origin of this software must not be misrepresented; you must not claim that you
*     wrote the original software. If you use this software in a product, an acknowledgment
*     in the product documentation would be appreciated but is not required.
*
*     2. Altered source versions must be plainly marked as such, and must not be misrepresented
*     as being the original software.
*
*     3. This notice may not be removed or altered from any source distribution.
*
**********************************************************************************************/
// Vendored (unmodified apart from formatting) alongside third_party/stb_image.h - see
// pw-video-compositor/src/cube_renderer.cpp for the sole consumer.

#ifndef RLIGHTS_H
#define RLIGHTS_H

#define MAX_LIGHTS 4

typedef enum {
    LIGHT_DIRECTIONAL = 0,
    LIGHT_POINT
} LightType;

typedef struct {
    int type;
    bool enabled;
    Vector3 position;
    Vector3 target;
    Color color;

    int enabledLoc;
    int typeLoc;
    int positionLoc;
    int targetLoc;
    int colorLoc;
} Light;

#ifdef __cplusplus
extern "C" {
#endif

Light CreateLight(int type, Vector3 position, Vector3 target, Color color, Shader shader);
void UpdateLightValues(Shader shader, Light light);

#ifdef __cplusplus
}
#endif

#endif // RLIGHTS_H

#if defined(RLIGHTS_IMPLEMENTATION)

static int lightsCount = 0;

Light CreateLight(int type, Vector3 position, Vector3 target, Color color, Shader shader)
{
    Light light = { 0 };

    if (lightsCount < MAX_LIGHTS)
    {
        light.enabled = true;
        light.type = type;
        light.position = position;
        light.target = target;
        light.color = color;

        char enabledName[32] = "lights[x].enabled\0";
        char typeName[32] = "lights[x].type\0";
        char posName[32] = "lights[x].position\0";
        char targetName[32] = "lights[x].target\0";
        char colorName[32] = "lights[x].color\0";

        enabledName[7] = '0' + lightsCount;
        typeName[7] = '0' + lightsCount;
        posName[7] = '0' + lightsCount;
        targetName[7] = '0' + lightsCount;
        colorName[7] = '0' + lightsCount;

        light.enabledLoc = GetShaderLocation(shader, enabledName);
        light.typeLoc = GetShaderLocation(shader, typeName);
        light.positionLoc = GetShaderLocation(shader, posName);
        light.targetLoc = GetShaderLocation(shader, targetName);
        light.colorLoc = GetShaderLocation(shader, colorName);

        UpdateLightValues(shader, light);

        lightsCount++;
    }

    return light;
}

void UpdateLightValues(Shader shader, Light light)
{
    SetShaderValue(shader, light.enabledLoc, &light.enabled, SHADER_UNIFORM_INT);
    SetShaderValue(shader, light.typeLoc, &light.type, SHADER_UNIFORM_INT);

    float position[3] = { light.position.x, light.position.y, light.position.z };
    SetShaderValue(shader, light.positionLoc, position, SHADER_UNIFORM_VEC3);

    float target[3] = { light.target.x, light.target.y, light.target.z };
    SetShaderValue(shader, light.targetLoc, target, SHADER_UNIFORM_VEC3);

    float color[4] = { (float)light.color.r/(float)255, (float)light.color.g/(float)255,
                        (float)light.color.b/(float)255, (float)light.color.a/(float)255 };
    SetShaderValue(shader, light.colorLoc, color, SHADER_UNIFORM_VEC4);
}

#endif // RLIGHTS_IMPLEMENTATION
