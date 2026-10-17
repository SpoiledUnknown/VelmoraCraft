using OpenTK.Mathematics;
using VoxelRenderEngine.src.Graphics;
using VoxelRenderEngine.src.UI;

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

            bool hasTranslucent = false;

            // PASS 1: Opaque blocks (all solid blocks, terrain, seabed, etc.)
            for (int x = 0; x < Chunk.SIZE_X; x++)
            {
                for (int y = 0; y < Chunk.SIZE_Y; y++)
                {
                    for (int z = 0; z < Chunk.SIZE_Z; z++)
                    {
                        byte blockId = chunk.GetBlock(x, y, z);
                        if (blockId == (byte)BlockType.Empty) continue;
                        if (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Portal)
                        {
                            hasTranslucent = true;
                            continue;
                        }

                        AddBlockMesh(chunk, leftChunk, rightChunk, frontChunk, backChunk, x, y, z, blockId, chunkWorldX, chunkWorldZ, builder);
                    }
                }
            }

            int opaqueIndexCount = builder.Indices.Count;

            // PASS 2: Translucent blocks (Water & Portal) - only executed if chunk contains translucent blocks
            if (hasTranslucent)
            {
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

            if (blockId == (byte)BlockType.Chest)
            {
                AddChestMesh(chunk, leftChunk, rightChunk, frontChunk, backChunk, x, y, z, bx, by, bz, blockDef, builder);
                return;
            }

            if (blockDef.MeshType == MeshType.StandardCube)
            {
                byte facing = 0;
                if (BlockRegistry.IsDirectionalBlock((BlockType)blockId))
                {
                    facing = chunk.GetMetadata(x, y, z);
                }

                // 1. Front (+Z)
                if (ShouldRenderFace(chunk, frontChunk, x, y, z + 1, 0, 1, blockId))
                {
                    AddFace(Face.Front, bx, by, bz, blockDef, builder, facing);
                }

                // 2. Back (-Z)
                if (ShouldRenderFace(chunk, backChunk, x, y, z - 1, 0, -1, blockId))
                {
                    AddFace(Face.Back, bx, by, bz, blockDef, builder, facing);
                }

                // 3. Left (-X)
                if (ShouldRenderFace(chunk, leftChunk, x - 1, y, z, -1, 0, blockId))
                {
                    AddFace(Face.Left, bx, by, bz, blockDef, builder, facing);
                }

                // 4. Right (+X)
                if (ShouldRenderFace(chunk, rightChunk, x + 1, y, z, 1, 0, blockId))
                {
                    AddFace(Face.Right, bx, by, bz, blockDef, builder, facing);
                }

                // 5. Top (+Y)
                byte topBlock = y + 1 < Chunk.SIZE_Y ? chunk.GetBlock(x, y + 1, z) : (byte)BlockType.Empty;
                if ((y + 1 >= Chunk.SIZE_Y || BlockRegistry.IsTransparent(topBlock)) && !(topBlock == blockId && (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Lava)))
                {
                    AddFace(Face.Top, bx, by, bz, blockDef, builder, facing);
                }

                // 6. Bottom (-Y)
                byte bottomBlock = y - 1 >= 0 ? chunk.GetBlock(x, y - 1, z) : (byte)BlockType.Empty;
                if ((y - 1 < 0 || BlockRegistry.IsTransparent(bottomBlock)) && !(bottomBlock == blockId && (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Lava)))
                {
                    AddFace(Face.Bottom, bx, by, bz, blockDef, builder, facing);
                }
            }
            else if (blockDef.MeshType == MeshType.CrossQuad)
            {
                if (blockDef.Type == BlockType.Wheat)
                {
                    byte stage = chunk.GetMetadata(x, y, z);
                    AddWheatMesh(bx, by, bz, blockDef, stage, builder);
                }
                else
                {
                    AddCrossQuad(bx, by, bz, blockDef, builder);
                }
            }
            else if (blockDef.MeshType == MeshType.FlatSurface)
            {
                if (BlockRegistry.IsRailBlock(blockDef.Type))
                {
                    byte meta = chunk.GetMetadata(x, y, z);
                    AddRailMesh(bx, by, bz, blockDef, meta, builder);
                }
                else
                {
                    AddFlatSurface(bx, by, bz, blockDef, builder);
                }
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
            else if (blockDef.MeshType == MeshType.Bed)
            {
                byte facing = chunk.GetMetadata(x, y, z);
                AddBed(bx, by, bz, blockDef, facing, builder);
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

        private static byte GetBlockAt(Chunk current, Chunk? leftChunk, Chunk? rightChunk, Chunk? frontChunk, Chunk? backChunk, int targetX, int targetY, int targetZ)
        {
            if (targetY < 0 || targetY >= Chunk.SIZE_Y) return (byte)BlockType.Empty;

            if (targetX >= 0 && targetX < Chunk.SIZE_X && targetZ >= 0 && targetZ < Chunk.SIZE_Z)
            {
                return current.GetBlock(targetX, targetY, targetZ);
            }

            if (targetX < 0 && leftChunk != null && targetZ >= 0 && targetZ < Chunk.SIZE_Z)
            {
                return leftChunk.GetBlock(Chunk.SIZE_X + targetX, targetY, targetZ);
            }

            if (targetX >= Chunk.SIZE_X && rightChunk != null && targetZ >= 0 && targetZ < Chunk.SIZE_Z)
            {
                return rightChunk.GetBlock(targetX - Chunk.SIZE_X, targetY, targetZ);
            }

            if (targetZ < 0 && backChunk != null && targetX >= 0 && targetX < Chunk.SIZE_X)
            {
                return backChunk.GetBlock(targetX, targetY, Chunk.SIZE_Z + targetZ);
            }

            if (targetZ >= Chunk.SIZE_Z && frontChunk != null && targetX >= 0 && targetX < Chunk.SIZE_X)
            {
                return frontChunk.GetBlock(targetX, targetY, targetZ - Chunk.SIZE_Z);
            }

            return (byte)BlockType.Empty;
        }

        private static byte GetMetadataAt(Chunk current, Chunk? leftChunk, Chunk? rightChunk, Chunk? frontChunk, Chunk? backChunk, int targetX, int targetY, int targetZ)
        {
            if (targetY < 0 || targetY >= Chunk.SIZE_Y) return 0;

            if (targetX >= 0 && targetX < Chunk.SIZE_X && targetZ >= 0 && targetZ < Chunk.SIZE_Z)
            {
                return current.GetMetadata(targetX, targetY, targetZ);
            }

            if (targetX < 0 && leftChunk != null && targetZ >= 0 && targetZ < Chunk.SIZE_Z)
            {
                return leftChunk.GetMetadata(Chunk.SIZE_X + targetX, targetY, targetZ);
            }

            if (targetX >= Chunk.SIZE_X && rightChunk != null && targetZ >= 0 && targetZ < Chunk.SIZE_Z)
            {
                return rightChunk.GetMetadata(targetX - Chunk.SIZE_X, targetY, targetZ);
            }

            if (targetZ < 0 && backChunk != null && targetX >= 0 && targetX < Chunk.SIZE_X)
            {
                return backChunk.GetMetadata(targetX, targetY, Chunk.SIZE_Z + targetZ);
            }

            if (targetZ >= Chunk.SIZE_Z && frontChunk != null && targetX >= 0 && targetX < Chunk.SIZE_X)
            {
                return frontChunk.GetMetadata(targetX, targetY, targetZ - Chunk.SIZE_Z);
            }

            return 0;
        }

        private static void AddChestMesh(
            Chunk chunk,
            Chunk? leftChunk,
            Chunk? rightChunk,
            Chunk? frontChunk,
            Chunk? backChunk,
            int x, int y, int z,
            float bx, float by, float bz,
            BlockDefinition blockDef,
            ChunkMeshBuilder builder)
        {
            int worldX = (int)chunk.Position.X + x;
            int worldY = y;
            int worldZ = (int)chunk.Position.Z + z;

            bool isDouble = ChestManager.TryGetDoubleChestPartner(
                new Vector3i(worldX, worldY, worldZ),
                (qx, qy, qz) =>
                {
                    int lx = qx - (int)chunk.Position.X;
                    int lz = qz - (int)chunk.Position.Z;
                    return GetBlockAt(chunk, leftChunk, rightChunk, frontChunk, backChunk, lx, qy, lz);
                },
                out Vector3i partnerPos,
                out bool isFirstHalf,
                out bool isAxisX
            );

            Vector2 uvFront, uvBack, uvLeft, uvRight, uvTop, uvBottom;
            Vector2 sideUV = new Vector2(10, 1);
            Vector2 topBottomUV = new Vector2(9, 1);

            if (!isDouble)
            {
                byte facing = chunk.GetMetadata(x, y, z);
                uvFront = (facing == 0) ? new Vector2(11, 1) : sideUV;
                uvBack  = (facing == 1) ? new Vector2(11, 1) : sideUV;
                uvLeft  = (facing == 2) ? new Vector2(11, 1) : sideUV;
                uvRight = (facing == 3) ? new Vector2(11, 1) : sideUV;
                uvTop = topBottomUV;
                uvBottom = topBottomUV;
            }
            else
            {
                // Always inherit the facing of the first/primary chest for both halves!
                Vector3i firstPos = isFirstHalf ? new Vector3i(worldX, worldY, worldZ) : partnerPos;
                int flx = firstPos.X - (int)chunk.Position.X;
                int flz = firstPos.Z - (int)chunk.Position.Z;
                byte facing = GetMetadataAt(chunk, leftChunk, rightChunk, frontChunk, backChunk, flx, worldY, flz);

                if (isAxisX)
                {
                    if (facing == (byte)BlockFacing.North) // Facing North (-Z)
                    {
                        if (isFirstHalf) // Lower X (Right half looking from -Z)
                        {
                            uvFront = new Vector2(9, 3);   // Back left
                            uvBack = new Vector2(10, 2);   // Front right
                            uvLeft = sideUV;               // Outer side
                            uvRight = sideUV;              // Inner side (culled)
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                        else // Higher X (Left half looking from -Z)
                        {
                            uvFront = new Vector2(10, 3);  // Back right
                            uvBack = new Vector2(9, 2);    // Front left
                            uvLeft = sideUV;               // Inner side (culled)
                            uvRight = sideUV;              // Outer side
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                    }
                    else // Facing South (+Z, default)
                    {
                        if (isFirstHalf) // Lower X (Left half looking from +Z)
                        {
                            uvFront = new Vector2(9, 2);   // Front left
                            uvBack = new Vector2(10, 3);   // Back left
                            uvLeft = sideUV;               // Outer side
                            uvRight = sideUV;              // Inner side (culled)
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                        else // Higher X (Right half looking from +Z)
                        {
                            uvFront = new Vector2(10, 2);  // Front right
                            uvBack = new Vector2(9, 3);    // Back right
                            uvLeft = sideUV;               // Inner side (culled)
                            uvRight = sideUV;              // Outer side
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                    }
                }
                else // isAxisZ
                {
                    if (facing == (byte)BlockFacing.East) // Facing East (+X)
                    {
                        if (isFirstHalf) // Lower Z (Right half looking from +X)
                        {
                            uvFront = sideUV;
                            uvBack = sideUV;
                            uvLeft = new Vector2(9, 3);    // Back right
                            uvRight = new Vector2(10, 2);  // Front right
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                        else // Higher Z (Left half looking from +X)
                        {
                            uvFront = sideUV;
                            uvBack = sideUV;
                            uvLeft = new Vector2(10, 3);   // Back left
                            uvRight = new Vector2(9, 2);   // Front left
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                    }
                    else // Facing West (-X, default)
                    {
                        if (isFirstHalf) // Lower Z (Left half looking from -X)
                        {
                            uvFront = sideUV;
                            uvBack = sideUV;
                            uvLeft = new Vector2(9, 2);    // Front left
                            uvRight = new Vector2(10, 3);  // Back left
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                        else // Higher Z (Right half looking from -X)
                        {
                            uvFront = sideUV;
                            uvBack = sideUV;
                            uvLeft = new Vector2(10, 2);   // Front right
                            uvRight = new Vector2(9, 3);   // Back right
                            uvTop = topBottomUV;
                            uvBottom = topBottomUV;
                        }
                    }
                }
            }

            // 1. Front (+Z)
            if (ShouldRenderFace(chunk, frontChunk, x, y, z + 1, 0, 1, (byte)BlockType.Chest))
            {
                AddFaceWithUV(Face.Front, bx, by, bz, uvFront, blockDef.TintColor, builder);
            }

            // 2. Back (-Z)
            if (ShouldRenderFace(chunk, backChunk, x, y, z - 1, 0, -1, (byte)BlockType.Chest))
            {
                AddFaceWithUV(Face.Back, bx, by, bz, uvBack, blockDef.TintColor, builder);
            }

            // 3. Left (-X)
            if (ShouldRenderFace(chunk, leftChunk, x - 1, y, z, -1, 0, (byte)BlockType.Chest))
            {
                AddFaceWithUV(Face.Left, bx, by, bz, uvLeft, blockDef.TintColor, builder);
            }

            // 4. Right (+X)
            if (ShouldRenderFace(chunk, rightChunk, x + 1, y, z, 1, 0, (byte)BlockType.Chest))
            {
                AddFaceWithUV(Face.Right, bx, by, bz, uvRight, blockDef.TintColor, builder);
            }

            // 5. Top (+Y)
            byte topBlock = y + 1 < Chunk.SIZE_Y ? chunk.GetBlock(x, y + 1, z) : (byte)BlockType.Empty;
            if (y + 1 >= Chunk.SIZE_Y || BlockRegistry.IsTransparent(topBlock))
            {
                AddFaceWithUV(Face.Top, bx, by, bz, uvTop, blockDef.TintColor, builder);
            }

            // 6. Bottom (-Y)
            byte bottomBlock = y - 1 >= 0 ? chunk.GetBlock(x, y - 1, z) : (byte)BlockType.Empty;
            if (y - 1 < 0 || BlockRegistry.IsTransparent(bottomBlock))
            {
                AddFaceWithUV(Face.Bottom, bx, by, bz, uvBottom, blockDef.TintColor, builder);
            }
        }

        private static void AddFace(
            Face face,
            float x,
            float y,
            float z,
            BlockDefinition def,
            ChunkMeshBuilder builder,
            byte facing = 0)
        {
            Face texFace = GetOrientedTextureFace(face, facing);
            AddFaceWithUV(face, x, y, z, def.GetFaceAtlasCoord(texFace), def.TintColor, builder);
        }

        private static Face GetOrientedTextureFace(Face meshFace, byte facing)
        {
            if (facing == 0) return meshFace;
            if (meshFace == Face.Top || meshFace == Face.Bottom) return meshFace;

            return facing switch
            {
                1 => meshFace switch // North (-Z)
                {
                    Face.Front => Face.Back,
                    Face.Back => Face.Front,
                    Face.Left => Face.Right,
                    Face.Right => Face.Left,
                    _ => meshFace
                },
                2 => meshFace switch // West (-X)
                {
                    Face.Front => Face.Right,
                    Face.Back => Face.Left,
                    Face.Left => Face.Front,
                    Face.Right => Face.Back,
                    _ => meshFace
                },
                3 => meshFace switch // East (+X)
                {
                    Face.Front => Face.Left,
                    Face.Back => Face.Right,
                    Face.Left => Face.Back,
                    Face.Right => Face.Front,
                    _ => meshFace
                },
                _ => meshFace
            };
        }

        private static void AddFaceWithUV(
            Face face,
            float x,
            float y,
            float z,
            Vector2 atlasCoord,
            Vector3 tint,
            ChunkMeshBuilder builder)
        {
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

        private static void AddWheatMesh(
            float bx, float by, float bz, BlockDefinition def, byte stage,
            ChunkMeshBuilder builder)
        {
            // Textures from (7, 5) to (15, 5), with 15,5 being complete wheat and 7,5 being seeds
            int col = (stage >= 7) ? 15 : (7 + Math.Clamp((int)stage, 0, 6));
            Vector2 uv = new Vector2(col, 5);
            Vector3 tint = def.TintColor;
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

        private static void AddRailMesh(
            float bx, float by, float bz, BlockDefinition def, byte shape,
            ChunkMeshBuilder builder)
        {
            Vector2 uv = def.GetFaceAtlasCoord(Face.Front);
            Vector3 tint = def.TintColor;
            float y = by - 0.47f; // Just 0.03 above ground block top

            // If it's a standard rail and curved shape (>= 2), use the curved rail texture tile (0, 7)
            if (def.Type == BlockType.Rail && shape >= 2)
            {
                uv = new Vector2(0, 7);
            }

            Vector3 pNW = new Vector3(bx - 0.5f, y, bz - 0.5f);
            Vector3 pNE = new Vector3(bx + 0.5f, y, bz - 0.5f);
            Vector3 pSE = new Vector3(bx + 0.5f, y, bz + 0.5f);
            Vector3 pSW = new Vector3(bx - 0.5f, y, bz + 0.5f);

            Vector3 v0, v1, v2, v3;

            switch (shape)
            {
                case 1: // EastWest (straight rail running along X axis)
                    v0 = pSW; v1 = pNW; v2 = pNE; v3 = pSE;
                    break;

                case 2: // SouthEast curve (connecting +Z South and +X East)
                    v0 = pNW; v1 = pNE; v2 = pSE; v3 = pSW;
                    break;

                case 3: // SouthWest curve (connecting +Z South and -X West)
                    v0 = pNE; v1 = pSE; v2 = pSW; v3 = pNW;
                    break;

                case 4: // NorthWest curve (connecting -Z North and -X West)
                    v0 = pSE; v1 = pSW; v2 = pNW; v3 = pNE;
                    break;

                case 5: // NorthEast curve (connecting -Z North and +X East)
                    v0 = pSW; v1 = pNW; v2 = pNE; v3 = pSE;
                    break;

                case 0: // NorthSouth (straight rail running along Z axis)
                default:
                    v0 = pNW; v1 = pNE; v2 = pSE; v3 = pSW;
                    break;
            }

            AddDoubleSidedQuad(v0, v1, v2, v3, uv, tint, Vector3.UnitY, builder);
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

        private static void AddBed(
            float bx, float by, float bz, BlockDefinition def, byte facing,
            ChunkMeshBuilder builder)
        {
            float x0 = bx - 0.5f;
            float x1 = bx + 0.5f;
            float y0 = by - 0.5f;
            float y1 = by + 0.0625f; // 9/16 standard bed height
            float z0 = bz - 0.5f;
            float z1 = bz + 0.5f;

            bool isHead = (def.Type == BlockType.BedHead);
            Vector2 topUV = isHead ? new Vector2(7, 8) : new Vector2(6, 8);
            Vector2 endUV = isHead ? new Vector2(5, 9) : new Vector2(6, 9);
            Vector2 sideUV = isHead ? new Vector2(7, 9) : new Vector2(8, 9);
            Vector2 bottomUV = new Vector2(4, 0); // Wooden planks bottom
            Vector3 tint = def.TintColor;

            // 1. Top face (+Y)
            AddBoxFaceWithUV(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), topUV, tint, Vector3.UnitY, builder);

            // 2. Bottom face (-Y)
            AddBoxFaceWithUV(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), bottomUV, tint, -Vector3.UnitY, builder);

            // 3. Side & End faces based on facing
            // facing: 0 = South (+Z), 1 = North (-Z), 2 = West (-X), 3 = East (+X)
            if (facing == 2 || facing == 3) // Facing along X
            {
                // North (-Z) & South (+Z) are side faces
                AddBoxFaceWithUV(new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), sideUV, tint, Vector3.UnitZ, builder);
                AddBoxFaceWithUV(new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), sideUV, tint, -Vector3.UnitZ, builder);

                // Ends: West (-X) and East (+X)
                Vector2 westUV = (facing == 2) ? (!isHead ? endUV : sideUV) : (isHead ? endUV : sideUV);
                Vector2 eastUV = (facing == 3) ? (!isHead ? endUV : sideUV) : (isHead ? endUV : sideUV);

                AddBoxFaceWithUV(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), westUV, tint, -Vector3.UnitX, builder);
                AddBoxFaceWithUV(new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), eastUV, tint, Vector3.UnitX, builder);
            }
            else // Default facing along Z (0 = South, 1 = North)
            {
                // West (-X) & East (+X) are side faces
                AddBoxFaceWithUV(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), sideUV, tint, -Vector3.UnitX, builder);
                AddBoxFaceWithUV(new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), sideUV, tint, Vector3.UnitX, builder);

                // Ends: North (-Z) and South (+Z)
                Vector2 northUV = (facing == 1) ? (!isHead ? endUV : sideUV) : (isHead ? endUV : sideUV);
                Vector2 southUV = (facing == 0) ? (!isHead ? endUV : sideUV) : (isHead ? endUV : sideUV);

                AddBoxFaceWithUV(new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), northUV, tint, -Vector3.UnitZ, builder);
                AddBoxFaceWithUV(new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), southUV, tint, Vector3.UnitZ, builder);
            }
        }

        private static void AddBoxFaceWithUV(
            Vector3 pos0, Vector3 pos1, Vector3 pos2, Vector3 pos3,
            Vector2 atlasCoord, Vector3 tint, Vector3 normal,
            ChunkMeshBuilder builder)
        {
            float u0 = atlasCoord.X / ATLAS_SIZE;
            float u1 = (atlasCoord.X + 1f) / ATLAS_SIZE;
            float v0 = 1.0f - ((atlasCoord.Y + 1f) / ATLAS_SIZE);
            float v1 = 1.0f - (atlasCoord.Y / ATLAS_SIZE);
            builder.AddQuad(pos0, pos1, pos2, pos3, u0, v0, u1, v1, tint, normal);
        }
    }
}
