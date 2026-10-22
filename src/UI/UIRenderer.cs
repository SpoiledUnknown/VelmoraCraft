using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VelmoraCraft.src.Graphics;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.UI;

[StructLayout(LayoutKind.Sequential)]
public struct UIVertex
{
    public Vector2 Position;
    public Vector2 TexCoord;
    public Vector4 Color;

    public UIVertex(float x, float y, float u, float v, Vector4 color)
    {
        Position = new Vector2(x, y);
        TexCoord = new Vector2(u, v);
        Color = color;
    }
}

public class UIRenderer
{
    private const int MAX_VERTICES = 8192;
    private const int MAX_INDICES = 12288;
    private readonly Texture atlasTexture;
    private readonly uint[] indices = new uint[MAX_INDICES];

    private readonly int projLoc;
    private readonly int renderModeLoc;
    private readonly ShaderProgram shader;
    private readonly int texture0Loc;

    private readonly UIVertex[] vertices = new UIVertex[MAX_VERTICES];

    private int currentRenderMode = -1; // 0 = Flat, 1 = Atlas, 2 = Font
    private int currentTextureId = -1;
    private int ebo;
    private int indexCount;

    private int vao;
    private int vbo;

    private int vertexCount;

    public UIRenderer(Texture atlas)
    {
        atlasTexture = atlas;
        shader = new ShaderProgram("UI.vert", "UI.frag");
        Font = new FontRenderer();

        projLoc = GL.GetUniformLocation(shader.ID, "projection");
        renderModeLoc = GL.GetUniformLocation(shader.ID, "renderMode");
        texture0Loc = GL.GetUniformLocation(shader.ID, "texture0");

        SetupBuffers();
    }

    public FontRenderer Font { get; }

    private void SetupBuffers()
    {
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, MAX_VERTICES * Marshal.SizeOf<UIVertex>(), IntPtr.Zero,
            BufferUsage.DynamicDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, MAX_INDICES * sizeof(uint), IntPtr.Zero,
            BufferUsage.DynamicDraw);

        var stride = Marshal.SizeOf<UIVertex>();

        // Location 0: aPosition (vec2)
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);

        // Location 1: aTexCoord (vec2)
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        // Location 2: aColor (vec4)
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        GL.BindVertexArray(0);
    }

    public void Begin(int screenWidth, int screenHeight)
    {
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        shader.Bind();

        var ortho = Matrix4.CreateOrthographicOffCenter(0, screenWidth, screenHeight, 0, -1.0f, 1.0f);
        GL.UniformMatrix4f(projLoc, 1, true, ref ortho);
        GL.Uniform1i(texture0Loc, 0);

        vertexCount = 0;
        indexCount = 0;
        currentRenderMode = -1;
        currentTextureId = -1;
    }

    public void End()
    {
        Flush();

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.Disable(EnableCap.Blend);
        GL.BindVertexArray(0);
    }

    private void SetRenderState(int mode, int textureHandle)
    {
        if (currentRenderMode != mode || currentTextureId != textureHandle)
        {
            Flush();
            currentRenderMode = mode;
            currentTextureId = textureHandle;

            GL.Uniform1i(renderModeLoc, currentRenderMode);
            if (currentTextureId != 0)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, currentTextureId);
            }
        }
    }

    public void Flush()
    {
        if (vertexCount == 0 || indexCount == 0) return;

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertexCount * Marshal.SizeOf<UIVertex>(), vertices);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, indexCount * sizeof(uint), indices);

        GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);

        vertexCount = 0;
        indexCount = 0;
    }

    private void AddQuad(float x, float y, float w, float h, Vector2 uv0, Vector2 uv1, Vector4 color)
    {
        if (vertexCount + 4 >= MAX_VERTICES || indexCount + 6 >= MAX_INDICES) Flush();

        var baseIdx = (uint)vertexCount;

        // 0: Top-Left, 1: Top-Right, 2: Bottom-Right, 3: Bottom-Left
        vertices[vertexCount++] = new UIVertex(x, y, uv0.X, uv1.Y, color);
        vertices[vertexCount++] = new UIVertex(x + w, y, uv1.X, uv1.Y, color);
        vertices[vertexCount++] = new UIVertex(x + w, y + h, uv1.X, uv0.Y, color);
        vertices[vertexCount++] = new UIVertex(x, y + h, uv0.X, uv0.Y, color);

        indices[indexCount++] = baseIdx + 0;
        indices[indexCount++] = baseIdx + 1;
        indices[indexCount++] = baseIdx + 2;
        indices[indexCount++] = baseIdx + 2;
        indices[indexCount++] = baseIdx + 3;
        indices[indexCount++] = baseIdx + 0;
    }
    // Flat & Semi-Transparent Primitive Drawing

    public void DrawQuad(float x, float y, float w, float h, Vector4 color)
    {
        SetRenderState(0, 0);
        AddQuad(x, y, w, h, Vector2.Zero, Vector2.One, color);
    }

    public void DrawBorder(float x, float y, float w, float h, float thickness, Vector4 color)
    {
        // Top
        DrawQuad(x, y, w, thickness, color);
        // Bottom
        DrawQuad(x, y + h - thickness, w, thickness, color);
        // Left
        DrawQuad(x, y + thickness, thickness, h - 2 * thickness, color);
        // Right
        DrawQuad(x + w - thickness, y + thickness, thickness, h - 2 * thickness, color);
    }

    public void DrawPanel(float x, float y, float w, float h, Vector4 bgColor, Vector4 borderColor,
        float borderWidth = 1.5f)
    {
        // Panel Body
        DrawQuad(x, y, w, h, bgColor);
        // Panel Border
        DrawBorder(x, y, w, h, borderWidth, borderColor);
    }

    public void DrawProgressBar(float x, float y, float w, float h, float progress, Vector4 fillColor, Vector4 bgColor)
    {
        progress = Math.Clamp(progress, 0.0f, 1.0f);
        DrawQuad(x, y, w, h, bgColor);
        DrawBorder(x, y, w, h, 1.0f, new Vector4(0.3f, 0.35f, 0.45f, 0.8f));
        if (progress > 0.001f) DrawQuad(x + 1, y + 1, (w - 2) * progress, h - 2, fillColor);
    }
    // Item & Atlas Textured Quad Drawing

    public void DrawTexturedQuad(float x, float y, float w, float h, Vector2 uvMin, Vector2 uvMax, Vector4? tint = null)
    {
        SetRenderState(1, atlasTexture.Handle);
        AddQuad(x, y, w, h, uvMin, uvMax, tint ?? Vector4.One);
    }

    public void DrawItemIcon(BlockType type, float x, float y, float size)
    {
        if (type == BlockType.Empty) return;

        var def = BlockRegistry.Get(type);
        var atlasCoord = def.GetFaceAtlasCoord(Face.Front);
        if (type == BlockType.Cake) atlasCoord = new Vector2(12, 8);

        const float ATLAS_SIZE = 16f;
        var u0 = atlasCoord.X / ATLAS_SIZE;
        var u1 = (atlasCoord.X + 1f) / ATLAS_SIZE;
        var v0 = 1.0f - (atlasCoord.Y + 1f) / ATLAS_SIZE;
        var v1 = 1.0f - atlasCoord.Y / ATLAS_SIZE;

        var tint = def.TintColor;
        if (type == BlockType.Coal) tint = new Vector3(0.2f, 0.2f, 0.2f);
        if (type == BlockType.IronIngot) tint = new Vector3(1.15f, 1.15f, 1.2f);
        if (type == BlockType.FlintAndSteel) tint = new Vector3(0.9f, 0.95f, 1.0f);
        if (type == BlockType.Bread) tint = new Vector3(1.4f, 0.95f, 0.50f);

        DrawTexturedQuad(x, y, size, size, new Vector2(u0, v0), new Vector2(u1, v1),
            new Vector4(tint.X, tint.Y, tint.Z, 1.0f));
    }

    public void DrawSlot(float x, float y, float size, ItemStack stack, bool isSelected = false, bool isHovered = false)
    {
        // Dark inset background with transparency
        var bg = isHovered
            ? new Vector4(0.22f, 0.24f, 0.28f, 0.90f)
            : new Vector4(0.12f, 0.13f, 0.16f, 0.85f);

        Vector4 border;
        var bWidth = 1.0f;
        if (isSelected)
        {
            border = new Vector4(0.35f, 0.72f, 1.0f, 0.95f); // Vibrant cyan/blue selection
            bWidth = 2.0f;
        }
        else if (isHovered)
        {
            border = new Vector4(0.85f, 0.88f, 0.95f, 0.75f);
        }
        else
        {
            border = new Vector4(0.25f, 0.28f, 0.35f, 0.65f);
        }

        DrawPanel(x, y, size, size, bg, border, bWidth);

        // Item Icon
        if (!stack.IsEmpty)
        {
            var padding = 4.0f;
            DrawItemIcon(stack.Type, x + padding, y + padding, size - padding * 2.0f);

            // Stack count number
            if (stack.Count > 1)
            {
                var countStr = stack.Count.ToString();
                var numScale = 1.25f;
                var textWidth = countStr.Length * 8f * numScale;
                var textX = x + size - textWidth - 3.0f;
                var textY = y + size - 8f * numScale - 3.0f;
                DrawText(countStr, textX, textY, numScale, new Vector4(1f, 1f, 1f, 1f));
            }
        }
    }
    // Text & Font Rendering

    public void DrawText(string text, float x, float y, float scale = 1.0f, Vector4? color = null, bool shadow = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var textColor = color ?? Vector4.One;

        // Draw shadow for ultra-crisp visibility
        if (shadow)
        {
            var shadowColor = new Vector4(0.05f, 0.05f, 0.05f, 0.85f);
            DrawTextInternal(text, x + 1.2f * scale, y + 1.2f * scale, scale, shadowColor);
        }

        DrawTextInternal(text, x, y, scale, textColor);
    }

    private void DrawTextInternal(string text, float x, float y, float scale, Vector4 color)
    {
        SetRenderState(2, Font.TextureID);

        var curX = x;
        var charW = 8f * scale;
        var charH = 8f * scale;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                y += 10f * scale;
                curX = x;
                continue;
            }

            var (uvMin, uvMax) = Font.GetCharUV(c);
            AddQuad(curX, y, charW, charH, uvMin, uvMax, color);
            curX += charW;
        }
    }

    public Vector2 MeasureText(string text, float scale = 1.0f)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        return new Vector2(text.Length * 8f * scale, 8f * scale);
    }
    // Crosshair & Tooltips

    public void DrawCrosshair(float centerX, float centerY, float size = 9f, float thickness = 2f)
    {
        var color = new Vector4(1.0f, 1.0f, 1.0f, 0.85f);
        var shadow = new Vector4(0.0f, 0.0f, 0.0f, 0.6f);

        // Shadow
        DrawQuad(centerX - size - 1, centerY - thickness * 0.5f - 1, size * 2 + 2, thickness + 2, shadow);
        DrawQuad(centerX - thickness * 0.5f - 1, centerY - size - 1, thickness + 2, size * 2 + 2, shadow);

        // Horizontal line
        DrawQuad(centerX - size, centerY - thickness * 0.5f, size * 2, thickness, color);
        // Vertical line
        DrawQuad(centerX - thickness * 0.5f, centerY - size, thickness, size * 2, color);
    }

    public void DrawTooltip(string text, float mouseX, float mouseY, int screenWidth, int screenHeight)
    {
        if (string.IsNullOrEmpty(text)) return;

        var scale = 1.2f;
        var size = MeasureText(text, scale);
        var padding = 6.0f;
        var w = size.X + padding * 2.0f;
        var h = size.Y + padding * 2.0f;

        var x = mouseX + 12.0f;
        var y = mouseY + 12.0f;

        // Screen boundary check
        if (x + w > screenWidth - 10) x = mouseX - w - 4.0f;
        if (y + h > screenHeight - 10) y = mouseY - h - 4.0f;

        DrawPanel(x, y, w, h, new Vector4(0.08f, 0.09f, 0.12f, 0.95f), new Vector4(0.4f, 0.45f, 0.55f, 0.85f));
        DrawText(text, x + padding, y + padding, scale, new Vector4(0.95f, 0.95f, 0.95f, 1.0f));
    }

    public void Delete()
    {
        shader.Delete();
        Font.Delete();
        if (vao != 0) GL.DeleteVertexArray(vao);
        if (vbo != 0) GL.DeleteBuffer(vbo);
        if (ebo != 0) GL.DeleteBuffer(ebo);
    }
}