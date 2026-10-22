using OpenTK.Graphics.OpenGL;

namespace VelmoraCraft.src.Graphics;

internal class VertexArrayObject
{
    private readonly int ID;

    public VertexArrayObject()
    {
        ID = GL.GenVertexArray();
        GL.BindVertexArray(ID);
    }

    public void LinkToVAO(int location, int size, VertexBufferObject vertexBufferObject)
    {
        Bind();
        vertexBufferObject.Bind();
        GL.VertexAttribPointer((uint)location, size, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
        GL.EnableVertexAttribArray((uint)location);
        vertexBufferObject.UnBind();
        UnBind();
    }

    public void LinkToVAO(int location, int size, int stride, int offset)
    {
        GL.VertexAttribPointer((uint)location, size, VertexAttribPointerType.Float, false, stride, offset);
        GL.EnableVertexAttribArray((uint)location);
    }

    public void Bind()
    {
        GL.BindVertexArray(ID);
    }

    private void UnBind()
    {
        GL.BindVertexArray(0);
    }

    public void Delete()
    {
        GL.DeleteVertexArray(ID);
    }
}