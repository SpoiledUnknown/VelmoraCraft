using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace VelmoraCraft.src.Graphics;

public class Texture
{
    public Texture(string filePath)
    {
        Handle = GL.GenTexture();
        GL.ActiveTexture(TextureUnit.Texture0);
        Bind();

        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        StbImage.stbi_set_flip_vertically_on_load(1);
        var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures", filePath);
        if (!File.Exists(fullPath)) fullPath = Path.Combine(Directory.GetCurrentDirectory(), "Textures", filePath);
        if (!File.Exists(fullPath)) fullPath = Path.Combine("../../../Textures", filePath);

        var dirtTexture = ImageResult.FromStream(File.OpenRead(fullPath), ColorComponents.RedGreenBlueAlpha);

        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, dirtTexture.Width, dirtTexture.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, dirtTexture.Data);
        UnBind();
    }

    public int Handle { get; }

    public void Bind()
    {
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    private void UnBind()
    {
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Delete()
    {
        GL.DeleteTexture(Handle);
    }
}