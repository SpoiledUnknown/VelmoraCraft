#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTexCoord;
layout (location = 2) in vec3 aColor;
layout (location = 3) in vec3 aNormal;

out vec2 texCoord;
out vec3 fragWorldPos;
out vec3 fragColor;
out vec3 fragNormal;

// Uniform variables
uniform mat4 model;
uniform mat4 viewProj;
uniform float uTime;
uniform float uWindStrength;

bool isFoliageTile(vec2 uv, out float topWeight, out bool isLeaves)
{
	isLeaves = false;
	topWeight = 0.0;
	float u = uv.x;
	float v = uv.y;

	// 1. Leaves: Oak (col 4, row 3) & Spruce (col 5, row 3)
	// Row 3: v in [12/16, 13/16] = [0.75, 0.8125]
	// Col 4..5: u in [4/16, 6/16] = [0.25, 0.375]
	if (u >= 0.249 && u <= 0.376 && v >= 0.749 && v <= 0.8135)
	{
		isLeaves = true;
		topWeight = 1.0;
		return true;
	}

	// 2. Tall Grass: (col 4, row 9)
	// Row 9: v in [6/16, 7/16] = [0.375, 0.4375]
	// Col 4: u in [4/16, 5/16] = [0.25, 0.3125]
	if (u >= 0.249 && u <= 0.3135 && v >= 0.374 && v <= 0.4385)
	{
		topWeight = clamp((v - 0.375) * 16.0, 0.0, 1.0);
		return true;
	}

	// 3. Sugar Cane: (col 9, row 4)
	// Row 4: v in [11/16, 12/16] = [0.6875, 0.75]
	// Col 9: u in [9/16, 10/16] = [0.5625, 0.625]
	if (u >= 0.5615 && u <= 0.626 && v >= 0.6865 && v <= 0.751)
	{
		topWeight = clamp((v - 0.6875) * 16.0, 0.0, 1.0);
		return true;
	}

	// 4. Oak Sapling: (col 15, row 0)
	// Row 0: v in [15/16, 16/16] = [0.9375, 1.0]
	// Col 15: u in [15/16, 16/16] = [0.9375, 1.0]
	if (u >= 0.9365 && u <= 1.001 && v >= 0.9365 && v <= 1.001)
	{
		topWeight = clamp((v - 0.9375) * 16.0, 0.0, 1.0);
		return true;
	}

	// 5. Flowers: Rose (col 12, row 0) & Dandelion (col 13, row 0)
	// Row 0: v in [15/16, 16/16] = [0.9375, 1.0]
	// Col 12..13: u in [12/16, 14/16] = [0.75, 0.875]
	if (u >= 0.749 && u <= 0.876 && v >= 0.9365 && v <= 1.001)
	{
		topWeight = clamp((v - 0.9375) * 16.0, 0.0, 1.0);
		return true;
	}

	// 6. Mushrooms: Red (col 12, row 1) & Brown (col 13, row 1)
	// Row 1: v in [14/16, 15/16] = [0.875, 0.9375]
	// Col 12..13: u in [12/16, 14/16] = [0.75, 0.875]
	if (u >= 0.749 && u <= 0.876 && v >= 0.874 && v <= 0.9385)
	{
		topWeight = clamp((v - 0.875) * 16.0, 0.0, 1.0);
		return true;
	}

	// 7. Wheat Stages (col 7..15, row 5)
	// Row 5: v in [10/16, 11/16] = [0.625, 0.6875]
	// Col 7..15: u in [7/16, 16/16] = [0.4375, 1.0]
	if (u >= 0.4365 && u <= 1.001 && v >= 0.624 && v <= 0.6885)
	{
		topWeight = clamp((v - 0.625) * 16.0, 0.0, 1.0);
		return true;
	}

	return false;
}

void main()
{
	vec4 worldPosition = vec4(aPosition, 1.0) * model;

	if (uWindStrength > 0.0001)
	{
		float topWeight;
		bool isLeaves;
		if (isFoliageTile(aTexCoord, topWeight, isLeaves))
		{
			float t = uTime * 2.4;
			float wave1 = sin(t + worldPosition.x * 0.7 + worldPosition.z * 0.5);
			float wave2 = cos(t * 1.3 + worldPosition.x * 0.4 - worldPosition.z * 0.6);
			float wave3 = sin(t * 0.8 + worldPosition.z * 0.8);

			vec2 windOffset = vec2(
				wave1 * 0.7 + wave2 * 0.3,
				wave2 * 0.6 + wave3 * 0.4
			);

			if (isLeaves)
			{
				float leafRustle = 0.35 * uWindStrength;
				worldPosition.x += (wave1 * 0.6 + wave2 * 0.4) * leafRustle;
				worldPosition.y += (wave3 * 0.3) * leafRustle;
				worldPosition.z += (wave2 * 0.6 - wave1 * 0.4) * leafRustle;
			}
			else
			{
				float stemBend = topWeight * topWeight * uWindStrength;
				worldPosition.x += windOffset.x * stemBend;
				worldPosition.z += windOffset.y * stemBend * 0.8;
				worldPosition.y -= (windOffset.x * windOffset.x + windOffset.y * windOffset.y) * 0.15 * stemBend;
			}
		}
	}

	fragWorldPos = worldPosition.xyz;
	gl_Position = worldPosition * viewProj;
	texCoord = aTexCoord;
	fragColor = aColor;
	fragNormal = mat3(model) * aNormal;
}