using OpenTK.Graphics.OpenGL;

namespace VoxelRenderEngine.src.Graphics
{
    internal class IndexBufferObject
    {
        private int ID;
        public IndexBufferObject(List<uint> data) : this(data.ToArray())
        {
        }

        public IndexBufferObject(ReadOnlySpan<uint> data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ID);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.Length * sizeof(uint), data, BufferUsage.StaticDraw);
        }

        public void Bind() { GL.BindBuffer(BufferTarget.ElementArrayBuffer, ID); }
        public void Delete() { GL.DeleteBuffer(ID); }
    }
}
