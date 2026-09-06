using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoxelRenderEngine.src.Graphics
{
    internal class VertexArrayObject
    {
        private int ID;
        public VertexArrayObject()
        {
            ID = GL.GenVertexArray();
            GL.BindVertexArray(ID);
        }

        public void LinkToVAO(int location, int size, VertexBufferObject vertexBufferObject)
        {
            Bind();
            vertexBufferObject.Bind();
            GL.VertexAttribPointer(location, size, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(location);
            vertexBufferObject.UnBind();
            UnBind();
        }

        public void Bind() { GL.BindVertexArray(ID); }
        private void UnBind() { GL.BindVertexArray(0); }
        public void Delete() { GL.DeleteVertexArray(ID); }
    }
}
