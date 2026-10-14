using OpenTK.Mathematics;

namespace VoxelRenderEngine.src.World
{
    public enum BlockType : byte
    {
        Empty = 0,
        Dirt = 1,
        Grass = 2,
        Stone = 3,
        Bedrock = 4,
        Sand = 5,
        Log = 6,
        Leaves = 7,
        Planks = 8,
        Glass = 9,
        Cobblestone = 10,
        CoalOre = 11,
        IronOre = 12,
        Obsidian = 13,
        Water = 14,
        Lava = 15,
        CraftingTable = 16,
        Chest = 17,
        Furnace = 18,
        Coal = 19,
        IronIngot = 20,
        FlintAndSteel = 21,
        Torch = 22,
        DoorLower = 23,
        DoorUpper = 24,
        DoorLowerOpen = 25,
        DoorUpperOpen = 26,
        Lever = 27,
        LeverOn = 28,
        StoneButton = 29,
        StoneButtonPressed = 30,
        Rail = 31,
        PoweredRail = 32,
        PoweredRailOn = 33,
        MinecartItem = 34,
        Portal = 35
    }

    public enum Face : byte
    {
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3,
        Top = 4,
        Bottom = 5
    }

    public enum MeshType : byte
    {
        StandardCube = 0,
        CrossQuad = 1,      // Torches (two crossed vertical quads)
        FlatSurface = 2,    // Rails (horizontal quad on floor)
        DoorClosed = 3,     // Thin vertical quad facing +Z / -Z
        DoorOpen = 4,       // Thin vertical quad rotated 90 degrees
        SwitchLever = 5,    // Lever switch
        WallButton = 6      // Small button box
    }

    public class BlockDefinition
    {
        public BlockType Type { get; }
        public string Name { get; }
        public bool IsSolid { get; }
        public bool IsTransparent { get; }
        public float Hardness { get; }
        public Vector2[] FaceUVCoords { get; }
        public Vector3 TintColor { get; }
        public MeshType MeshType { get; }
        public bool IsLightSource { get; }

        public BlockDefinition(
            BlockType type,
            string name,
            bool isSolid,
            bool isTransparent,
            float hardness,
            Vector2[] faceUVCoords,
            Vector3? tintColor = null,
            MeshType meshType = MeshType.StandardCube,
            bool isLightSource = false)
        {
            Type = type;
            Name = name;
            IsSolid = isSolid;
            IsTransparent = isTransparent;
            Hardness = hardness;
            FaceUVCoords = faceUVCoords;
            TintColor = tintColor ?? Vector3.One;
            MeshType = meshType;
            IsLightSource = isLightSource;
        }

        public Vector2 GetFaceAtlasCoord(Face face)
        {
            return FaceUVCoords[(int)face];
        }
    }

    public static class BlockRegistry
    {
        private static readonly BlockDefinition[] Definitions = new BlockDefinition[256];

        static BlockRegistry()
        {
            // Empty
            Register(new BlockDefinition(
                BlockType.Empty,
                "Air",
                isSolid: false,
                isTransparent: true,
                hardness: 0f,
                faceUVCoords: new Vector2[6]
            ));

            // Dirt (col=2, row=0)
            Register(CreateUniformBlock(BlockType.Dirt, "Dirt", isSolid: true, isTransparent: false, hardness: 0.5f, new Vector2(2, 0)));

            // Grass (Top: col=0, row=0; Side: col=3, row=0; Bottom: col=2, row=0)
            Register(new BlockDefinition(
                BlockType.Grass,
                "Grass Block",
                isSolid: true,
                isTransparent: false,
                hardness: 0.6f,
                faceUVCoords: new[]
                {
                    new Vector2(3, 0), // Front (Side)
                    new Vector2(3, 0), // Back (Side)
                    new Vector2(3, 0), // Left (Side)
                    new Vector2(3, 0), // Right (Side)
                    new Vector2(7, 2), // Top (Grass Top)
                    new Vector2(2, 0)  // Bottom (Dirt)
                }
            ));

            // Stone (col=1, row=0)
            Register(CreateUniformBlock(BlockType.Stone, "Stone", isSolid: true, isTransparent: false, hardness: 1.5f, new Vector2(1, 0)));

            // Bedrock (col=1, row=1)
            Register(CreateUniformBlock(BlockType.Bedrock, "Bedrock", isSolid: true, isTransparent: false, hardness: -1f, new Vector2(1, 1)));

            // Sand (col=2, row=1)
            Register(CreateUniformBlock(BlockType.Sand, "Sand", isSolid: true, isTransparent: false, hardness: 0.5f, new Vector2(2, 1)));

            // Log / Wood (Top/Bottom: col=5, row=1; Sides: col=4, row=1)
            Register(new BlockDefinition(
                BlockType.Log,
                "Wood Log",
                isSolid: true,
                isTransparent: false,
                hardness: 2.0f,
                faceUVCoords: new[]
                {
                    new Vector2(4, 1), // Front
                    new Vector2(4, 1), // Back
                    new Vector2(4, 1), // Left
                    new Vector2(4, 1), // Right
                    new Vector2(5, 1), // Top
                    new Vector2(5, 1)  // Bottom
                }
            ));

            // Leaves (col=4, row=3, with lush green foliage tint!)
            Register(CreateUniformBlock(BlockType.Leaves, "Leaves", isSolid: true, isTransparent: true, hardness: 0.2f, new Vector2(4, 3), tintColor: new Vector3(0.35f, 0.75f, 0.25f)));

            // Planks (col=4, row=0)
            Register(CreateUniformBlock(BlockType.Planks, "Wood Planks", isSolid: true, isTransparent: false, hardness: 1.5f, new Vector2(4, 0)));

            // Glass (col=1, row=3)
            Register(CreateUniformBlock(BlockType.Glass, "Glass", isSolid: true, isTransparent: true, hardness: 0.3f, new Vector2(1, 3)));

            // Cobblestone (col=0, row=1)
            Register(CreateUniformBlock(BlockType.Cobblestone, "Cobblestone", isSolid: true, isTransparent: false, hardness: 2.0f, new Vector2(0, 1)));

            // Coal Ore (col=2, row=2)
            Register(CreateUniformBlock(BlockType.CoalOre, "Coal Ore", isSolid: true, isTransparent: false, hardness: 3.0f, new Vector2(2, 2)));

            // Iron Ore (col=1, row=2)
            Register(CreateUniformBlock(BlockType.IronOre, "Iron Ore", isSolid: true, isTransparent: false, hardness: 3.0f, new Vector2(1, 2)));

            // Obsidian (col=5, row=2)
            Register(CreateUniformBlock(BlockType.Obsidian, "Obsidian", isSolid: true, isTransparent: false, hardness: 50.0f, new Vector2(5, 2)));

            // Water (col=13, row=12)
            Register(CreateUniformBlock(BlockType.Water, "Water", isSolid: false, isTransparent: true, hardness: 100f, new Vector2(13, 12)));

            // Lava (col=13, row=14)
            Register(CreateUniformBlock(BlockType.Lava, "Lava", isSolid: false, isTransparent: false, hardness: 100f, new Vector2(13, 14)));

            // Crafting Table (Top: col=11, row=2; Front/Back: col=11, row=3; Sides: col=12, row=3; Bottom: col=4, row=0 Planks)
            Register(new BlockDefinition(
                BlockType.CraftingTable,
                "Crafting Table",
                isSolid: true,
                isTransparent: false,
                hardness: 2.5f,
                faceUVCoords: new[]
                {
                    new Vector2(11, 3), // Front
                    new Vector2(11, 3), // Back
                    new Vector2(12, 3), // Left
                    new Vector2(12, 3), // Right
                    new Vector2(11, 2), // Top
                    new Vector2(4, 0)   // Bottom (Planks)
                }
            ));

            // Chest (Front: col=11, row=1; Sides/Back: col=10, row=1; Top/Bottom: col=9, row=1)
            Register(new BlockDefinition(
                BlockType.Chest,
                "Chest",
                isSolid: true,
                isTransparent: false,
                hardness: 2.5f,
                faceUVCoords: new[]
                {
                    new Vector2(11, 1), // Front
                    new Vector2(10, 1), // Back
                    new Vector2(10, 1), // Left
                    new Vector2(10, 1), // Right
                    new Vector2(9, 1),  // Top
                    new Vector2(9, 1)   // Bottom
                }
            ));

            // Furnace (Front: col=12, row=2; Sides/Back: col=13, row=2; Top/Bottom: col=14, row=3)
            Register(new BlockDefinition(
                BlockType.Furnace,
                "Furnace",
                isSolid: true,
                isTransparent: false,
                hardness: 3.5f,
                faceUVCoords: new[]
                {
                    new Vector2(12, 2), // Front
                    new Vector2(13, 2), // Back
                    new Vector2(13, 2), // Left
                    new Vector2(13, 2), // Right
                    new Vector2(14, 3), // Top
                    new Vector2(14, 3)  // Bottom
                }
            ));

            // Items (represented via atlas icons or colored blocks)
            Register(CreateUniformBlock(BlockType.Coal, "Coal", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(2, 2)));
            Register(CreateUniformBlock(BlockType.IronIngot, "Iron Ingot", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(1, 2)));
            Register(CreateUniformBlock(BlockType.FlintAndSteel, "Flint and Steel", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(5, 2)));

            // 1. Torch (Cross-quad, light emitting)
            Register(CreateUniformBlock(BlockType.Torch, "Torch", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(0, 5), meshType: MeshType.CrossQuad, isLightSource: true));

            // 2. Wooden Door
            // Lower Half (Closed)
            Register(CreateUniformBlock(BlockType.DoorLower, "Wooden Door", isSolid: true, isTransparent: true, hardness: 1.5f, new Vector2(1, 6), meshType: MeshType.DoorClosed));
            // Upper Half (Closed)
            Register(CreateUniformBlock(BlockType.DoorUpper, "Wooden Door", isSolid: true, isTransparent: true, hardness: 1.5f, new Vector2(1, 5), meshType: MeshType.DoorClosed));
            // Lower Half (Open)
            Register(CreateUniformBlock(BlockType.DoorLowerOpen, "Wooden Door", isSolid: false, isTransparent: true, hardness: 1.5f, new Vector2(1, 6), meshType: MeshType.DoorOpen));
            // Upper Half (Open)
            Register(CreateUniformBlock(BlockType.DoorUpperOpen, "Wooden Door", isSolid: false, isTransparent: true, hardness: 1.5f, new Vector2(1, 5), meshType: MeshType.DoorOpen));

            // 3. Lever
            Register(CreateUniformBlock(BlockType.Lever, "Lever", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(0, 6), meshType: MeshType.SwitchLever));
            Register(CreateUniformBlock(BlockType.LeverOn, "Lever (Active)", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(0, 6), tintColor: new Vector3(1.4f, 0.4f, 0.3f), meshType: MeshType.SwitchLever));

            // 4. Stone Button
            Register(CreateUniformBlock(BlockType.StoneButton, "Stone Button", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(1, 0), meshType: MeshType.WallButton));
            Register(CreateUniformBlock(BlockType.StoneButtonPressed, "Stone Button (Pressed)", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(1, 0), tintColor: new Vector3(1.3f, 0.4f, 0.3f), meshType: MeshType.WallButton));

            // 5. Rails
            Register(CreateUniformBlock(BlockType.Rail, "Rail", isSolid: false, isTransparent: true, hardness: 0.7f, new Vector2(0, 7), meshType: MeshType.FlatSurface));
            Register(CreateUniformBlock(BlockType.PoweredRail, "Powered Rail", isSolid: false, isTransparent: true, hardness: 0.7f, new Vector2(0, 8), meshType: MeshType.FlatSurface));
            Register(CreateUniformBlock(BlockType.PoweredRailOn, "Powered Rail (Active)", isSolid: false, isTransparent: true, hardness: 0.7f, new Vector2(0, 8), tintColor: new Vector3(1.4f, 0.4f, 0.3f), meshType: MeshType.FlatSurface));

            // 6. Minecart Item (Icon with black-pink texture & metallic tint)
            Register(CreateUniformBlock(BlockType.MinecartItem, "Minecart", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(6, 0)));

            // 7. Nether Portal Block (Translucent, glowing purple endgame rift)
            Register(CreateUniformBlock(BlockType.Portal, "Nether Portal", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(9, 2), tintColor: new Vector3(0.75f, 0.20f, 0.95f), isLightSource: true));
        }

        private static BlockDefinition CreateUniformBlock(
            BlockType type,
            string name,
            bool isSolid,
            bool isTransparent,
            float hardness,
            Vector2 uv,
            Vector3? tintColor = null,
            MeshType meshType = MeshType.StandardCube,
            bool isLightSource = false)
        {
            return new BlockDefinition(
                type,
                name,
                isSolid,
                isTransparent,
                hardness,
                new[] { uv, uv, uv, uv, uv, uv },
                tintColor,
                meshType,
                isLightSource
            );
        }

        private static void Register(BlockDefinition def)
        {
            Definitions[(byte)def.Type] = def;
        }

        public static BlockDefinition Get(BlockType type)
        {
            return Definitions[(byte)type] ?? Definitions[0];
        }

        public static BlockDefinition Get(byte type)
        {
            return Definitions[type] ?? Definitions[0];
        }

        public static bool IsTransparent(byte type)
        {
            return (Definitions[type]?.IsTransparent) ?? true;
        }

        public static bool IsSolid(byte type)
        {
            return (Definitions[type]?.IsSolid) ?? false;
        }
    }
}
