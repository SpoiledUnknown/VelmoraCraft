using OpenTK.Mathematics;

namespace VelmoraCraft.src.World
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
        Portal = 35,

        // Ores & Minerals
        GoldOre = 36,
        DiamondOre = 37,
        RedstoneOre = 38,
        LapisOre = 39,
        GoldBlock = 40,
        IronBlock = 41,
        DiamondBlock = 42,
        LapisBlock = 43,

        // Natural Terrain & Wood
        Gravel = 44,
        Clay = 45,
        Sandstone = 46,
        BirchLog = 47,
        SpruceLog = 48,
        SpruceLeaves = 49,
        SnowBlock = 50,
        SnowyGrass = 51,
        Ice = 52,
        Cactus = 53,
        SugarCane = 54,

        // Flora, Plants & Crops
        Rose = 55,
        Dandelion = 56,
        RedMushroom = 57,
        BrownMushroom = 58,
        OakSapling = 59,
        Wheat = 60,
        Farmland = 61,

        // Construction & Building
        Bricks = 62,
        SmoothStone = 63,
        DoubleStoneSlab = 64,
        MossyCobblestone = 65,
        Bookshelf = 66,
        Sponge = 67,
        Ladder = 68,
        Trapdoor = 69,
        Cobweb = 70,

        // Nether Blocks
        Netherrack = 71,
        SoulSand = 72,
        Glowstone = 73,

        // Redstone, Interactive & Food
        TNT = 74,
        MonsterSpawner = 75,
        Dispenser = 76,
        LitFurnace = 77,
        RedstoneTorch = 78,
        Jukebox = 79,
        Pumpkin = 80,
        CarvedPumpkin = 81,
        JackOLantern = 82,
        Cake = 83,

        // 16 Wool Colors
        WhiteWool = 84,
        OrangeWool = 85,
        MagentaWool = 86,
        LightBlueWool = 87,
        YellowWool = 88,
        LimeWool = 89,
        PinkWool = 90,
        GrayWool = 91,
        LightGrayWool = 92,
        CyanWool = 93,
        PurpleWool = 94,
        BlueWool = 95,
        BrownWool = 96,
        GreenWool = 97,
        RedWool = 98,
        BlackWool = 99,
        RedstoneBlock = 100,
        CoalBlock = 101,

        // Iron Door
        IronDoorLower = 102,
        IronDoorUpper = 103,
        IronDoorLowerOpen = 104,
        IronDoorUpperOpen = 105,

        // Additional Flora, Bed & Items
        TallGrass = 106,
        BedFoot = 107,
        BedHead = 108,
        WheatSeeds = 109,
        Bread = 110
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

    public enum BlockFacing : byte
    {
        South = 0, // Faces +Z (default)
        North = 1, // Faces -Z (180 deg)
        West = 2,  // Faces -X (90 deg CCW)
        East = 3   // Faces +X (90 deg CW)
    }

    public enum RailShape : byte
    {
        NorthSouth = 0, // Straight along Z (0 deg)
        EastWest = 1,   // Straight along X (90 deg)
        SouthEast = 2,  // Curve connecting South (+Z) and East (+X)
        SouthWest = 3,  // Curve connecting South (+Z) and West (-X)
        NorthWest = 4,  // Curve connecting North (-Z) and West (-X)
        NorthEast = 5   // Curve connecting North (-Z) and East (+X)
    }

    public enum MeshType : byte
    {
        StandardCube = 0,
        CrossQuad = 1,      // Torches (two crossed vertical quads)
        FlatSurface = 2,    // Rails (horizontal quad on floor)
        DoorClosed = 3,     // Thin vertical quad facing +Z / -Z
        DoorOpen = 4,       // Thin vertical quad rotated 90 degrees
        SwitchLever = 5,    // Lever switch
        WallButton = 6,     // Small button box
        Bed = 7             // Bed block
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

            // 2b. Iron Door
            // Lower Half (Closed)
            Register(CreateUniformBlock(BlockType.IronDoorLower, "Iron Door", isSolid: true, isTransparent: true, hardness: 5.0f, new Vector2(2, 6), meshType: MeshType.DoorClosed));
            // Upper Half (Closed)
            Register(CreateUniformBlock(BlockType.IronDoorUpper, "Iron Door", isSolid: true, isTransparent: true, hardness: 5.0f, new Vector2(2, 5), meshType: MeshType.DoorClosed));
            // Lower Half (Open)
            Register(CreateUniformBlock(BlockType.IronDoorLowerOpen, "Iron Door", isSolid: false, isTransparent: true, hardness: 5.0f, new Vector2(2, 6), meshType: MeshType.DoorOpen));
            // Upper Half (Open)
            Register(CreateUniformBlock(BlockType.IronDoorUpperOpen, "Iron Door", isSolid: false, isTransparent: true, hardness: 5.0f, new Vector2(2, 5), meshType: MeshType.DoorOpen));

            // 3. Lever
            Register(CreateUniformBlock(BlockType.Lever, "Lever", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(0, 6), meshType: MeshType.SwitchLever));
            Register(CreateUniformBlock(BlockType.LeverOn, "Lever (Active)", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(0, 6), tintColor: new Vector3(1.4f, 0.4f, 0.3f), meshType: MeshType.SwitchLever));

            // 4. Stone Button
            Register(CreateUniformBlock(BlockType.StoneButton, "Stone Button", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(1, 0), meshType: MeshType.WallButton));
            Register(CreateUniformBlock(BlockType.StoneButtonPressed, "Stone Button (Pressed)", isSolid: false, isTransparent: true, hardness: 0.5f, new Vector2(1, 0), tintColor: new Vector3(1.3f, 0.4f, 0.3f), meshType: MeshType.WallButton));

            // 5. Rails
            Register(CreateUniformBlock(BlockType.Rail, "Rail", isSolid: false, isTransparent: true, hardness: 0.7f, new Vector2(0, 8), meshType: MeshType.FlatSurface));
            Register(CreateUniformBlock(BlockType.PoweredRail, "Powered Rail", isSolid: false, isTransparent: true, hardness: 0.7f, new Vector2(0, 8), meshType: MeshType.FlatSurface));
            Register(CreateUniformBlock(BlockType.PoweredRailOn, "Powered Rail (Active)", isSolid: false, isTransparent: true, hardness: 0.7f, new Vector2(0, 8), tintColor: new Vector3(1.4f, 0.4f, 0.3f), meshType: MeshType.FlatSurface));

            // 6. Minecart Item (Icon with black-pink texture & metallic tint)
            Register(CreateUniformBlock(BlockType.MinecartItem, "Minecart", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(6, 0)));

            // 7. Nether Portal Block (Translucent, glowing purple endgame rift)
            Register(CreateUniformBlock(BlockType.Portal, "Nether Portal", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(14, 12), tintColor: new Vector3(0.75f, 0.20f, 0.95f), isLightSource: true));

            // 8. Bed (Foot top at (6,8), Head top at (7,8), Foot End (5,9), Foot Side (6,9), Head End (8,9), Head Side (7,9))
            Register(new BlockDefinition(
                BlockType.BedFoot,
                "Bed",
                isSolid: true,
                isTransparent: true,
                hardness: 0.2f,
                faceUVCoords: new[]
                {
                    new Vector2(5, 9), // Front (Foot end)
                    new Vector2(6, 9), // Back (Inner)
                    new Vector2(6, 9), // Left (Side)
                    new Vector2(6, 9), // Right (Side)
                    new Vector2(6, 8), // Top (Foot top)
                    new Vector2(4, 0)  // Bottom (Wood)
                },
                meshType: MeshType.Bed
            ));
            Register(new BlockDefinition(
                BlockType.BedHead,
                "Bed",
                isSolid: true,
                isTransparent: true,
                hardness: 0.2f,
                faceUVCoords: new[]
                {
                    new Vector2(8, 9), // Front (Head end)
                    new Vector2(7, 9), // Back (Inner)
                    new Vector2(7, 9), // Left (Side)
                    new Vector2(7, 9), // Right (Side)
                    new Vector2(7, 8), // Top (Head top)
                    new Vector2(4, 0)  // Bottom (Wood)
                },
                meshType: MeshType.Bed
            ));

            // --- Ores & Minerals ---
            Register(CreateUniformBlock(BlockType.GoldOre, "Gold Ore", isSolid: true, isTransparent: false, hardness: 3.0f, new Vector2(0, 2)));
            Register(CreateUniformBlock(BlockType.DiamondOre, "Diamond Ore", isSolid: true, isTransparent: false, hardness: 3.5f, new Vector2(2, 3)));
            Register(CreateUniformBlock(BlockType.RedstoneOre, "Redstone Ore", isSolid: true, isTransparent: false, hardness: 3.0f, new Vector2(3, 3), isLightSource: true));
            Register(CreateUniformBlock(BlockType.LapisOre, "Lapis Lazuli Ore", isSolid: true, isTransparent: false, hardness: 3.0f, new Vector2(0, 10)));
            Register(CreateUniformBlock(BlockType.GoldBlock, "Block of Gold", isSolid: true, isTransparent: false, hardness: 3.0f, new Vector2(7, 1)));
            Register(CreateUniformBlock(BlockType.IronBlock, "Block of Iron", isSolid: true, isTransparent: false, hardness: 5.0f, new Vector2(6, 1)));
            Register(CreateUniformBlock(BlockType.DiamondBlock, "Block of Diamond", isSolid: true, isTransparent: false, hardness: 5.0f, new Vector2(8, 1)));
            Register(CreateUniformBlock(BlockType.LapisBlock, "Lapis Lazuli Block", isSolid: true, isTransparent: false, hardness: 3.0f, new Vector2(0, 9)));

            // --- Natural Terrain & Wood ---
            Register(CreateUniformBlock(BlockType.Gravel, "Gravel", isSolid: true, isTransparent: false, hardness: 0.6f, new Vector2(3, 1)));
            Register(CreateUniformBlock(BlockType.Clay, "Clay Block", isSolid: true, isTransparent: false, hardness: 0.6f, new Vector2(8, 4)));
            Register(CreateMultiFaceBlock(BlockType.Sandstone, "Sandstone", isSolid: true, isTransparent: false, hardness: 0.8f, top: new Vector2(0, 11), side: new Vector2(0, 12), bottom: new Vector2(0, 13)));
            Register(CreateMultiFaceBlock(BlockType.BirchLog, "Birch Log", isSolid: true, isTransparent: false, hardness: 2.0f, top: new Vector2(5, 1), side: new Vector2(5, 7), bottom: new Vector2(5, 1)));
            Register(CreateMultiFaceBlock(BlockType.SpruceLog, "Spruce Log", isSolid: true, isTransparent: false, hardness: 2.0f, top: new Vector2(5, 1), side: new Vector2(4, 7), bottom: new Vector2(5, 1)));
            Register(CreateUniformBlock(BlockType.SpruceLeaves, "Spruce Leaves", isSolid: true, isTransparent: true, hardness: 0.2f, new Vector2(5, 3), tintColor: new Vector3(0.35f, 0.65f, 0.35f)));
            Register(CreateUniformBlock(BlockType.SnowBlock, "Snow Block", isSolid: true, isTransparent: false, hardness: 0.2f, new Vector2(2, 4)));
            Register(CreateMultiFaceBlock(BlockType.SnowyGrass, "Snowy Grass", isSolid: true, isTransparent: false, hardness: 0.6f, top: new Vector2(2, 4), side: new Vector2(4, 4), bottom: new Vector2(2, 0)));
            Register(CreateUniformBlock(BlockType.Ice, "Ice", isSolid: true, isTransparent: true, hardness: 0.5f, new Vector2(3, 4)));
            Register(CreateMultiFaceBlock(BlockType.Cactus, "Cactus", isSolid: true, isTransparent: true, hardness: 0.4f, top: new Vector2(5, 4), side: new Vector2(6, 4), bottom: new Vector2(7, 4)));
            Register(CreateUniformBlock(BlockType.SugarCane, "Sugar Cane", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(9, 4), meshType: MeshType.CrossQuad));

            // --- Flora, Plants & Crops ---
            Register(CreateUniformBlock(BlockType.TallGrass, "Tall Grass", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(13, 5), tintColor: new Vector3(0.85f, 1.0f, 0.85f), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.Rose, "Rose", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(12, 0), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.Dandelion, "Dandelion", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(13, 0), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.RedMushroom, "Red Mushroom", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(12, 1), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.BrownMushroom, "Brown Mushroom", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(13, 1), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.OakSapling, "Oak Sapling", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(15, 0), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.Wheat, "Wheat", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(15, 5), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.WheatSeeds, "Wheat Seeds", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(8, 5)));
            Register(CreateMultiFaceBlock(BlockType.Farmland, "Farmland", isSolid: true, isTransparent: false, hardness: 0.6f, top: new Vector2(6, 5), side: new Vector2(2, 0), bottom: new Vector2(2, 0)));

            // --- Construction & Building ---
            Register(CreateUniformBlock(BlockType.Bricks, "Bricks", isSolid: true, isTransparent: false, hardness: 2.0f, new Vector2(7, 0)));
            Register(CreateUniformBlock(BlockType.SmoothStone, "Smooth Stone", isSolid: true, isTransparent: false, hardness: 1.5f, new Vector2(6, 0)));
            Register(CreateMultiFaceBlock(BlockType.DoubleStoneSlab, "Double Stone Slab", isSolid: true, isTransparent: false, hardness: 2.0f, top: new Vector2(6, 0), side: new Vector2(5, 0), bottom: new Vector2(6, 0)));
            Register(CreateUniformBlock(BlockType.MossyCobblestone, "Mossy Cobblestone", isSolid: true, isTransparent: false, hardness: 2.0f, new Vector2(4, 2)));
            Register(CreateMultiFaceBlock(BlockType.Bookshelf, "Bookshelf", isSolid: true, isTransparent: false, hardness: 1.5f, top: new Vector2(4, 0), side: new Vector2(3, 2), bottom: new Vector2(4, 0)));
            Register(CreateUniformBlock(BlockType.Sponge, "Sponge", isSolid: true, isTransparent: false, hardness: 0.6f, new Vector2(0, 3)));
            Register(CreateUniformBlock(BlockType.Ladder, "Ladder", isSolid: false, isTransparent: true, hardness: 0.4f, new Vector2(3, 5), meshType: MeshType.CrossQuad));
            Register(CreateUniformBlock(BlockType.Trapdoor, "Trapdoor", isSolid: false, isTransparent: true, hardness: 1.5f, new Vector2(4, 5)));
            Register(CreateUniformBlock(BlockType.Cobweb, "Cobweb", isSolid: false, isTransparent: true, hardness: 4.0f, new Vector2(11, 0), meshType: MeshType.CrossQuad));

            // --- Nether Blocks ---
            Register(CreateUniformBlock(BlockType.Netherrack, "Netherrack", isSolid: true, isTransparent: false, hardness: 0.4f, new Vector2(7, 6)));
            Register(CreateUniformBlock(BlockType.SoulSand, "Soul Sand", isSolid: true, isTransparent: false, hardness: 0.5f, new Vector2(8, 6)));
            Register(CreateUniformBlock(BlockType.Glowstone, "Glowstone", isSolid: true, isTransparent: false, hardness: 0.3f, new Vector2(9, 6), isLightSource: true));

            // --- Redstone, Interactive & Food ---
            Register(CreateMultiFaceBlock(BlockType.TNT, "TNT", isSolid: true, isTransparent: false, hardness: 0f, top: new Vector2(9, 0), side: new Vector2(8, 0), bottom: new Vector2(10, 0)));
            Register(CreateUniformBlock(BlockType.MonsterSpawner, "Monster Spawner", isSolid: true, isTransparent: true, hardness: 5.0f, new Vector2(1, 4)));
            Register(CreateDirectionalBlock(BlockType.Dispenser, "Dispenser", isSolid: true, isTransparent: false, hardness: 3.5f, front: new Vector2(14, 2), side: new Vector2(13, 2), top: new Vector2(14, 3)));
            Register(CreateDirectionalBlock(BlockType.LitFurnace, "Burning Furnace", isSolid: true, isTransparent: false, hardness: 3.5f, front: new Vector2(13, 3), side: new Vector2(13, 2), top: new Vector2(14, 3), isLightSource: true));
            Register(CreateUniformBlock(BlockType.RedstoneTorch, "Redstone Torch", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(3, 6), meshType: MeshType.CrossQuad, isLightSource: true));
            Register(CreateMultiFaceBlock(BlockType.Jukebox, "Jukebox", isSolid: true, isTransparent: false, hardness: 2.0f, top: new Vector2(11, 4), side: new Vector2(10, 4), bottom: new Vector2(10, 4)));
            Register(CreateMultiFaceBlock(BlockType.Pumpkin, "Pumpkin", isSolid: true, isTransparent: false, hardness: 1.0f, top: new Vector2(6, 6), side: new Vector2(6, 7), bottom: new Vector2(6, 6)));
            Register(CreateDirectionalBlock(BlockType.CarvedPumpkin, "Carved Pumpkin", isSolid: true, isTransparent: false, hardness: 1.0f, front: new Vector2(7, 7), side: new Vector2(6, 7), top: new Vector2(6, 6)));
            Register(CreateDirectionalBlock(BlockType.JackOLantern, "Jack o'Lantern", isSolid: true, isTransparent: false, hardness: 1.0f, front: new Vector2(8, 7), side: new Vector2(6, 7), top: new Vector2(6, 6), isLightSource: true));
            Register(CreateMultiFaceBlock(BlockType.Cake, "Cake", isSolid: true, isTransparent: true, hardness: 0.5f, top: new Vector2(9, 7), side: new Vector2(10, 1), bottom: new Vector2(12, 7)));
            Register(CreateUniformBlock(BlockType.Bread, "Bread", isSolid: false, isTransparent: true, hardness: 0f, new Vector2(8, 5), tintColor: new Vector3(1.35f, 0.95f, 0.50f)));

            // --- 16 Wool Colors ---
            Register(CreateUniformBlock(BlockType.WhiteWool, "White Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(0, 4)));
            Register(CreateUniformBlock(BlockType.OrangeWool, "Orange Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(2, 14)));
            Register(CreateUniformBlock(BlockType.MagentaWool, "Magenta Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(2, 13)));
            Register(CreateUniformBlock(BlockType.LightBlueWool, "Light Blue Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(2, 12)));
            Register(CreateUniformBlock(BlockType.YellowWool, "Yellow Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(2, 11)));
            Register(CreateUniformBlock(BlockType.LimeWool, "Lime Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(2, 10)));
            Register(CreateUniformBlock(BlockType.PinkWool, "Pink Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(2, 9)));
            Register(CreateUniformBlock(BlockType.GrayWool, "Gray Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(2, 8)));
            Register(CreateUniformBlock(BlockType.LightGrayWool, "Light Gray Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 15)));
            Register(CreateUniformBlock(BlockType.CyanWool, "Cyan Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 14)));
            Register(CreateUniformBlock(BlockType.PurpleWool, "Purple Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 13)));
            Register(CreateUniformBlock(BlockType.BlueWool, "Blue Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 12)));
            Register(CreateUniformBlock(BlockType.BrownWool, "Brown Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 11)));
            Register(CreateUniformBlock(BlockType.GreenWool, "Green Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 10)));
            Register(CreateUniformBlock(BlockType.RedWool, "Red Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 9)));
            Register(CreateUniformBlock(BlockType.BlackWool, "Black Wool", isSolid: true, isTransparent: false, hardness: 0.8f, new Vector2(1, 8)));
            Register(CreateUniformBlock(BlockType.RedstoneBlock, "Block of Redstone", isSolid: true, isTransparent: false, hardness: 5.0f, new Vector2(8, 1), tintColor: new Vector3(0.95f, 0.15f, 0.15f), isLightSource: true));
            Register(CreateUniformBlock(BlockType.CoalBlock, "Block of Coal", isSolid: true, isTransparent: false, hardness: 5.0f, new Vector2(6, 1), tintColor: new Vector3(0.12f, 0.12f, 0.12f)));
        }

        private static BlockDefinition CreateMultiFaceBlock(
            BlockType type,
            string name,
            bool isSolid,
            bool isTransparent,
            float hardness,
            Vector2 top,
            Vector2 side,
            Vector2 bottom,
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
                new[] { side, side, side, side, top, bottom },
                tintColor,
                meshType,
                isLightSource
            );
        }

        private static BlockDefinition CreateDirectionalBlock(
            BlockType type,
            string name,
            bool isSolid,
            bool isTransparent,
            float hardness,
            Vector2 front,
            Vector2 side,
            Vector2 top,
            Vector3? tintColor = null,
            bool isLightSource = false)
        {
            return new BlockDefinition(
                type,
                name,
                isSolid,
                isTransparent,
                hardness,
                new[] { front, side, side, side, top, top },
                tintColor,
                MeshType.StandardCube,
                isLightSource
            );
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

        public static bool IsDirectionalBlock(BlockType type) =>
            type == BlockType.Furnace ||
            type == BlockType.LitFurnace ||
            type == BlockType.Dispenser ||
            type == BlockType.CarvedPumpkin ||
            type == BlockType.JackOLantern ||
            type == BlockType.CraftingTable ||
            type == BlockType.Chest;

        public static bool IsRailBlock(BlockType type) =>
            type == BlockType.Rail ||
            type == BlockType.PoweredRail ||
            type == BlockType.PoweredRailOn;

        public static bool IsRailBlock(byte type) => IsRailBlock((BlockType)type);

        public static bool IsDoorBlock(BlockType type) =>
            type == BlockType.DoorLower ||
            type == BlockType.DoorUpper ||
            type == BlockType.DoorLowerOpen ||
            type == BlockType.DoorUpperOpen ||
            type == BlockType.IronDoorLower ||
            type == BlockType.IronDoorUpper ||
            type == BlockType.IronDoorLowerOpen ||
            type == BlockType.IronDoorUpperOpen;

        public static bool IsDoorBlock(byte type) => IsDoorBlock((BlockType)type);
    }
}
