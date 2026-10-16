#version 330 core

in vec2 texCoord;
in vec3 fragWorldPos;
in vec3 vertColor;

out vec4 FragColor;

uniform int renderMode; // 1 = Sun, 2 = Moon, 3 = Dynamic Clouds, 4 = Stars
uniform vec3 viewPos;
uniform vec3 skyColor;
uniform vec3 celestialColor;
uniform vec3 celestialRimColor;
uniform vec2 cloudOffset;
uniform vec3 cloudColor;
uniform float cloudAlpha;
uniform float cloudThreshold; // Cutoff for cloud density (higher = fewer clouds, lower = denser clouds)
uniform float fadeStart;
uniform float fadeEnd;
uniform float starBrightness;

// Hash and cellular noise functions for dynamic voxel clouds
float hash21(vec2 p)
{
    p = fract(p * vec2(234.34, 435.345));
    p += dot(p, p + 34.23);
    return fract(p.x * p.y);
}

float cloudNoise(vec2 c)
{
    // Layered 3-octave cellular noise for cohesive Minecraft cloud formations
    float n1 = hash21(c * 0.12);
    float n2 = hash21(c * 0.06 + 17.43);
    float n3 = hash21(c * 0.02 + 89.12);
    return n1 * 0.20 + n2 * 0.35 + n3 * 0.45;
}

// Iconic Minecraft 8x8 moon crater pattern
bool isMoonCrater(vec2 g)
{
    int gx = int(g.x);
    int gy = int(g.y);

    // Classic lunar maria layout on 8x8 grid
    if ((gx == 2 && gy == 2) || (gx == 3 && gy == 2) || (gx == 2 && gy == 3) ||
        (gx == 5 && gy == 4) || (gx == 5 && gy == 5) || (gx == 6 && gy == 4) ||
        (gx == 2 && gy == 5) || (gx == 1 && gy == 5) || (gx == 4 && gy == 1))
    {
        return true;
    }
    return false;
}

void main()
{
    if (renderMode == 1)
    {
        // === SUN (Classic Minecraft Square Sun with Radiant Halo) ===
        vec2 d = abs(texCoord - vec2(0.5));
        float maxD = max(d.x, d.y);
        if (maxD > 0.5)
        {
            discard;
        }

        vec3 col;
        float alpha = 1.0;

        if (maxD <= 0.36)
        {
            // Radiant core
            col = celestialColor;
        }
        else if (maxD <= 0.46)
        {
            // Golden solar corona rim
            float t = (maxD - 0.36) / 0.10;
            col = mix(celestialColor, celestialRimColor, t);
        }
        else
        {
            // Outer atmospheric bloom / soft edge
            col = celestialRimColor;
            alpha = 1.0 - smoothstep(0.46, 0.50, maxD);
        }

        FragColor = vec4(col, alpha);
    }
    else if (renderMode == 2)
    {
        // === MOON (Classic Minecraft Square Moon with Silvery Craters) ===
        vec2 d = abs(texCoord - vec2(0.5));
        float maxD = max(d.x, d.y);
        if (maxD > 0.5)
        {
            discard;
        }

        // 8x8 grid for crater pattern
        vec2 grid = floor(texCoord * 8.0);
        vec3 col = celestialColor;

        if (isMoonCrater(grid))
        {
            // Lunar maria (craters)
            col *= 0.72;
        }
        else
        {
            // Subtle geometric texture
            float subtle = (mod(grid.x + grid.y, 2.0) == 0.0) ? 1.0 : 0.94;
            col *= subtle;
        }

        // Soft outer edge
        float alpha = 1.0;
        if (maxD > 0.46)
        {
            alpha = 1.0 - smoothstep(0.46, 0.50, maxD);
        }

        FragColor = vec4(col, alpha);
    }
    else if (renderMode == 3)
    {
        // === DYNAMIC VOXEL CLOUDS ===
        // Discard based on distance fog towards horizon
        float dist = length(fragWorldPos.xz - viewPos.xz);
        float horizonFade = 1.0 - smoothstep(fadeStart, fadeEnd, dist);
        if (horizonFade <= 0.01)
        {
            discard;
        }

        // Discrete voxel cells
        float cellSize = 14.0;
        vec2 cloudPos = fragWorldPos.xz + cloudOffset;
        vec2 cell = floor(cloudPos / cellSize);

        float density = cloudNoise(cell);
        if (density < cloudThreshold)
        {
            discard;
        }

        // Block edge shading (gives a 3D blocky appearance to cloud cells)
        vec2 frac = fract(cloudPos / cellSize);
        float edgeDist = min(min(frac.x, 1.0 - frac.x), min(frac.y, 1.0 - frac.y));
        float edgeShade = mix(0.86, 1.0, clamp(edgeDist * 6.0, 0.0, 1.0));

        vec3 col = cloudColor * edgeShade;
        float alpha = cloudAlpha * horizonFade;

        FragColor = vec4(col, alpha);
    }
    else if (renderMode == 4)
    {
        // === NIGHT STARS ===
        vec2 d = abs(texCoord - vec2(0.5));
        float maxD = max(d.x, d.y);
        if (maxD > 0.5 || starBrightness <= 0.01)
        {
            discard;
        }

        float a = (1.0 - smoothstep(0.2, 0.5, maxD)) * starBrightness;
        FragColor = vec4(vertColor, a);
    }
    else
    {
        FragColor = vec4(vertColor, 1.0);
    }
}
