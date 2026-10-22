using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VelmoraCraft.src.Graphics;

namespace VelmoraCraft.src.World;

public class Chunk
{
    public const int SIZE_X = 16;
    public const int SIZE_Y = 128;
    public const int SIZE_Z = 16;

    public readonly List<Vector3i> LightPositions = new();
    private readonly Dictionary<int, byte> blockMetadata = new();

    private VertexArrayObject? chunkVAO;
    private IndexBufferObject? ibo;
    private int indexCount;
    private VertexBufferObject? vbo;

    public Chunk(Vector2i coord)
    {
        ChunkCoord = coord;
        Position = new Vector3(coord.X * SIZE_X, 0, coord.Y * SIZE_Z);
    }

    public Vector2i ChunkCoord { get; }
    public Vector3 Position { get; }

    // Flat array indexed as: x + (z * SIZE_X) + (y * SIZE_X * SIZE_Z)
    // 16 * 128 * 16 bytes = 32,768 bytes (32 KB per chunk)
    public byte[] Blocks { get; } = new byte[SIZE_X * SIZE_Y * SIZE_Z];

    public bool IsMeshDirty { get; set; } = true;
    public bool IsGeneratingMesh { get; set; } = false;
    public bool NeedsRemesh { get; set; } = false;
    public bool IsTerrainGenerated { get; set; } = false;
    public bool IsDecorated { get; set; } = false;
    public bool HasMesh => (OpaqueIndexCount > 0 || WaterIndexCount > 0) && chunkVAO != null && ibo != null;
    public bool HasOpaqueMesh => OpaqueIndexCount > 0 && chunkVAO != null && ibo != null;
    public bool HasWaterMesh => WaterIndexCount > 0 && chunkVAO != null && ibo != null;
    public int OpaqueIndexCount { get; private set; }

    public int WaterIndexCount { get; private set; }

    public Vector3 MinBounds => Position;
    public Vector3 MaxBounds => new(Position.X + SIZE_X, SIZE_Y, Position.Z + SIZE_Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBlockIndex(int x, int y, int z)
    {
        return x + z * SIZE_X + y * SIZE_X * SIZE_Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= SIZE_X || y < 0 || y >= SIZE_Y || z < 0 || z >= SIZE_Z) return (byte)BlockType.Empty;

        return Blocks[GetBlockIndex(x, y, z)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x >= 0 && x < SIZE_X && y >= 0 && y < SIZE_Y && z >= 0 && z < SIZE_Z)
        {
            var idx = GetBlockIndex(x, y, z);
            Blocks[idx] = (byte)type;
            if (type == BlockType.Empty) blockMetadata.Remove(idx);
            IsMeshDirty = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetMetadata(int x, int y, int z)
    {
        if (x < 0 || x >= SIZE_X || y < 0 || y >= SIZE_Y || z < 0 || z >= SIZE_Z) return 0;
        var idx = GetBlockIndex(x, y, z);
        return blockMetadata.TryGetValue(idx, out var meta) ? meta : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMetadata(int x, int y, int z, byte data)
    {
        if (x >= 0 && x < SIZE_X && y >= 0 && y < SIZE_Y && z >= 0 && z < SIZE_Z)
        {
            var idx = GetBlockIndex(x, y, z);
            if (data == 0)
                blockMetadata.Remove(idx);
            else
                blockMetadata[idx] = data;
            IsMeshDirty = true;
        }
    }

    // Upload mesh geometry to OpenGL GPU buffers (called on main thread)
    public void UploadMesh(ChunkMesh mesh)
    {
        // Clean up old GPU buffers if they exist
        DeleteGLBuffers();

        indexCount = mesh.IndexCount;
        OpaqueIndexCount = mesh.OpaqueIndexCount;
        WaterIndexCount = mesh.WaterIndexCount;
        if (indexCount == 0) return;

        chunkVAO = new VertexArrayObject();
        chunkVAO.Bind();

        vbo = new VertexBufferObject(mesh.VerticesSpan);
        vbo.Bind();

        // Link interleaved vertex attributes: Pos (loc 0), UV (loc 1), Color (loc 2), Normal (loc 3), Sway (loc 4)
        chunkVAO.LinkToVAO(0, 3, ChunkVertex.SizeInBytes, 0); // Position (vec3)
        chunkVAO.LinkToVAO(1, 2, ChunkVertex.SizeInBytes, 12); // TexCoord (vec2)
        chunkVAO.LinkToVAO(2, 3, ChunkVertex.SizeInBytes, 20); // Color (vec3)
        chunkVAO.LinkToVAO(3, 3, ChunkVertex.SizeInBytes, 32); // Normal (vec3)
        chunkVAO.LinkToVAO(4, 1, ChunkVertex.SizeInBytes, 44); // SwayWeight (float)

        ibo = new IndexBufferObject(mesh.IndicesSpan);

        IsMeshDirty = false;
    }

    public void RenderOpaque()
    {
        if (OpaqueIndexCount == 0 || chunkVAO == null) return;

        chunkVAO.Bind();
        GL.DrawElements(
            PrimitiveType.Triangles,
            OpaqueIndexCount,
            DrawElementsType.UnsignedInt,
            0
        );
    }

    public void RenderWater()
    {
        if (WaterIndexCount == 0 || chunkVAO == null) return;

        chunkVAO.Bind();
        GL.DrawElements(
            PrimitiveType.Triangles,
            WaterIndexCount,
            DrawElementsType.UnsignedInt,
            OpaqueIndexCount * sizeof(uint)
        );
    }

    public void Render()
    {
        RenderOpaque();
        RenderWater();
    }

    public void DeleteGLBuffers()
    {
        chunkVAO?.Delete();
        vbo?.Delete();
        ibo?.Delete();

        chunkVAO = null;
        vbo = null;
        ibo = null;
        indexCount = 0;
        OpaqueIndexCount = 0;
        WaterIndexCount = 0;
        LightPositions.Clear();
    }
}