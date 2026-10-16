using OpenTK.Graphics.OpenGL;
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
        public bool HasMesh => (opaqueIndexCount > 0 || waterIndexCount > 0) && chunkVAO != null && ibo != null;
        public bool HasOpaqueMesh => opaqueIndexCount > 0 && chunkVAO != null && ibo != null;
        public bool HasWaterMesh => waterIndexCount > 0 && chunkVAO != null && ibo != null;
        public int OpaqueIndexCount => opaqueIndexCount;
        public int WaterIndexCount => waterIndexCount;

        private VertexArrayObject? chunkVAO;
        private VertexBufferObject? vbo;
        private IndexBufferObject? ibo;
        private int indexCount = 0;
        private int opaqueIndexCount = 0;
        private int waterIndexCount = 0;

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

            indexCount = mesh.IndexCount;
            opaqueIndexCount = mesh.OpaqueIndexCount;
            waterIndexCount = mesh.WaterIndexCount;
            if (indexCount == 0) return;

            chunkVAO = new VertexArrayObject();
            chunkVAO.Bind();

            vbo = new VertexBufferObject(mesh.VerticesSpan);
            vbo.Bind();

            // Link interleaved vertex attributes: Pos (loc 0), UV (loc 1), Color (loc 2), Normal (loc 3)
            chunkVAO.LinkToVAO(0, 3, ChunkVertex.SizeInBytes, 0);   // Position (vec3)
            chunkVAO.LinkToVAO(1, 2, ChunkVertex.SizeInBytes, 12);  // TexCoord (vec2)
            chunkVAO.LinkToVAO(2, 3, ChunkVertex.SizeInBytes, 20);  // Color (vec3)
            chunkVAO.LinkToVAO(3, 3, ChunkVertex.SizeInBytes, 32);  // Normal (vec3)

            ibo = new IndexBufferObject(mesh.IndicesSpan);

            IsMeshDirty = false;
        }

        public void RenderOpaque()
        {
            if (opaqueIndexCount == 0 || chunkVAO == null || ibo == null) return;

            chunkVAO.Bind();
            ibo.Bind();
            GL.DrawElements(
                PrimitiveType.Triangles,
                opaqueIndexCount,
                DrawElementsType.UnsignedInt,
                0
            );
        }

        public void RenderWater()
        {
            if (waterIndexCount == 0 || chunkVAO == null || ibo == null) return;

            chunkVAO.Bind();
            ibo.Bind();
            GL.DrawElements(
                PrimitiveType.Triangles,
                waterIndexCount,
                DrawElementsType.UnsignedInt,
                (IntPtr)(opaqueIndexCount * sizeof(uint))
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
            opaqueIndexCount = 0;
            waterIndexCount = 0;
        }
    }
}
