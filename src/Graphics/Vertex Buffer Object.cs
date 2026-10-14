using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace VoxelRenderEngine.src.Graphics
{
    internal class VertexBufferObject
    {
        private int ID;

        public VertexBufferObject(List<Vector3> data) : this(data.ToArray())
        {
        }

        public VertexBufferObject(ReadOnlySpan<Vector3> data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * Vector3.SizeInBytes, data, BufferUsage.StaticDraw);
        }

        public VertexBufferObject(List<Vector2> data) : this(data.ToArray())
        {
        }

        public VertexBufferObject(ReadOnlySpan<Vector2> data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * Vector2.SizeInBytes, data, BufferUsage.StaticDraw);
        }

        public void Bind() { GL.BindBuffer(BufferTarget.ArrayBuffer, ID); }
        public void UnBind() { GL.BindBuffer(BufferTarget.ArrayBuffer, 0); }
        public void Delete() { GL.DeleteBuffer(ID); }
    }
}
