using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StbImageSharp;

namespace Minecraft.src.Graphics
{
    internal class Texture
    {
        private int ID;

        public Texture(string filePath)
        {
            ID = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            Bind();

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResult dirtTexture = ImageResult.FromStream(File.OpenRead("../../../Textures/" + filePath), ColorComponents.RedGreenBlueAlpha);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, dirtTexture.Width, dirtTexture.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, dirtTexture.Data);
            UnBind();
        }

        public void Bind() { GL.BindTexture(TextureTarget.Texture2D, ID); }
        private void UnBind() { GL.BindTexture(TextureTarget.Texture2D, 0); }
        public void Delete() { GL.DeleteTexture(ID); } 
    }
}
