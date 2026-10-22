using System.Buffers;

namespace VelmoraCraft.src.Graphics;

public class ChunkMesh : IDisposable
{
    private readonly bool isRented;
    private uint[]? indices;
    private ChunkVertex[]? vertices;

    public ChunkMesh(ChunkVertex[] rentedVertices, int vertexCount, uint[] rentedIndices, int indexCount,
        int opaqueIndexCount, int waterIndexCount, bool isRented = true)
    {
        vertices = rentedVertices;
        VertexCount = vertexCount;
        indices = rentedIndices;
        IndexCount = indexCount;
        OpaqueIndexCount = opaqueIndexCount;
        WaterIndexCount = waterIndexCount;
        this.isRented = isRented;
    }

    public ChunkMesh(ChunkVertex[] vertices, uint[] indices)
        : this(vertices, vertices.Length, indices, indices.Length, indices.Length, 0, false)
    {
    }

    public ReadOnlySpan<ChunkVertex> VerticesSpan =>
        vertices != null ? vertices.AsSpan(0, VertexCount) : ReadOnlySpan<ChunkVertex>.Empty;

    public ReadOnlySpan<uint> IndicesSpan => indices != null ? indices.AsSpan(0, IndexCount) : ReadOnlySpan<uint>.Empty;

    public int VertexCount { get; }
    public int IndexCount { get; }
    public int OpaqueIndexCount { get; }
    public int WaterIndexCount { get; }

    public bool IsEmpty => IndexCount == 0;

    public void Dispose()
    {
        if (isRented)
        {
            if (vertices != null)
            {
                ArrayPool<ChunkVertex>.Shared.Return(vertices);
                vertices = null;
            }

            if (indices != null)
            {
                ArrayPool<uint>.Shared.Return(indices);
                indices = null;
            }
        }
    }
}