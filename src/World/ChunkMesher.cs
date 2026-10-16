using OpenTK.Mathematics;
using VoxelRenderEngine.src.Graphics;

namespace VoxelRenderEngine.src.World
{
    public class ChunkMeshBuilder
    {
        public readonly List<ChunkVertex> Vertices = new List<ChunkVertex>(8192);
        public readonly List<uint> Indices = new List<uint>(12288);
        public uint VertexOffset = 0;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void AddQuad(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
            float u0, float v0, float u1, float v1,
            Vector3 tint, Vector3 normal)
        {
            uint b = VertexOffset;

            Vertices.Add(new ChunkVertex(p0.X, p0.Y, p0.Z, u0, v1, tint.X, tint.Y, tint.Z, normal.X, normal.Y, normal.Z));
            Vertices.Add(new ChunkVertex(p1.X, p1.Y, p1.Z, u1, v1, tint.X, tint.Y, tint.Z, normal.X, normal.Y, normal.Z));
            Vertices.Add(new ChunkVertex(p2.X, p2.Y, p2.Z, u1, v0, tint.X, tint.Y, tint.Z, normal.X, normal.Y, normal.Z));
            Vertices.Add(new ChunkVertex(p3.X, p3.Y, p3.Z, u0, v0, tint.X, tint.Y, tint.Z, normal.X, normal.Y, normal.Z));

            Indices.Add(b + 0);
            Indices.Add(b + 1);
            Indices.Add(b + 2);
            Indices.Add(b + 2);
            Indices.Add(b + 3);
            Indices.Add(b + 0);

            VertexOffset += 4;
        }

        public void Clear()
        {
            Vertices.Clear();
            Indices.Clear();
            VertexOffset = 0;
        }

        public ChunkMesh Build(int opaqueIndexCount, int waterIndexCount)
        {
            int vCount = Vertices.Count;
            int iCount = Indices.Count;

            if (iCount == 0)
            {
                return new ChunkMesh(Array.Empty<ChunkVertex>(), 0, Array.Empty<uint>(), 0, 0, 0, false);
            }

            var rentedVertices = System.Buffers.ArrayPool<ChunkVertex>.Shared.Rent(vCount);
            var rentedIndices = System.Buffers.ArrayPool<uint>.Shared.Rent(iCount);

            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Vertices).CopyTo(rentedVertices.AsSpan(0, vCount));
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Indices).CopyTo(rentedIndices.AsSpan(0, iCount));

            return new ChunkMesh(rentedVertices, vCount, rentedIndices, iCount, opaqueIndexCount, waterIndexCount, true);
        }
    }

    public static class ChunkMesher
    {
        private const float ATLAS_SIZE = 16f;
        private static readonly ThreadLocal<ChunkMeshBuilder> threadLocalBuilder = new ThreadLocal<ChunkMeshBuilder>(() => new ChunkMeshBuilder());

        public static ChunkMesh BuildMesh(Chunk chunk, Chunk? leftChunk, Chunk? rightChunk, Chunk? frontChunk, Chunk? backChunk)
        {
            var builder = threadLocalBuilder.Value!;
            builder.Clear();

            float chunkWorldX = chunk.Position.X;
            float chunkWorldZ = chunk.Position.Z;

            // PASS 1: Opaque blocks (all solid blocks, terrain, seabed, etc.)
            for (int x = 0; x < Chunk.SIZE_X; x++)
            {
                for (int y = 0; y < Chunk.SIZE_Y; y++)
                {
                    for (int z = 0; z < Chunk.SIZE_Z; z++)
                    {
                        byte blockId = chunk.GetBlock(x, y, z);
                        if (blockId == (byte)BlockType.Empty) continue;
                        if (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Portal) continue;

                        AddBlockMesh(chunk, leftChunk, rightChunk, frontChunk, backChunk, x, y, z, blockId, chunkWorldX, chunkWorldZ, builder);
                    }
                }
            }

            int opaqueIndexCount = builder.Indices.Count;

            // PASS 2: Translucent blocks (Water & Portal)
            for (int x = 0; x < Chunk.SIZE_X; x++)
            {
                for (int y = 0; y < Chunk.SIZE_Y; y++)
                {
                    for (int z = 0; z < Chunk.SIZE_Z; z++)
                    {
                        byte blockId = chunk.GetBlock(x, y, z);
                        if (blockId != (byte)BlockType.Water && blockId != (byte)BlockType.Portal) continue;

                        AddBlockMesh(chunk, leftChunk, rightChunk, frontChunk, backChunk, x, y, z, blockId, chunkWorldX, chunkWorldZ, builder);
                    }
                }
            }

            int waterIndexCount = builder.Indices.Count - opaqueIndexCount;

            return builder.Build(opaqueIndexCount, waterIndexCount);
        }

        private static void AddBlockMesh(
            Chunk chunk,
            Chunk? leftChunk,
            Chunk? rightChunk,
            Chunk? frontChunk,
            Chunk? backChunk,
            int x,
            int y,
            int z,
            byte blockId,
            float chunkWorldX,
            float chunkWorldZ,
            ChunkMeshBuilder builder)
        {
            var blockDef = BlockRegistry.Get(blockId);
            float bx = chunkWorldX + x;
            float by = y;
            float bz = chunkWorldZ + z;

            if (blockDef.MeshType == MeshType.StandardCube)
            {
                // 1. Front (+Z)
                if (ShouldRenderFace(chunk, frontChunk, x, y, z + 1, 0, 1, blockId))
                {
                    AddFace(Face.Front, bx, by, bz, blockDef, builder);
                }

                // 2. Back (-Z)
                if (ShouldRenderFace(chunk, backChunk, x, y, z - 1, 0, -1, blockId))
                {
                    AddFace(Face.Back, bx, by, bz, blockDef, builder);
                }

                // 3. Left (-X)
                if (ShouldRenderFace(chunk, leftChunk, x - 1, y, z, -1, 0, blockId))
                {
                    AddFace(Face.Left, bx, by, bz, blockDef, builder);
                }

                // 4. Right (+X)
                if (ShouldRenderFace(chunk, rightChunk, x + 1, y, z, 1, 0, blockId))
                {
                    AddFace(Face.Right, bx, by, bz, blockDef, builder);
                }

                // 5. Top (+Y)
                byte topBlock = y + 1 < Chunk.SIZE_Y ? chunk.GetBlock(x, y + 1, z) : (byte)BlockType.Empty;
                if ((y + 1 >= Chunk.SIZE_Y || BlockRegistry.IsTransparent(topBlock)) && !(topBlock == blockId && (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Lava)))
                {
                    AddFace(Face.Top, bx, by, bz, blockDef, builder);
                }

                // 6. Bottom (-Y)
                byte bottomBlock = y - 1 >= 0 ? chunk.GetBlock(x, y - 1, z) : (byte)BlockType.Empty;
                if ((y - 1 < 0 || BlockRegistry.IsTransparent(bottomBlock)) && !(bottomBlock == blockId && (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Lava)))
                {
                    AddFace(Face.Bottom, bx, by, bz, blockDef, builder);
                }
            }
            else if (blockDef.MeshType == MeshType.CrossQuad)
            {
                AddCrossQuad(bx, by, bz, blockDef, builder);
            }
            else if (blockDef.MeshType == MeshType.FlatSurface)
            {
                AddFlatSurface(bx, by, bz, blockDef, builder);
            }
            else if (blockDef.MeshType == MeshType.DoorClosed)
            {
                AddDoor(bx, by, bz, blockDef, false, builder);
            }
            else if (blockDef.MeshType == MeshType.DoorOpen)
            {
                AddDoor(bx, by, bz, blockDef, true, builder);
            }
            else if (blockDef.MeshType == MeshType.SwitchLever)
            {
                AddLever(bx, by, bz, blockDef, builder);
            }
            else if (blockDef.MeshType == MeshType.WallButton)
            {
                AddButton(bx, by, bz, blockDef, builder);
            }
        }

        private static bool ShouldRenderFace(Chunk current, Chunk? neighborChunk, int targetX, int targetY, int targetZ, int dirX, int dirZ, byte currentBlockId)
        {
            if (targetY < 0 || targetY >= Chunk.SIZE_Y) return true;

            byte neighborId = (byte)BlockType.Empty;

            // Inside current chunk?
            if (targetX >= 0 && targetX < Chunk.SIZE_X && targetZ >= 0 && targetZ < Chunk.SIZE_Z)
            {
                neighborId = current.GetBlock(targetX, targetY, targetZ);
            }
            // Cross-chunk boundary check
            else if (neighborChunk != null)
            {
                int neighborLocalX = targetX;
                int neighborLocalZ = targetZ;

                if (dirX == -1) neighborLocalX = Chunk.SIZE_X - 1;
                else if (dirX == 1) neighborLocalX = 0;

                if (dirZ == -1) neighborLocalZ = Chunk.SIZE_Z - 1;
                else if (dirZ == 1) neighborLocalZ = 0;

                neighborId = neighborChunk.GetBlock(neighborLocalX, targetY, neighborLocalZ);
            }
            else
            {
                // Unloaded neighbor: show face
                return true;
            }

            // If both are the same fluid (e.g. water next to water, or lava next to lava), cull internal face
            if (currentBlockId == neighborId && (currentBlockId == (byte)BlockType.Water || currentBlockId == (byte)BlockType.Lava))
            {
                return false;
            }

            return BlockRegistry.IsTransparent(neighborId);
        }

        private static void AddFace(
            Face face,
            float x,
            float y,
            float z,
            BlockDefinition def,
            ChunkMeshBuilder builder)
        {
            Vector3 tint = def.TintColor;
            Vector3 normal = face switch
            {
                Face.Front => new Vector3(0, 0, 1),
                Face.Back => new Vector3(0, 0, -1),
                Face.Left => new Vector3(-1, 0, 0),
                Face.Right => new Vector3(1, 0, 0),
                Face.Top => new Vector3(0, 1, 0),
                Face.Bottom => new Vector3(0, -1, 0),
                _ => Vector3.UnitY
            };

            float x0 = x - 0.5f;
            float x1 = x + 0.5f;
            float y0 = y - 0.5f;
            float y1 = y + 0.5f;
            float z0 = z - 0.5f;
            float z1 = z + 0.5f;

            Vector3 pos0, pos1, pos2, pos3;
            switch (face)
            {
                case Face.Front: // +Z
                    pos0 = new Vector3(x0, y1, z1);
                    pos1 = new Vector3(x1, y1, z1);
                    pos2 = new Vector3(x1, y0, z1);
                    pos3 = new Vector3(x0, y0, z1);
                    break;

                case Face.Back: // -Z
                    pos0 = new Vector3(x1, y1, z0);
                    pos1 = new Vector3(x0, y1, z0);
                    pos2 = new Vector3(x0, y0, z0);
                    pos3 = new Vector3(x1, y0, z0);
                    break;

                case Face.Left: // -X
                    pos0 = new Vector3(x0, y1, z0);
                    pos1 = new Vector3(x0, y1, z1);
                    pos2 = new Vector3(x0, y0, z1);
                    pos3 = new Vector3(x0, y0, z0);
                    break;

                case Face.Right: // +X
                    pos0 = new Vector3(x1, y1, z1);
                    pos1 = new Vector3(x1, y1, z0);
                    pos2 = new Vector3(x1, y0, z0);
                    pos3 = new Vector3(x1, y0, z1);
                    break;

                case Face.Top: // +Y
                    pos0 = new Vector3(x0, y1, z0);
                    pos1 = new Vector3(x1, y1, z0);
                    pos2 = new Vector3(x1, y1, z1);
                    pos3 = new Vector3(x0, y1, z1);
                    break;

                default: // Face.Bottom (-Y)
                    pos0 = new Vector3(x0, y0, z1);
                    pos1 = new Vector3(x1, y0, z1);
                    pos2 = new Vector3(x1, y0, z0);
                    pos3 = new Vector3(x0, y0, z0);
                    break;
            }

            Vector2 atlasCoord = def.GetFaceAtlasCoord(face);
            float u0 = atlasCoord.X / ATLAS_SIZE;
            float u1 = (atlasCoord.X + 1f) / ATLAS_SIZE;
            float v0 = 1.0f - ((atlasCoord.Y + 1f) / ATLAS_SIZE); // bottom of tile
            float v1 = 1.0f - (atlasCoord.Y / ATLAS_SIZE);        // top of tile

            builder.AddQuad(pos0, pos1, pos2, pos3, u0, v0, u1, v1, tint, normal);
        }

        private static void AddDoubleSidedQuad(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector2 atlasCoord,
            Vector3 tint,
            Vector3 normal,
            ChunkMeshBuilder builder)
        {
            float u0 = atlasCoord.X / ATLAS_SIZE;
            float u1 = (atlasCoord.X + 1f) / ATLAS_SIZE;
            float b0 = 1.0f - ((atlasCoord.Y + 1f) / ATLAS_SIZE);
            float b1 = 1.0f - (atlasCoord.Y / ATLAS_SIZE);

            // Front side (CW winding)
            builder.AddQuad(v0, v1, v2, v3, u0, b0, u1, b1, tint, normal);

            // Back side (reversed winding so visible from both sides)
            builder.AddQuad(v1, v0, v3, v2, u1, b0, u0, b1, tint, -normal);
        }

        private static void AddCrossQuad(
            float bx, float by, float bz, BlockDefinition def,
            ChunkMeshBuilder builder)
        {
            Vector2 uv = def.GetFaceAtlasCoord(Face.Front);
            Vector3 tint = def.IsLightSource ? new Vector3(1.35f, 1.25f, 1.10f) : def.TintColor;
            float r = 0.35f;

            // Diagonal 1
            AddDoubleSidedQuad(
                new Vector3(bx - r, by + 0.45f, bz - r),
                new Vector3(bx + r, by + 0.45f, bz + r),
                new Vector3(bx + r, by - 0.5f,  bz + r),
                new Vector3(bx - r, by - 0.5f,  bz - r),
                uv, tint, new Vector3(-0.7f, 0, 0.7f),
                builder
            );

            // Diagonal 2
            AddDoubleSidedQuad(
                new Vector3(bx - r, by + 0.45f, bz + r),
                new Vector3(bx + r, by + 0.45f, bz - r),
                new Vector3(bx + r, by - 0.5f,  bz - r),
                new Vector3(bx - r, by - 0.5f,  bz + r),
                uv, tint, new Vector3(0.7f, 0, 0.7f),
                builder
            );
        }

        private static void AddFlatSurface(
            float bx, float by, float bz, BlockDefinition def,
            ChunkMeshBuilder builder)
        {
            Vector2 uv = def.GetFaceAtlasCoord(Face.Front);
            Vector3 tint = def.TintColor;
            float y = by - 0.47f; // Just 0.03 above ground block top

            AddDoubleSidedQuad(
                new Vector3(bx - 0.5f, y, bz - 0.5f),
                new Vector3(bx + 0.5f, y, bz - 0.5f),
                new Vector3(bx + 0.5f, y, bz + 0.5f),
                new Vector3(bx - 0.5f, y, bz + 0.5f),
                uv, tint, Vector3.UnitY,
                builder
            );
        }

        private static void AddDoor(
            float bx, float by, float bz, BlockDefinition def, bool isOpen,
            ChunkMeshBuilder builder)
        {
            Vector2 uv = def.GetFaceAtlasCoord(Face.Front);
            Vector3 tint = def.TintColor;

            if (!isOpen)
            {
                // Closed door: aligned with Z edge
                float z = bz - 0.42f;
                AddDoubleSidedQuad(
                    new Vector3(bx - 0.5f, by + 0.5f, z),
                    new Vector3(bx + 0.5f, by + 0.5f, z),
                    new Vector3(bx + 0.5f, by - 0.5f, z),
                    new Vector3(bx - 0.5f, by - 0.5f, z),
                    uv, tint, new Vector3(0, 0, 1),
                    builder
                );
            }
            else
            {
                // Open door: rotated 90 degrees along X edge
                float x = bx - 0.42f;
                AddDoubleSidedQuad(
                    new Vector3(x, by + 0.5f, bz - 0.5f),
                    new Vector3(x, by + 0.5f, bz + 0.5f),
                    new Vector3(x, by - 0.5f, bz + 0.5f),
                    new Vector3(x, by - 0.5f, bz - 0.5f),
                    uv, tint, new Vector3(1, 0, 0),
                    builder
                );
            }
        }

        private static void AddLever(
            float bx, float by, float bz, BlockDefinition def,
            ChunkMeshBuilder builder)
        {
            Vector2 uv = def.GetFaceAtlasCoord(Face.Front);
            Vector3 tint = def.TintColor;

            // Small floor base
            AddDoubleSidedQuad(
                new Vector3(bx - 0.25f, by - 0.45f, bz - 0.25f),
                new Vector3(bx + 0.25f, by - 0.45f, bz - 0.25f),
                new Vector3(bx + 0.25f, by - 0.45f, bz + 0.25f),
                new Vector3(bx - 0.25f, by - 0.45f, bz + 0.25f),
                uv, tint, Vector3.UnitY,
                builder
            );

            // Handle quad
            AddDoubleSidedQuad(
                new Vector3(bx - 0.15f, by + 0.2f, bz),
                new Vector3(bx + 0.15f, by + 0.2f, bz),
                new Vector3(bx + 0.15f, by - 0.45f, bz),
                new Vector3(bx - 0.15f, by - 0.45f, bz),
                uv, tint, new Vector3(0, 0, 1),
                builder
            );
        }

        private static void AddButton(
            float bx, float by, float bz, BlockDefinition def,
            ChunkMeshBuilder builder)
        {
            Vector2 uv = def.GetFaceAtlasCoord(Face.Front);
            Vector3 tint = def.TintColor;

            AddDoubleSidedQuad(
                new Vector3(bx - 0.2f, by - 0.42f, bz - 0.2f),
                new Vector3(bx + 0.2f, by - 0.42f, bz - 0.2f),
                new Vector3(bx + 0.2f, by - 0.42f, bz + 0.2f),
                new Vector3(bx - 0.2f, by - 0.42f, bz + 0.2f),
                uv, tint, Vector3.UnitY,
                builder
            );
        }
    }
}
