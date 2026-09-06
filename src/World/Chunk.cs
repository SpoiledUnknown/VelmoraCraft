using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SimplexNoise;
using VoxelRenderEngine.src.Graphics;

namespace VoxelRenderEngine.src.World
{
    internal class Chunk
    {
        private List<Vector3> chunkVerts;
        private List<Vector2> chunkUVs;
        private List<uint> chunkIndices;

        const int SIZE = 32;
        const int HEIGHT = 32;

        private Vector3 position;
        private uint indexCount;

        private VertexArrayObject? chunkVAO;
        private VertexBufferObject? chunkVertexVBO;
        private VertexBufferObject? chunkUVVBO;
        private IndexBufferObject? chunkIBO;

        private Texture? texture;
        Block[,,] chunkBLocks = new Block[SIZE, HEIGHT, SIZE];

        public Chunk(Vector3 position)
        {
            this.position = position;
            Random random = new Random();
            Noise.Seed = random.Next(0, 100000);

            chunkVerts = new List<Vector3>();
            chunkUVs = new List<Vector2>();
            chunkIndices = new List<uint>();

            float[,] heightMap = GenChunk();
            GenBLock(heightMap);
            GenFaces(heightMap);
            BuildChunk();
        }

        public float[,] GenChunk()
        {
            float[,] heightMap = new float[SIZE, SIZE];

            for (int x = 0; x < SIZE; x++)
            {
                for (int z = 0; z < SIZE; z++)
                {
                    heightMap[x, z] = Noise.CalcPixel2D(x, z, 0.01f);
                }
            }

            return heightMap;
        }
        public void GenBLock(float[,] heightMap)
        {
            for (int x = 0; x < SIZE; x++)
            {
                for (int z = 0; z < SIZE; z++)
                {

                    int columnHeight = (int)(heightMap[x, z] / 10);
                    for (int y = 0; y < HEIGHT; y++)
                    {
                        BlockType type = BlockType.Empty;
                        if (y < columnHeight - 1)
                        {
                            type = BlockType.Dirt;
                        }
                        else if (y == columnHeight - 1)
                        {
                            type = BlockType.Grass;
                        }

                        chunkBLocks[x, y, z] = new Block(new Vector3(x, y, z), type);
                    }
                }
            }
        }

        public void GenFaces(float[,] heightMap)
        {
            for (int x = 0; x < SIZE; x++)
            {
                for (int z = 0; z < SIZE; z++)
                {
                    for (int y = 0; y < HEIGHT; y++)
                    {
                        int numFaces = 0;

                        if (chunkBLocks[x, y, z].type == BlockType.Empty) continue;

                        //Left Faces
                        if (x > 0)
                        {
                            if (chunkBLocks[x - 1, y, z].type == BlockType.Empty)
                            {
                                IntegrateFace(chunkBLocks[x, y, z], Faces.Left);
                                numFaces++;
                            }
                        }
                        else
                        {
                            IntegrateFace(chunkBLocks[x, y, z], Faces.Left);
                            numFaces++;
                        }

                        //Right Faces
                        if (x < (SIZE - 1))
                        {
                            if (chunkBLocks[x + 1, y, z].type == BlockType.Empty)
                            {
                                IntegrateFace(chunkBLocks[x, y, z], Faces.Right);
                                numFaces++;
                            }
                        }
                        else
                        {
                            IntegrateFace(chunkBLocks[x, y, z], Faces.Right);
                            numFaces++;
                        }

                        //Top Faces
                        if (y < (HEIGHT - 1))
                        {
                            if (chunkBLocks[x, y + 1, z].type == BlockType.Empty)
                            {
                                IntegrateFace(chunkBLocks[x, y, z], Faces.Top);
                                numFaces++;
                            }
                        }
                        else
                        {
                            IntegrateFace(chunkBLocks[x, y, z], Faces.Top);
                            numFaces++;
                        }

                        //Bottom Faces
                        if (y > 0)
                        {
                            if (chunkBLocks[x, y - 1, z].type == BlockType.Empty)
                            {
                                IntegrateFace(chunkBLocks[x, y, z], Faces.Bottom);
                                numFaces++;
                            }
                        }
                        else
                        {
                            IntegrateFace(chunkBLocks[x, y, z], Faces.Bottom);
                            numFaces++;
                        }

                        //Front Faces
                        if (z < (SIZE - 1))
                        {
                            if (chunkBLocks[x, y, z + 1].type == BlockType.Empty)
                            {
                                IntegrateFace(chunkBLocks[x, y, z], Faces.Front);
                                numFaces++;
                            }
                        }
                        else
                        {
                            IntegrateFace(chunkBLocks[x, y, z], Faces.Front);
                            numFaces++;
                        }

                        //Back Faces
                        if (z > 0)
                        {
                            if (chunkBLocks[x, y, z - 1].type == BlockType.Empty)
                            {
                                IntegrateFace(chunkBLocks[x, y, z], Faces.Back);
                                numFaces++;
                            }
                        }
                        else
                        {
                            IntegrateFace(chunkBLocks[x, y, z], Faces.Back);
                            numFaces++;
                        }

                        AddIndices(numFaces);
                    }
                }
            }
        }

        private void IntegrateFace(Block block, Faces face)
        {
            var faceData = block.GetFace(face);
            chunkVerts.AddRange(faceData.vertices);
            chunkUVs.AddRange(faceData.uv);
        }

        private void AddIndices(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                chunkIndices.Add(0 + indexCount);
                chunkIndices.Add(1 + indexCount);
                chunkIndices.Add(2 + indexCount);
                chunkIndices.Add(2 + indexCount);
                chunkIndices.Add(3 + indexCount);
                chunkIndices.Add(0 + indexCount);

                indexCount += 4;
            }
        }
        private void BuildChunk()
        {
            chunkVAO = new VertexArrayObject();
            chunkVAO.Bind();

            chunkVertexVBO = new VertexBufferObject(chunkVerts);
            chunkVertexVBO.Bind();
            chunkVAO.LinkToVAO(0, 3, chunkVertexVBO);

            chunkUVVBO = new VertexBufferObject(chunkUVs);
            chunkUVVBO.Bind();
            chunkVAO.LinkToVAO(1, 2, chunkUVVBO);

            chunkIBO = new IndexBufferObject(chunkIndices);

            texture = new Texture("atlas.png");
        }
        public void Render(ShaderProgram shaderProgram)
        {
            shaderProgram.Bind();
            chunkVAO!.Bind();
            chunkIBO!.Bind();
            texture!.Bind();

            GL.DrawElements(PrimitiveType.Triangles, chunkIndices.Count, DrawElementsType.UnsignedInt, 0);
        }

        public void Delete()
        {
            chunkVAO!.Delete();
            texture!.Delete();
            chunkUVVBO!.Delete();
            chunkIBO!.Delete();
        }
    }
}
