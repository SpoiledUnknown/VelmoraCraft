using Minecraft.src.World;
using OpenTK.Mathematics;

namespace Minecraft.src.Graphics
{
    internal class TextureData
    {
        public static Dictionary<BlockType, Dictionary<Faces, Vector2>> blockTypeUVCoord = new Dictionary<BlockType, Dictionary<Faces, Vector2>>()
        {
            {BlockType.Dirt, new Dictionary<Faces, Vector2>() 
            {
                {Faces.Front, new Vector2(2, 15) },
                {Faces.Back, new Vector2(2, 15) },
                {Faces.Left, new Vector2(2, 15) },
                {Faces.Right, new Vector2(2, 15) },
                {Faces.Top, new Vector2(2, 15) },
                {Faces.Bottom, new Vector2(2, 15) },
            } },

            {BlockType.Grass, new Dictionary<Faces, Vector2>()
            {
                {Faces.Front, new Vector2(3, 15) },
                {Faces.Back, new Vector2(3, 15) },
                {Faces.Left, new Vector2(3, 15) },
                {Faces.Right, new Vector2(3, 15) },
                {Faces.Top, new Vector2(7, 13) },
                {Faces.Bottom, new Vector2(2, 15) },
            } },
        };
    }
}
