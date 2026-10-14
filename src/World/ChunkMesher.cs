using OpenTK.Mathematics;
using VoxelRenderEngine.src.Graphics;

namespace VoxelRenderEngine.src.World
{
    public static class ChunkMesher
    {
        private const float ATLAS_SIZE = 16f;

        public static ChunkMesh BuildMesh(Chunk chunk, Chunk? leftChunk, Chunk? rightChunk, Chunk? frontChunk, Chunk? backChunk)
        {
            var vertices = new List<Vector3>(4096);
            var uvs = new List<Vector2>(4096);
            var colors = new List<Vector3>(4096);
            var normals = new List<Vector3>(4096);
            var indices = new List<uint>(6144);
            uint vertexOffset = 0;

            float chunkWorldX = chunk.Position.X;
            float chunkWorldZ = chunk.Position.Z;

            for (int x = 0; x < Chunk.SIZE_X; x++)
            {
                for (int y = 0; y < Chunk.SIZE_Y; y++)
                {
                    for (int z = 0; z < Chunk.SIZE_Z; z++)
                    {
                        byte blockId = chunk.GetBlock(x, y, z);
                        if (blockId == (byte)BlockType.Empty) continue;

                        var blockDef = BlockRegistry.Get(blockId);
                        float bx = chunkWorldX + x;
                        float by = y;
                        float bz = chunkWorldZ + z;

                        if (blockDef.MeshType == MeshType.StandardCube)
                        {
                            // 1. Front (+Z)
                            if (ShouldRenderFace(chunk, frontChunk, x, y, z + 1, 0, 1, blockId))
                            {
                                AddFace(Face.Front, bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                            }

                            // 2. Back (-Z)
                            if (ShouldRenderFace(chunk, backChunk, x, y, z - 1, 0, -1, blockId))
                            {
                                AddFace(Face.Back, bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                            }

                            // 3. Left (-X)
                            if (ShouldRenderFace(chunk, leftChunk, x - 1, y, z, -1, 0, blockId))
                            {
                                AddFace(Face.Left, bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                            }

                            // 4. Right (+X)
                            if (ShouldRenderFace(chunk, rightChunk, x + 1, y, z, 1, 0, blockId))
                            {
                                AddFace(Face.Right, bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                            }

                            // 5. Top (+Y)
                            byte topBlock = y + 1 < Chunk.SIZE_Y ? chunk.GetBlock(x, y + 1, z) : (byte)BlockType.Empty;
                            if ((y + 1 >= Chunk.SIZE_Y || BlockRegistry.IsTransparent(topBlock)) && !(topBlock == blockId && (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Lava)))
                            {
                                AddFace(Face.Top, bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                            }

                            // 6. Bottom (-Y)
                            byte bottomBlock = y - 1 >= 0 ? chunk.GetBlock(x, y - 1, z) : (byte)BlockType.Empty;
                            if ((y - 1 < 0 || BlockRegistry.IsTransparent(bottomBlock)) && !(bottomBlock == blockId && (blockId == (byte)BlockType.Water || blockId == (byte)BlockType.Lava)))
                            {
                                AddFace(Face.Bottom, bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                            }
                        }
                        else if (blockDef.MeshType == MeshType.CrossQuad)
                        {
                            AddCrossQuad(bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                        }
                        else if (blockDef.MeshType == MeshType.FlatSurface)
                        {
                            AddFlatSurface(bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                        }
                        else if (blockDef.MeshType == MeshType.DoorClosed)
                        {
                            AddDoor(bx, by, bz, blockDef, false, vertices, uvs, colors, normals, indices, ref vertexOffset);
                        }
                        else if (blockDef.MeshType == MeshType.DoorOpen)
                        {
                            AddDoor(bx, by, bz, blockDef, true, vertices, uvs, colors, normals, indices, ref vertexOffset);
                        }
                        else if (blockDef.MeshType == MeshType.SwitchLever)
                        {
                            AddLever(bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                        }
                        else if (blockDef.MeshType == MeshType.WallButton)
                        {
                            AddButton(bx, by, bz, blockDef, vertices, uvs, colors, normals, indices, ref vertexOffset);
                        }
                    }
                }
            }

            return new ChunkMesh(vertices.ToArray(), uvs.ToArray(), colors.ToArray(), normals.ToArray(), indices.ToArray());
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
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Vector3> colors,
            List<Vector3> normals,
            List<uint> indices,
            ref uint vertexOffset)
        {
            Vector3 tint = def.TintColor;
            colors.Add(tint);
            colors.Add(tint);
            colors.Add(tint);
            colors.Add(tint);

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
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            // Unit cube centered around block position (e.g. from -0.5 to +0.5 or 0 to 1)
            // Matching existing engine orientation:
            // x-0.5f to x+0.5f, y-0.5f to y+0.5f, z-0.5f to z+0.5f
            float x0 = x - 0.5f;
            float x1 = x + 0.5f;
            float y0 = y - 0.5f;
            float y1 = y + 0.5f;
            float z0 = z - 0.5f;
            float z1 = z + 0.5f;

            switch (face)
            {
                case Face.Front: // +Z
                    vertices.Add(new Vector3(x0, y1, z1));
                    vertices.Add(new Vector3(x1, y1, z1));
                    vertices.Add(new Vector3(x1, y0, z1));
                    vertices.Add(new Vector3(x0, y0, z1));
                    break;

                case Face.Back: // -Z
                    vertices.Add(new Vector3(x1, y1, z0));
                    vertices.Add(new Vector3(x0, y1, z0));
                    vertices.Add(new Vector3(x0, y0, z0));
                    vertices.Add(new Vector3(x1, y0, z0));
                    break;

                case Face.Left: // -X
                    vertices.Add(new Vector3(x0, y1, z0));
                    vertices.Add(new Vector3(x0, y1, z1));
                    vertices.Add(new Vector3(x0, y0, z1));
                    vertices.Add(new Vector3(x0, y0, z0));
                    break;

                case Face.Right: // +X
                    vertices.Add(new Vector3(x1, y1, z1));
                    vertices.Add(new Vector3(x1, y1, z0));
                    vertices.Add(new Vector3(x1, y0, z0));
                    vertices.Add(new Vector3(x1, y0, z1));
                    break;

                case Face.Top: // +Y
                    vertices.Add(new Vector3(x0, y1, z0));
                    vertices.Add(new Vector3(x1, y1, z0));
                    vertices.Add(new Vector3(x1, y1, z1));
                    vertices.Add(new Vector3(x0, y1, z1));
                    break;

                case Face.Bottom: // -Y
                    vertices.Add(new Vector3(x0, y0, z1));
                    vertices.Add(new Vector3(x1, y0, z1));
                    vertices.Add(new Vector3(x1, y0, z0));
                    vertices.Add(new Vector3(x0, y0, z0));
                    break;
            }

            // Intuitive Texture Coordinates:
            // atlasCoord.X = Column (0 to 15, left to right)
            // atlasCoord.Y = Row (0 to 15, top to bottom)
            Vector2 atlasCoord = def.GetFaceAtlasCoord(face);
            float u0 = atlasCoord.X / ATLAS_SIZE;
            float u1 = (atlasCoord.X + 1f) / ATLAS_SIZE;

            // Flip Y so row 0 is the TOP row of the PNG file
            float v0 = 1.0f - ((atlasCoord.Y + 1f) / ATLAS_SIZE); // bottom of tile
            float v1 = 1.0f - (atlasCoord.Y / ATLAS_SIZE);        // top of tile

            // Quad corners: 0: Top-Left, 1: Top-Right, 2: Bottom-Right, 3: Bottom-Left
            uvs.Add(new Vector2(u0, v1)); // 0: Top-Left
            uvs.Add(new Vector2(u1, v1)); // 1: Top-Right
            uvs.Add(new Vector2(u1, v0)); // 2: Bottom-Right
            uvs.Add(new Vector2(u0, v0)); // 3: Bottom-Left

            // Indices (CW winding matching GL.FrontFace(FrontFaceDirection.Cw))
            indices.Add(0 + vertexOffset);
            indices.Add(1 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(3 + vertexOffset);
            indices.Add(0 + vertexOffset);

            vertexOffset += 4;
        }

        private static void AddDoubleSidedQuad(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector2 atlasCoord,
            Vector3 tint,
            Vector3 normal,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Vector3> colors,
            List<Vector3> normals,
            List<uint> indices,
            ref uint vertexOffset)
        {
            float u0 = atlasCoord.X / ATLAS_SIZE;
            float u1 = (atlasCoord.X + 1f) / ATLAS_SIZE;
            float b0 = 1.0f - ((atlasCoord.Y + 1f) / ATLAS_SIZE);
            float b1 = 1.0f - (atlasCoord.Y / ATLAS_SIZE);

            // Front side (CW winding)
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            for (int i = 0; i < 4; i++)
            {
                colors.Add(tint);
                normals.Add(normal);
            }

            uvs.Add(new Vector2(u0, b1)); // 0: Top-Left
            uvs.Add(new Vector2(u1, b1)); // 1: Top-Right
            uvs.Add(new Vector2(u1, b0)); // 2: Bottom-Right
            uvs.Add(new Vector2(u0, b0)); // 3: Bottom-Left

            indices.Add(0 + vertexOffset);
            indices.Add(1 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(3 + vertexOffset);
            indices.Add(0 + vertexOffset);

            vertexOffset += 4;

            // Back side (reversed winding so visible from both sides)
            vertices.Add(v1);
            vertices.Add(v0);
            vertices.Add(v3);
            vertices.Add(v2);

            for (int i = 0; i < 4; i++)
            {
                colors.Add(tint);
                normals.Add(-normal);
            }

            uvs.Add(new Vector2(u1, b1));
            uvs.Add(new Vector2(u0, b1));
            uvs.Add(new Vector2(u0, b0));
            uvs.Add(new Vector2(u1, b0));

            indices.Add(0 + vertexOffset);
            indices.Add(1 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(3 + vertexOffset);
            indices.Add(0 + vertexOffset);

            vertexOffset += 4;
        }

        private static void AddCrossQuad(
            float bx, float by, float bz, BlockDefinition def,
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> colors, List<Vector3> normals, List<uint> indices, ref uint vertexOffset)
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
                vertices, uvs, colors, normals, indices, ref vertexOffset
            );

            // Diagonal 2
            AddDoubleSidedQuad(
                new Vector3(bx - r, by + 0.45f, bz + r),
                new Vector3(bx + r, by + 0.45f, bz - r),
                new Vector3(bx + r, by - 0.5f,  bz - r),
                new Vector3(bx - r, by - 0.5f,  bz + r),
                uv, tint, new Vector3(0.7f, 0, 0.7f),
                vertices, uvs, colors, normals, indices, ref vertexOffset
            );
        }

        private static void AddFlatSurface(
            float bx, float by, float bz, BlockDefinition def,
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> colors, List<Vector3> normals, List<uint> indices, ref uint vertexOffset)
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
                vertices, uvs, colors, normals, indices, ref vertexOffset
            );
        }

        private static void AddDoor(
            float bx, float by, float bz, BlockDefinition def, bool isOpen,
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> colors, List<Vector3> normals, List<uint> indices, ref uint vertexOffset)
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
                    vertices, uvs, colors, normals, indices, ref vertexOffset
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
                    vertices, uvs, colors, normals, indices, ref vertexOffset
                );
            }
        }

        private static void AddLever(
            float bx, float by, float bz, BlockDefinition def,
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> colors, List<Vector3> normals, List<uint> indices, ref uint vertexOffset)
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
                vertices, uvs, colors, normals, indices, ref vertexOffset
            );

            // Handle quad
            AddDoubleSidedQuad(
                new Vector3(bx - 0.15f, by + 0.2f, bz),
                new Vector3(bx + 0.15f, by + 0.2f, bz),
                new Vector3(bx + 0.15f, by - 0.45f, bz),
                new Vector3(bx - 0.15f, by - 0.45f, bz),
                uv, tint, new Vector3(0, 0, 1),
                vertices, uvs, colors, normals, indices, ref vertexOffset
            );
        }

        private static void AddButton(
            float bx, float by, float bz, BlockDefinition def,
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> colors, List<Vector3> normals, List<uint> indices, ref uint vertexOffset)
        {
            Vector2 uv = def.GetFaceAtlasCoord(Face.Front);
            Vector3 tint = def.TintColor;

            AddDoubleSidedQuad(
                new Vector3(bx - 0.2f, by - 0.42f, bz - 0.2f),
                new Vector3(bx + 0.2f, by - 0.42f, bz - 0.2f),
                new Vector3(bx + 0.2f, by - 0.42f, bz + 0.2f),
                new Vector3(bx - 0.2f, by - 0.42f, bz + 0.2f),
                uv, tint, Vector3.UnitY,
                vertices, uvs, colors, normals, indices, ref vertexOffset
            );
        }
    }
}
