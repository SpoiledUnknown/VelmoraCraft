#version 330 core

in vec2 texCoord;
in vec3 fragWorldPos;
in vec3 fragColor;
in vec3 fragNormal;

out vec4 FragColor;

uniform sampler2D texture0;
uniform vec3 viewPos;
uniform vec3 fogColor;
uniform float fogStart;
uniform float fogEnd;

// Dynamic Day/Night Lighting
uniform vec3 lightDir;
uniform vec3 lightColor;
uniform vec3 ambientColor;
uniform float lightIntensity;

// Dynamic Point Lights (Placed Torches & Handheld Torch)
#define MAX_POINT_LIGHTS 16
uniform int numPointLights;
uniform vec3 pointLightPositions[MAX_POINT_LIGHTS];
uniform vec3 pointLightColors[MAX_POINT_LIGHTS];
uniform float pointLightRadii[MAX_POINT_LIGHTS];
uniform float pointLightIntensities[MAX_POINT_LIGHTS];
uniform float uTime;
uniform int isTranslucentPass; // 0 = Opaque pass, 1 = Translucent / Water pass

// Water animation frames: (13,12), (14,12), (14,13), (15,12), (15,13)
const vec2 waterFrames[5] = vec2[5](
        vec2(13.0, 12.0),
        vec2(14.0, 12.0),
        vec2(14.0, 13.0),
        vec2(15.0, 12.0),
        vec2(15.0, 13.0)
);

// Lava animation frames: (13,14), (14,14), (14,15), (15,14), (15,15)
const vec2 lavaFrames[5] = vec2[5](
        vec2(13.0, 14.0),
        vec2(14.0, 14.0),
        vec2(14.0, 15.0),
        vec2(15.0, 14.0),
        vec2(15.0, 15.0)
);

vec4 sampleSmoothAnimatedTexture(vec2 baseCoord, float baseCol, float baseRow, vec2 frames[5], float animSpeed, bool isWater)
{
    float s = clamp((baseCoord.x - baseCol / 16.0) * 16.0, 0.0, 1.0);
    float t = clamp((baseCoord.y - (1.0 - (baseRow + 1.0) / 16.0)) * 16.0, 0.0, 1.0);

    // Fluid ripple and flow perturbation across the texture tile
    float sp, tp;
    if (isWater)
    {
        float r1 = sin(uTime * 5.2 + s * 16.0 + t * 12.0) * 0.04;
        float r2 = cos(uTime * 4.5 - s * 12.0 + t * 16.0) * 0.04;
        sp = clamp(s + r1 + r2 * 0.3, 0.01, 0.99);
        tp = clamp(t + r2 - r1 * 0.3, 0.01, 0.99);
    }
    else // Lava
    {
        float r1 = sin(uTime * 3.4 + s * 10.0 - t * 8.0) * 0.045;
        float r2 = cos(uTime * 2.8 - s * 8.0 + t * 12.0) * 0.045;
        sp = clamp(s + r1, 0.01, 0.99);
        tp = clamp(t + r2, 0.01, 0.99);
    }

    float progress = uTime * animSpeed;
    int idx0 = int(floor(progress)) % 5;
    if (idx0 < 0) idx0 += 5;
    int idx1 = (idx0 + 1) % 5;
    float blend = smoothstep(0.0, 1.0, fract(progress));

    vec2 f0 = frames[idx0];
    vec2 f1 = frames[idx1];

    vec2 uv0 = vec2((f0.x + sp) / 16.0, 1.0 - (f0.y + 1.0 - tp) / 16.0);
    vec2 uv1 = vec2((f1.x + sp) / 16.0, 1.0 - (f1.y + 1.0 - tp) / 16.0);

    vec4 c0 = texture(texture0, uv0);
    vec4 c1 = texture(texture0, uv1);

    vec4 result = mix(c0, c1, blend);

    if (isWater)
    {
        // Shimmering specular caustics on water ripples
        float shimmer = sin(sp * 22.0 + uTime * 6.5) * cos(tp * 22.0 - uTime * 5.8);
        shimmer = clamp(shimmer * shimmer * 0.25, 0.0, 0.25);
        result.rgb += vec3(shimmer * 0.7, shimmer * 0.9, shimmer * 1.15);
    }
    else
    {
        // Pulsating glowing magma heat currents
        float heatPulse = sin(uTime * 4.0 + sp * 8.0 + tp * 8.0) * 0.12 + 0.08;
        result.rgb += vec3(heatPulse * 0.25, heatPulse * 0.08, 0.0);
    }

    return result;
}

void main()
{
    vec4 texColor;
    // Check for water tile (col=13, row=12)
    if (texCoord.x >= 0.8120 && texCoord.x <= 0.8755 && texCoord.y >= 0.1870 && texCoord.y <= 0.2505)
    {
        texColor = sampleSmoothAnimatedTexture(texCoord, 13.0, 12.0, waterFrames, 5.0, true);
    }
    // Check for lava tile (col=13, row=14)
    else if (texCoord.x >= 0.8120 && texCoord.x <= 0.8755 && texCoord.y >= 0.0620 && texCoord.y <= 0.1255)
    {
        texColor = sampleSmoothAnimatedTexture(texCoord, 13.0, 14.0, lavaFrames, 4.0, false);
    }
    else
    {
        texColor = texture(texture0, texCoord);
    }

    if (texColor.a < 0.05)
    {
        discard;
    }

    texColor.rgb *= fragColor;

    // Directional Lighting with N dot L
    vec3 norm = normalize(fragNormal);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * lightColor * lightIntensity;

    // Combine ambient and diffuse
    vec3 totalLight = ambientColor + diffuse;

    // Dynamic Point Lights (Placed Torches, Lava, Handheld Torch)
    for (int i = 0; i < numPointLights; i++)
    {
        vec3 toLight = pointLightPositions[i] - fragWorldPos;
        float distSq = dot(toLight, toLight);
        float pRad = pointLightRadii[i];
        if (distSq < pRad * pRad)
        {
            float pDist = sqrt(distSq);
            float atten = 1.0 - (pDist / pRad);
            atten = atten * atten; // Smooth quadratic falloff
            vec3 lightToFrag = toLight / max(pDist, 0.001);
            float nDotL = max(dot(norm, lightToFrag), 0.35); // Wrap-around diffuse
            totalLight += pointLightColors[i] * (nDotL * atten * pointLightIntensities[i]);
        }
    }

    // Self-luminous blocks (e.g. glowing torches) glow brightly in the dark
    if (fragColor.r > 1.25)
    {
        totalLight = max(totalLight, vec3(1.2));
    }

    // Apply lighting to texture
    vec3 litColor = texColor.rgb * totalLight;

    // Distance Fog
    float dist = length(fragWorldPos - viewPos);
    float fogFactor = clamp((dist - fogStart) / (fogEnd - fogStart), 0.0, 1.0);

    float outAlpha = texColor.a;
    if (isTranslucentPass == 1 && outAlpha >= 0.98)
    {
        outAlpha = 0.65; // Crystal-clear translucent water
    }

    FragColor = mix(vec4(litColor, outAlpha), vec4(fogColor, outAlpha), fogFactor);
}