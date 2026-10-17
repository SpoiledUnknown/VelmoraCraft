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

	if (uWindStrength > 0.0001 && aSwayWeight > 0.001)
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