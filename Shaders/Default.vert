#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTexCoord;
layout (location = 2) in vec3 aColor;
layout (location = 3) in vec3 aNormal;
layout (location = 4) in float aSwayWeight;

out vec2 texCoord;
out vec3 fragWorldPos;
out vec3 fragColor;
out vec3 fragNormal;

// Uniform variables
uniform mat4 model;
uniform mat4 viewProj;
uniform float uTime;
uniform float uWindStrength;

void main()
{
    vec4 worldPosition = vec4(aPosition, 1.0) * model;

    // Water wave: aSwayWeight == 3.0 (from mesher) or water UV on top face
    bool isWaterVert = (aSwayWeight > 2.5 && aSwayWeight < 3.5) ||
    (aTexCoord.x >= 0.8120 && aTexCoord.x <= 0.8755 && aTexCoord.y >= 0.1870 && aTexCoord.y <= 0.2505 && aNormal.y > 0.5);

    // Lava wave: aSwayWeight == 4.0 (from mesher) or lava UV on top face
    bool isLavaVert = (aSwayWeight > 3.5 && aSwayWeight < 4.5) ||
    (aTexCoord.x >= 0.8120 && aTexCoord.x <= 0.8755 && aTexCoord.y >= 0.0620 && aTexCoord.y <= 0.1255 && aNormal.y > 0.5);

    if (isWaterVert)
    {
        // Smooth harmonic wave connected across adjacent water blocks
        float wave = sin(uTime * 2.8 + worldPosition.x * 1.5 + worldPosition.z * 1.3) * 0.038
        + cos(uTime * 2.1 + worldPosition.x * 1.1 - worldPosition.z * 1.4) * 0.024;
        worldPosition.y += wave;
    }
    else if (isLavaVert)
    {
        // Slower viscous churning wave connected across adjacent lava blocks
        float wave = sin(uTime * 1.8 + worldPosition.x * 1.2 + worldPosition.z * 1.2) * 0.028
        + cos(uTime * 1.4 - worldPosition.x * 0.9 + worldPosition.z * 1.1) * 0.018;
        worldPosition.y += wave;
    }
    else if (uWindStrength > 0.0001 && aSwayWeight > 0.001)
    {
        float t = uTime * 2.4;
        float wave1 = sin(t + worldPosition.x * 0.7 + worldPosition.z * 0.5);
        float wave2 = cos(t * 1.3 + worldPosition.x * 0.4 - worldPosition.z * 0.6);
        float wave3 = sin(t * 0.8 + worldPosition.z * 0.8);

        if (aSwayWeight > 1.5) // Leaves omnidirectional rustle (value 2.0)
        {
            float leafRustle = 0.35 * uWindStrength;
            worldPosition.x += (wave1 * 0.6 + wave2 * 0.4) * leafRustle;
            worldPosition.y += (wave3 * 0.3) * leafRustle;
            worldPosition.z += (wave2 * 0.6 - wave1 * 0.4) * leafRustle;
        }
        else // Plants / Grass / Flowers / Sugarcane / Wheat (value in [0, 1])
        {
            vec2 windOffset = vec2(
                    wave1 * 0.7 + wave2 * 0.3,
                    wave2 * 0.6 + wave3 * 0.4
            );
            float stemBend = aSwayWeight * aSwayWeight * uWindStrength;
            worldPosition.x += windOffset.x * stemBend;
            worldPosition.z += windOffset.y * stemBend * 0.8;
            worldPosition.y -= (windOffset.x * windOffset.x + windOffset.y * windOffset.y) * 0.15 * stemBend;
        }
    }

    fragWorldPos = worldPosition.xyz;
    gl_Position = worldPosition * viewProj;
    texCoord = aTexCoord;
    fragColor = aColor;
    fragNormal = mat3(model) * aNormal;
}