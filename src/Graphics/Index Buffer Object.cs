using OpenTK.Graphics.OpenGL4;

namespace Minecraft.src.Graphics
{
    internal class IndexBufferObject
    {
        private int ID;
        public IndexBufferObject(List<uint> data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ID);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.Count * sizeof(uint), data.ToArray(), BufferUsageHint.StaticDraw);
        }

        public void Bind() { GL.BindBuffer(BufferTarget.ElementArrayBuffer, ID); }
        public void Delete() { GL.DeleteBuffer(ID); }
    }
}
