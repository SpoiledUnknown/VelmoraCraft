#version 330 core

in vec2 texCoord;
in vec4 vertColor;

out vec4 FragColor;

uniform sampler2D texture0;
uniform int renderMode; // 0 = Colored Flat/Glass, 1 = Textured Quad, 2 = Font/Text

void main()
{
    if (renderMode == 0)
    {
        FragColor = vertColor;
    }
    else if (renderMode == 1)
    {
        vec4 texColor = texture(texture0, texCoord);
        if (texColor.a < 0.05)
        {
            discard;
        }
        FragColor = texColor * vertColor;
    }
    else if (renderMode == 2)
    {
        vec4 texColor = texture(texture0, texCoord);
        float alpha = max(texColor.a, texColor.r);
        if (alpha < 0.1)
        {
            discard;
        }
        FragColor = vec4(vertColor.rgb, vertColor.a * alpha);
    }
    else
    {
        FragColor = vertColor;
    }
}
