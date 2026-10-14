using OpenTK.Mathematics;

namespace VoxelRenderEngine.src.Graphics
{
    public class ChunkMesh
    {
        public Vector3[] Vertices { get; }
        public Vector2[] UVs { get; }
        public Vector3[] Colors { get; }
        public Vector3[] Normals { get; }
        public uint[] Indices { get; }

        public ChunkMesh(Vector3[] vertices, Vector2[] uvs, Vector3[] colors, Vector3[] normals, uint[] indices)
        {
            Vertices = vertices;
            UVs = uvs;
            Colors = colors;
            Normals = normals;
            Indices = indices;
        }

        public bool IsEmpty => Indices.Length == 0;
    }
}
