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

void main()
{
	vec4 texColor = texture(texture0, texCoord);
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
		float pDist = length(pointLightPositions[i] - fragWorldPos);
		float pRad = pointLightRadii[i];
		if (pDist < pRad)
		{
			float atten = clamp(1.0 - (pDist / pRad), 0.0, 1.0);
			atten = atten * atten; // Smooth quadratic falloff
			vec3 lightToFrag = normalize(pointLightPositions[i] - fragWorldPos);
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

	FragColor = mix(vec4(litColor, texColor.a), vec4(fogColor, 1.0), fogFactor);
}