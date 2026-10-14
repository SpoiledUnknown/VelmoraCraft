#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;
layout (location = 2) in vec4 aColor;

out vec2 texCoord;
out vec4 vertColor;

uniform mat4 projection;

void main()
{
    gl_Position = vec4(aPosition, 0.0, 1.0) * projection;
    texCoord = aTexCoord;
    vertColor = aColor;
}
