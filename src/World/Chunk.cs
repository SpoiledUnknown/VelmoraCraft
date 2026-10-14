using OpenTK.Mathematics;
using VoxelRenderEngine.src.Graphics;

namespace VoxelRenderEngine.src.World
{
    public class Chunk
    {
        public const int SIZE_X = 16;
        public const int SIZE_Y = 128;
        public const int SIZE_Z = 16;

        public Vector2i ChunkCoord { get; }
        public Vector3 Position { get; }

        // Flat array indexed as: x + (z * SIZE_X) + (y * SIZE_X * SIZE_Z)
        // 16 * 128 * 16 bytes = 32,768 bytes (32 KB per chunk)
        public byte[] Blocks { get; } = new byte[SIZE_X * SIZE_Y * SIZE_Z];

        public bool IsMeshDirty { get; set; } = true;
        public bool IsGeneratingMesh { get; set; } = false;
        public bool IsTerrainGenerated { get; set; } = false;
        public bool IsDecorated { get; set; } = false;
        public bool HasMesh => indexCount > 0 && chunkVAO != null && ibo != null;

        private VertexArrayObject? chunkVAO;
        private VertexBufferObject? vertexVBO;
        private VertexBufferObject? uvVBO;
        private VertexBufferObject? colorVBO;
        private VertexBufferObject? normalVBO;
        private IndexBufferObject? ibo;
        private int indexCount = 0;

        public Chunk(Vector2i coord)
        {
            ChunkCoord = coord;
            Position = new Vector3(coord.X * SIZE_X, 0, coord.Y * SIZE_Z);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static int GetBlockIndex(int x, int y, int z)
        {
            return x + (z * SIZE_X) + (y * SIZE_X * SIZE_Z);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public byte GetBlock(int x, int y, int z)
        {
            if (x < 0 || x >= SIZE_X || y < 0 || y >= SIZE_Y || z < 0 || z >= SIZE_Z)
            {
                return (byte)BlockType.Empty;
            }

            return Blocks[GetBlockIndex(x, y, z)];
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (x >= 0 && x < SIZE_X && y >= 0 && y < SIZE_Y && z >= 0 && z < SIZE_Z)
            {
                Blocks[GetBlockIndex(x, y, z)] = (byte)type;
                IsMeshDirty = true;
            }
        }

        // Upload mesh geometry to OpenGL GPU buffers (called on main thread)
        public void UploadMesh(ChunkMesh mesh)
        {
            // Clean up old GPU buffers if they exist
            DeleteGLBuffers();

            indexCount = mesh.Indices.Length;
            if (indexCount == 0) return;

            chunkVAO = new VertexArrayObject();
            chunkVAO.Bind();

            vertexVBO = new VertexBufferObject(mesh.Vertices.AsSpan());
            vertexVBO.Bind();
            chunkVAO.LinkToVAO(0, 3, vertexVBO);

            uvVBO = new VertexBufferObject(mesh.UVs.AsSpan());
            uvVBO.Bind();
            chunkVAO.LinkToVAO(1, 2, uvVBO);

            colorVBO = new VertexBufferObject(mesh.Colors.AsSpan());
            colorVBO.Bind();
            chunkVAO.LinkToVAO(2, 3, colorVBO);

            normalVBO = new VertexBufferObject(mesh.Normals.AsSpan());
            normalVBO.Bind();
            chunkVAO.LinkToVAO(3, 3, normalVBO);

            ibo = new IndexBufferObject(mesh.Indices.AsSpan());

            IsMeshDirty = false;
        }

        public void Render()
        {
            if (indexCount == 0 || chunkVAO == null || ibo == null) return;

            chunkVAO.Bind();
            ibo.Bind();
            OpenTK.Graphics.OpenGL.GL.DrawElements(
                OpenTK.Graphics.OpenGL.PrimitiveType.Triangles,
                indexCount,
                OpenTK.Graphics.OpenGL.DrawElementsType.UnsignedInt,
                0
            );
        }

        public void DeleteGLBuffers()
        {
            chunkVAO?.Delete();
            vertexVBO?.Delete();
            uvVBO?.Delete();
            colorVBO?.Delete();
            normalVBO?.Delete();
            ibo?.Delete();

            chunkVAO = null;
            vertexVBO = null;
            uvVBO = null;
            colorVBO = null;
            normalVBO = null;
            ibo = null;
            indexCount = 0;
        }
    }
}
