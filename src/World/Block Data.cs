using OpenTK.Mathematics;

namespace Minecraft.src.World
{
    public enum BlockType
    {
        Dirt,
        Grass,
        Empty
    }

    public enum Faces
    {
        Front,
        Back,
        Left,
        Right,
        Top,
        Bottom
    }

    public struct FaceData
    {
        public List<Vector3> vertices;
        public List<Vector2> uv;
    }

    public struct RawFaceData
    {

        public static readonly Dictionary<Faces, List<Vector3>> rawVertexData = new Dictionary<Faces, List<Vector3>>
        {
            {Faces.Front, new List<Vector3>()
            {
                new Vector3(-0.5f, 0.5f, 0.5f), // topleft vert
                new Vector3(0.5f, 0.5f, 0.5f), // topright vert
                new Vector3(0.5f, -0.5f, 0.5f), // bottomright vert
                new Vector3(-0.5f, -0.5f, 0.5f), // bottomleft vert
            } },
            {Faces.Back, new List<Vector3>()
            {
                new Vector3(0.5f, 0.5f, -0.5f), // topleft vert
                new Vector3(-0.5f, 0.5f, -0.5f), // topright vert
                new Vector3(-0.5f, -0.5f, -0.5f), // bottomright vert
                new Vector3(0.5f, -0.5f, -0.5f), // bottomleft vert
            } },
            {Faces.Left, new List<Vector3>()
            {
                new Vector3(-0.5f, 0.5f, -0.5f), // topleft vert
                new Vector3(-0.5f, 0.5f, 0.5f), // topright vert
                new Vector3(-0.5f, -0.5f, 0.5f), // bottomright vert
                new Vector3(-0.5f, -0.5f, -0.5f), // bottomleft vert
            } },
            {Faces.Right, new List<Vector3>()
            {
                new Vector3(0.5f, 0.5f, 0.5f), // topleft vert
                new Vector3(0.5f, 0.5f, -0.5f), // topright vert
                new Vector3(0.5f, -0.5f, -0.5f), // bottomright vert
                new Vector3(0.5f, -0.5f, 0.5f), // bottomleft vert
            } },
            {Faces.Top, new List<Vector3>()
            {
                new Vector3(-0.5f, 0.5f, -0.5f), // topleft vert
                new Vector3(0.5f, 0.5f, -0.5f), // topright vert
                new Vector3(0.5f, 0.5f, 0.5f), // bottomright vert
                new Vector3(-0.5f, 0.5f, 0.5f), // bottomleft vert
            } },
            {Faces.Bottom, new List<Vector3>()
            {
                new Vector3(-0.5f, -0.5f, 0.5f), // topleft vert
                new Vector3(0.5f, -0.5f, 0.5f), // topright vert
                new Vector3(0.5f, -0.5f, -0.5f), // bottomright vert
                new Vector3(-0.5f, -0.5f, -0.5f), // bottomleft vert
            } },
        };
    }

}
