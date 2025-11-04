using Minecraft.src.Graphics;
using OpenTK.Mathematics;

namespace Minecraft.src.World
{
    internal class Block
    {
        public BlockType type;

        private Vector3 position;
        private Dictionary<Faces, FaceData> faces;

        private Dictionary<Faces, List<Vector2>> blockUVs = new Dictionary<Faces, List<Vector2>>()
        {
            {Faces.Front, new List<Vector2>() },
            {Faces.Back, new List<Vector2>() },
            {Faces.Left, new List<Vector2>() },
            {Faces.Right, new List<Vector2>() },
            {Faces.Top, new List<Vector2>() },
            {Faces.Bottom, new List<Vector2>() },
        };

        private Dictionary<Faces, List<Vector2>> GetUVsFromCoordinates(Dictionary<Faces, Vector2> coords)
        {
            Dictionary<Faces, List<Vector2>> faceData = new Dictionary<Faces, List<Vector2>>();
            foreach (var faceCoord in coords)
            {
                faceData[faceCoord.Key] = new List<Vector2>()
                {
                    new Vector2((faceCoord.Value.X + 1f) / 16f, (faceCoord.Value.Y + 1f) / 16f),
                    new Vector2(faceCoord.Value.X / 16f, (faceCoord.Value.Y + 1f) / 16f),
                    new Vector2(faceCoord.Value.X / 16f, faceCoord.Value.Y / 16f),
                    new Vector2((faceCoord.Value.X + 1f) / 16f, faceCoord.Value.Y / 16f)
                };
            }

            return faceData;
        }

        public Block(Vector3 position, BlockType blockType = BlockType.Empty)
        {
            type = blockType;
            this.position = position;

            if (blockType != BlockType.Empty)
            {
                blockUVs = GetUVsFromCoordinates(TextureData.blockTypeUVCoord[blockType]);
            }

            faces = new Dictionary<Faces, FaceData>
            {
                {Faces.Front, new FaceData {
                    vertices = AddTransformedVertices(RawFaceData.rawVertexData[Faces.Front]),
                    uv = blockUVs[Faces.Front]
                } },

                {Faces.Back, new FaceData {
                    vertices = AddTransformedVertices(RawFaceData.rawVertexData[Faces.Back]),
                    uv = blockUVs[Faces.Back]
                } },

                {Faces.Left, new FaceData {
                    vertices = AddTransformedVertices(RawFaceData.rawVertexData[Faces.Left]),
                    uv = blockUVs[Faces.Left]
                } },

                {Faces.Right, new FaceData {
                    vertices = AddTransformedVertices(RawFaceData.rawVertexData[Faces.Right]),
                    uv = blockUVs[Faces.Right]
                } },

                {Faces.Top, new FaceData {
                    vertices = AddTransformedVertices(RawFaceData.rawVertexData[Faces.Top]),
                    uv = blockUVs[Faces.Top]
                } },

                {Faces.Bottom, new FaceData {
                    vertices = AddTransformedVertices(RawFaceData.rawVertexData[Faces.Bottom]),
                    uv = blockUVs[Faces.Bottom]
                } },
            };
        }


        public List<Vector3> AddTransformedVertices(List<Vector3> vertices)
        {
            List<Vector3> transformVertices = new List<Vector3>();
            foreach (var vert in vertices)
            {
                transformVertices.Add(vert + position);
            }

            return transformVertices;
        }
        public FaceData GetFace(Faces face)
        {
            return faces[face];
        }
    }
}
