using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace VoxelRenderEngine.src.Graphics
{
    internal class VertexBufferObject
    {
        private int ID;

        public VertexBufferObject(List<Vector3> data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Count * Vector3.SizeInBytes, data.ToArray(), BufferUsageHint.StaticDraw);
        }

        public VertexBufferObject(List<Vector2> data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Count * Vector2.SizeInBytes, data.ToArray(), BufferUsageHint.StaticDraw);
        }

        public void Bind() { GL.BindBuffer(BufferTarget.ArrayBuffer, ID); }
        public void UnBind() { GL.BindBuffer(BufferTarget.ArrayBuffer, 0); }
        public void Delete() { GL.DeleteBuffer(ID); }
    }
}
