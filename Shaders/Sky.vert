#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTexCoord;
layout (location = 2) in vec3 aColor;

out vec2 texCoord;
out vec3 fragWorldPos;
out vec3 vertColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    vec4 worldPosition = vec4(aPosition, 1.0) * model;
    fragWorldPos = worldPosition.xyz;
    gl_Position = worldPosition * view * projection;
    texCoord = aTexCoord;
    vertColor = aColor;
}
