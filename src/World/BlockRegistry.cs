using OpenTK.Mathematics;

namespace VelmoraCraft.src.World;

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
    GoldOre = 36,
    DiamondOre = 37,
    RedstoneOre = 38,
    LapisOre = 39,
    GoldBlock = 40,
    IronBlock = 41,
    DiamondBlock = 42,
    LapisBlock = 43,
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
    Rose = 55,
    Dandelion = 56,
    RedMushroom = 57,
    BrownMushroom = 58,
    OakSapling = 59,
    Wheat = 60,
    Farmland = 61,
    Bricks = 62,
    SmoothStone = 63,
    DoubleStoneSlab = 64,
    MossyCobblestone = 65,
    Bookshelf = 66,
    Sponge = 67,
    Ladder = 68,
    Trapdoor = 69,
    Cobweb = 70,
    Netherrack = 71,
    SoulSand = 72,
    Glowstone = 73,
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
    IronDoorLower = 102,
    IronDoorUpper = 103,
    IronDoorLowerOpen = 104,
    IronDoorUpperOpen = 105,
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
    West = 2, // Faces -X (90 deg CCW)
    East = 3 // Faces +X (90 deg CW)
}

public enum RailShape : byte
{
    NorthSouth = 0, // Straight along Z (0 deg)
    EastWest = 1, // Straight along X (90 deg)
    SouthEast = 2, // Curve connecting South (+Z) and East (+X)
    SouthWest = 3, // Curve connecting South (+Z) and West (-X)
    NorthWest = 4, // Curve connecting North (-Z) and West (-X)
    NorthEast = 5 // Curve connecting North (-Z) and East (+X)
}

public enum MeshType : byte
{
    StandardCube = 0,
    CrossQuad = 1, // Torches (two crossed vertical quads)
    FlatSurface = 2, // Rails (horizontal quad on floor)
    DoorClosed = 3, // Thin vertical quad facing +Z / -Z
    DoorOpen = 4, // Thin vertical quad rotated 90 degrees
    SwitchLever = 5, // Lever switch
    WallButton = 6, // Small button box
    Bed = 7 // Bed block
}

public class BlockDefinition
{
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

    public BlockType Type { get; }
    public string Name { get; }
    public bool IsSolid { get; }
    public bool IsTransparent { get; }
    public float Hardness { get; }
    public Vector2[] FaceUVCoords { get; }
    public Vector3 TintColor { get; }
    public MeshType MeshType { get; }
    public bool IsLightSource { get; }

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
            false,
            true,
            0f,
            new Vector2[6]
        ));

        // Dirt (col=2, row=0)
        Register(CreateUniformBlock(BlockType.Dirt, "Dirt", true, false, 0.5f, new Vector2(2, 0)));

        // Grass (Top: col=0, row=0; Side: col=3, row=0; Bottom: col=2, row=0)
        Register(new BlockDefinition(
            BlockType.Grass,
            "Grass Block",
            true,
            false,
            0.6f,
            new[]
            {
                new Vector2(3, 0), // Front (Side)
                new Vector2(3, 0), // Back (Side)
                new Vector2(3, 0), // Left (Side)
                new Vector2(3, 0), // Right (Side)
                new Vector2(7, 2), // Top (Grass Top)
                new Vector2(2, 0) // Bottom (Dirt)
            }
        ));

        // Stone (col=1, row=0)
        Register(CreateUniformBlock(BlockType.Stone, "Stone", true, false, 1.5f, new Vector2(1, 0)));

        // Bedrock (col=1, row=1)
        Register(CreateUniformBlock(BlockType.Bedrock, "Bedrock", true, false, -1f, new Vector2(1, 1)));

        // Sand (col=2, row=1)
        Register(CreateUniformBlock(BlockType.Sand, "Sand", true, false, 0.5f, new Vector2(2, 1)));

        // Log / Wood (Top/Bottom: col=5, row=1; Sides: col=4, row=1)
        Register(new BlockDefinition(
            BlockType.Log,
            "Wood Log",
            true,
            false,
            2.0f,
            new[]
            {
                new Vector2(4, 1), // Front
                new Vector2(4, 1), // Back
                new Vector2(4, 1), // Left
                new Vector2(4, 1), // Right
                new Vector2(5, 1), // Top
                new Vector2(5, 1) // Bottom
            }
        ));

        // Leaves (col=4, row=3, with lush green foliage tint!)
        Register(CreateUniformBlock(BlockType.Leaves, "Leaves", true, true, 0.2f, new Vector2(4, 3),
            new Vector3(0.35f, 0.75f, 0.25f)));

        // Planks (col=4, row=0)
        Register(CreateUniformBlock(BlockType.Planks, "Wood Planks", true, false, 1.5f, new Vector2(4, 0)));

        // Glass (col=1, row=3)
        Register(CreateUniformBlock(BlockType.Glass, "Glass", true, true, 0.3f, new Vector2(1, 3)));

        // Cobblestone (col=0, row=1)
        Register(CreateUniformBlock(BlockType.Cobblestone, "Cobblestone", true, false, 2.0f, new Vector2(0, 1)));

        // Coal Ore (col=2, row=2)
        Register(CreateUniformBlock(BlockType.CoalOre, "Coal Ore", true, false, 3.0f, new Vector2(2, 2)));

        // Iron Ore (col=1, row=2)
        Register(CreateUniformBlock(BlockType.IronOre, "Iron Ore", true, false, 3.0f, new Vector2(1, 2)));

        // Obsidian (col=5, row=2)
        Register(CreateUniformBlock(BlockType.Obsidian, "Obsidian", true, false, 50.0f, new Vector2(5, 2)));

        // Water (col=13, row=12)
        Register(CreateUniformBlock(BlockType.Water, "Water", false, true, 100f, new Vector2(13, 12)));

        // Lava (col=13, row=14)
        Register(CreateUniformBlock(BlockType.Lava, "Lava", false, false, 100f, new Vector2(13, 14)));

        // Crafting Table (Top: col=11, row=2; Front/Back: col=11, row=3; Sides: col=12, row=3; Bottom: col=4, row=0 Planks)
        Register(new BlockDefinition(
            BlockType.CraftingTable,
            "Crafting Table",
            true,
            false,
            2.5f,
            new[]
            {
                new Vector2(11, 3), // Front
                new Vector2(11, 3), // Back
                new Vector2(12, 3), // Left
                new Vector2(12, 3), // Right
                new Vector2(11, 2), // Top
                new Vector2(4, 0) // Bottom (Planks)
            }
        ));

        // Chest (Front: col=11, row=1; Sides/Back: col=10, row=1; Top/Bottom: col=9, row=1)
        Register(new BlockDefinition(
            BlockType.Chest,
            "Chest",
            true,
            false,
            2.5f,
            new[]
            {
                new Vector2(11, 1), // Front
                new Vector2(10, 1), // Back
                new Vector2(10, 1), // Left
                new Vector2(10, 1), // Right
                new Vector2(9, 1), // Top
                new Vector2(9, 1) // Bottom
            }
        ));

        // Furnace (Front: col=12, row=2; Sides/Back: col=13, row=2; Top/Bottom: col=14, row=3)
        Register(new BlockDefinition(
            BlockType.Furnace,
            "Furnace",
            true,
            false,
            3.5f,
            new[]
            {
                new Vector2(12, 2), // Front
                new Vector2(13, 2), // Back
                new Vector2(13, 2), // Left
                new Vector2(13, 2), // Right
                new Vector2(14, 3), // Top
                new Vector2(14, 3) // Bottom
            }
        ));
        Register(CreateUniformBlock(BlockType.Coal, "Coal", false, true, 0f, new Vector2(2, 2)));
        Register(CreateUniformBlock(BlockType.IronIngot, "Iron Ingot", false, true, 0f, new Vector2(1, 2)));
        Register(CreateUniformBlock(BlockType.FlintAndSteel, "Flint and Steel", false, true, 0f, new Vector2(5, 2)));
        Register(CreateUniformBlock(BlockType.Torch, "Torch", false, true, 0f, new Vector2(0, 5),
            meshType: MeshType.CrossQuad, isLightSource: true));
        Register(CreateUniformBlock(BlockType.DoorLower, "Wooden Door", true, true, 1.5f, new Vector2(1, 6),
            meshType: MeshType.DoorClosed));
        Register(CreateUniformBlock(BlockType.DoorUpper, "Wooden Door", true, true, 1.5f, new Vector2(1, 5),
            meshType: MeshType.DoorClosed));
        Register(CreateUniformBlock(BlockType.DoorLowerOpen, "Wooden Door", false, true, 1.5f, new Vector2(1, 6),
            meshType: MeshType.DoorOpen));
        Register(CreateUniformBlock(BlockType.DoorUpperOpen, "Wooden Door", false, true, 1.5f, new Vector2(1, 5),
            meshType: MeshType.DoorOpen));
        Register(CreateUniformBlock(BlockType.IronDoorLower, "Iron Door", true, true, 5.0f, new Vector2(2, 6),
            meshType: MeshType.DoorClosed));
        Register(CreateUniformBlock(BlockType.IronDoorUpper, "Iron Door", true, true, 5.0f, new Vector2(2, 5),
            meshType: MeshType.DoorClosed));
        Register(CreateUniformBlock(BlockType.IronDoorLowerOpen, "Iron Door", false, true, 5.0f, new Vector2(2, 6),
            meshType: MeshType.DoorOpen));
        Register(CreateUniformBlock(BlockType.IronDoorUpperOpen, "Iron Door", false, true, 5.0f, new Vector2(2, 5),
            meshType: MeshType.DoorOpen));
        Register(CreateUniformBlock(BlockType.Lever, "Lever", false, true, 0.5f, new Vector2(0, 6),
            meshType: MeshType.SwitchLever));
        Register(CreateUniformBlock(BlockType.LeverOn, "Lever (Active)", false, true, 0.5f, new Vector2(0, 6),
            new Vector3(1.4f, 0.4f, 0.3f), MeshType.SwitchLever));
        Register(CreateUniformBlock(BlockType.StoneButton, "Stone Button", false, true, 0.5f, new Vector2(1, 0),
            meshType: MeshType.WallButton));
        Register(CreateUniformBlock(BlockType.StoneButtonPressed, "Stone Button (Pressed)", false, true, 0.5f,
            new Vector2(1, 0), new Vector3(1.3f, 0.4f, 0.3f), MeshType.WallButton));
        Register(CreateUniformBlock(BlockType.Rail, "Rail", false, true, 0.7f, new Vector2(0, 8),
            meshType: MeshType.FlatSurface));
        Register(CreateUniformBlock(BlockType.PoweredRail, "Powered Rail", false, true, 0.7f, new Vector2(0, 8),
            meshType: MeshType.FlatSurface));
        Register(CreateUniformBlock(BlockType.PoweredRailOn, "Powered Rail (Active)", false, true, 0.7f,
            new Vector2(0, 8), new Vector3(1.4f, 0.4f, 0.3f), MeshType.FlatSurface));
        Register(CreateUniformBlock(BlockType.MinecartItem, "Minecart", false, true, 0f, new Vector2(6, 0)));
        Register(CreateUniformBlock(BlockType.Portal, "Nether Portal", false, true, 0f, new Vector2(14, 12),
            new Vector3(1.35f, 0.40f, 1.65f), isLightSource: true));
        Register(new BlockDefinition(
            BlockType.BedFoot,
            "Bed",
            true,
            true,
            0.2f,
            new[]
            {
                new Vector2(5, 9), // Front (Foot end)
                new Vector2(6, 9), // Back (Inner)
                new Vector2(6, 9), // Left (Side)
                new Vector2(6, 9), // Right (Side)
                new Vector2(6, 8), // Top (Foot top)
                new Vector2(4, 0) // Bottom (Wood)
            },
            meshType: MeshType.Bed
        ));
        Register(new BlockDefinition(
            BlockType.BedHead,
            "Bed",
            true,
            true,
            0.2f,
            new[]
            {
                new Vector2(8, 9), // Front (Head end)
                new Vector2(7, 9), // Back (Inner)
                new Vector2(7, 9), // Left (Side)
                new Vector2(7, 9), // Right (Side)
                new Vector2(7, 8), // Top (Head top)
                new Vector2(4, 0) // Bottom (Wood)
            },
            meshType: MeshType.Bed
        ));
        Register(CreateUniformBlock(BlockType.GoldOre, "Gold Ore", true, false, 3.0f, new Vector2(0, 2)));
        Register(CreateUniformBlock(BlockType.DiamondOre, "Diamond Ore", true, false, 3.5f, new Vector2(2, 3)));
        Register(CreateUniformBlock(BlockType.RedstoneOre, "Redstone Ore", true, false, 3.0f, new Vector2(3, 3)));
        Register(CreateUniformBlock(BlockType.LapisOre, "Lapis Lazuli Ore", true, false, 3.0f, new Vector2(0, 10)));
        Register(CreateUniformBlock(BlockType.GoldBlock, "Block of Gold", true, false, 3.0f, new Vector2(7, 1)));
        Register(CreateUniformBlock(BlockType.IronBlock, "Block of Iron", true, false, 5.0f, new Vector2(6, 1)));
        Register(CreateUniformBlock(BlockType.DiamondBlock, "Block of Diamond", true, false, 5.0f, new Vector2(8, 1)));
        Register(CreateUniformBlock(BlockType.LapisBlock, "Lapis Lazuli Block", true, false, 3.0f, new Vector2(0, 9)));
        Register(CreateUniformBlock(BlockType.Gravel, "Gravel", true, false, 0.6f, new Vector2(3, 1)));
        Register(CreateUniformBlock(BlockType.Clay, "Clay Block", true, false, 0.6f, new Vector2(8, 4)));
        Register(CreateMultiFaceBlock(BlockType.Sandstone, "Sandstone", true, false, 0.8f, new Vector2(0, 11),
            new Vector2(0, 12), new Vector2(0, 13)));
        Register(CreateMultiFaceBlock(BlockType.BirchLog, "Birch Log", true, false, 2.0f, new Vector2(5, 1),
            new Vector2(5, 7), new Vector2(5, 1)));
        Register(CreateMultiFaceBlock(BlockType.SpruceLog, "Spruce Log", true, false, 2.0f, new Vector2(5, 1),
            new Vector2(4, 7), new Vector2(5, 1)));
        Register(CreateUniformBlock(BlockType.SpruceLeaves, "Spruce Leaves", true, true, 0.2f, new Vector2(5, 3),
            new Vector3(0.35f, 0.65f, 0.35f)));
        Register(CreateUniformBlock(BlockType.SnowBlock, "Snow Block", true, false, 0.2f, new Vector2(2, 4)));
        Register(CreateMultiFaceBlock(BlockType.SnowyGrass, "Snowy Grass", true, false, 0.6f, new Vector2(2, 4),
            new Vector2(4, 4), new Vector2(2, 0)));
        Register(CreateUniformBlock(BlockType.Ice, "Ice", true, true, 0.5f, new Vector2(3, 4)));
        Register(CreateMultiFaceBlock(BlockType.Cactus, "Cactus", true, true, 0.4f, new Vector2(5, 4),
            new Vector2(6, 4), new Vector2(7, 4)));
        Register(CreateUniformBlock(BlockType.SugarCane, "Sugar Cane", false, true, 0f, new Vector2(9, 4),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.TallGrass, "Tall Grass", false, true, 0f, new Vector2(13, 5),
            new Vector3(0.85f, 1.0f, 0.85f), MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.Rose, "Rose", false, true, 0f, new Vector2(12, 0),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.Dandelion, "Dandelion", false, true, 0f, new Vector2(13, 0),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.RedMushroom, "Red Mushroom", false, true, 0f, new Vector2(12, 1),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.BrownMushroom, "Brown Mushroom", false, true, 0f, new Vector2(13, 1),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.OakSapling, "Oak Sapling", false, true, 0f, new Vector2(15, 0),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.Wheat, "Wheat", false, true, 0f, new Vector2(15, 5),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.WheatSeeds, "Wheat Seeds", false, true, 0f, new Vector2(8, 5)));
        Register(CreateMultiFaceBlock(BlockType.Farmland, "Farmland", true, false, 0.6f, new Vector2(6, 5),
            new Vector2(2, 0), new Vector2(2, 0)));
        Register(CreateUniformBlock(BlockType.Bricks, "Bricks", true, false, 2.0f, new Vector2(7, 0)));
        Register(CreateUniformBlock(BlockType.SmoothStone, "Smooth Stone", true, false, 1.5f, new Vector2(6, 0)));
        Register(CreateMultiFaceBlock(BlockType.DoubleStoneSlab, "Double Stone Slab", true, false, 2.0f,
            new Vector2(6, 0), new Vector2(5, 0), new Vector2(6, 0)));
        Register(CreateUniformBlock(BlockType.MossyCobblestone, "Mossy Cobblestone", true, false, 2.0f,
            new Vector2(4, 2)));
        Register(CreateMultiFaceBlock(BlockType.Bookshelf, "Bookshelf", true, false, 1.5f, new Vector2(4, 0),
            new Vector2(3, 2), new Vector2(4, 0)));
        Register(CreateUniformBlock(BlockType.Sponge, "Sponge", true, false, 0.6f, new Vector2(0, 3)));
        Register(CreateUniformBlock(BlockType.Ladder, "Ladder", false, true, 0.4f, new Vector2(3, 5),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.Trapdoor, "Trapdoor", false, true, 1.5f, new Vector2(4, 5)));
        Register(CreateUniformBlock(BlockType.Cobweb, "Cobweb", false, true, 4.0f, new Vector2(11, 0),
            meshType: MeshType.CrossQuad));
        Register(CreateUniformBlock(BlockType.Netherrack, "Netherrack", true, false, 0.4f, new Vector2(7, 6)));
        Register(CreateUniformBlock(BlockType.SoulSand, "Soul Sand", true, false, 0.5f, new Vector2(8, 6)));
        Register(CreateUniformBlock(BlockType.Glowstone, "Glowstone", true, false, 0.3f, new Vector2(9, 6),
            isLightSource: true));
        Register(CreateMultiFaceBlock(BlockType.TNT, "TNT", true, false, 0f, new Vector2(9, 0), new Vector2(8, 0),
            new Vector2(10, 0)));
        Register(CreateUniformBlock(BlockType.MonsterSpawner, "Monster Spawner", true, true, 5.0f, new Vector2(1, 4)));
        Register(CreateDirectionalBlock(BlockType.Dispenser, "Dispenser", true, false, 3.5f, new Vector2(14, 2),
            new Vector2(13, 2), new Vector2(14, 3)));
        Register(CreateDirectionalBlock(BlockType.LitFurnace, "Burning Furnace", true, false, 3.5f, new Vector2(13, 3),
            new Vector2(13, 2), new Vector2(14, 3), isLightSource: true));
        Register(CreateUniformBlock(BlockType.RedstoneTorch, "Redstone Torch", false, true, 0f, new Vector2(3, 6),
            meshType: MeshType.CrossQuad, isLightSource: true));
        Register(CreateMultiFaceBlock(BlockType.Jukebox, "Jukebox", true, false, 2.0f, new Vector2(11, 4),
            new Vector2(10, 4), new Vector2(10, 4)));
        Register(CreateMultiFaceBlock(BlockType.Pumpkin, "Pumpkin", true, false, 1.0f, new Vector2(6, 6),
            new Vector2(6, 7), new Vector2(6, 6)));
        Register(CreateDirectionalBlock(BlockType.CarvedPumpkin, "Carved Pumpkin", true, false, 1.0f, new Vector2(7, 7),
            new Vector2(6, 7), new Vector2(6, 6)));
        Register(CreateDirectionalBlock(BlockType.JackOLantern, "Jack o'Lantern", true, false, 1.0f, new Vector2(8, 7),
            new Vector2(6, 7), new Vector2(6, 6), isLightSource: true));
        Register(CreateMultiFaceBlock(BlockType.Cake, "Cake", true, true, 0.5f, new Vector2(9, 7), new Vector2(10, 1),
            new Vector2(12, 7)));
        Register(CreateUniformBlock(BlockType.Bread, "Bread", false, true, 0f, new Vector2(8, 5),
            new Vector3(1.35f, 0.95f, 0.50f)));
        Register(CreateUniformBlock(BlockType.WhiteWool, "White Wool", true, false, 0.8f, new Vector2(0, 4)));
        Register(CreateUniformBlock(BlockType.OrangeWool, "Orange Wool", true, false, 0.8f, new Vector2(2, 14)));
        Register(CreateUniformBlock(BlockType.MagentaWool, "Magenta Wool", true, false, 0.8f, new Vector2(2, 13)));
        Register(CreateUniformBlock(BlockType.LightBlueWool, "Light Blue Wool", true, false, 0.8f, new Vector2(2, 12)));
        Register(CreateUniformBlock(BlockType.YellowWool, "Yellow Wool", true, false, 0.8f, new Vector2(2, 11)));
        Register(CreateUniformBlock(BlockType.LimeWool, "Lime Wool", true, false, 0.8f, new Vector2(2, 10)));
        Register(CreateUniformBlock(BlockType.PinkWool, "Pink Wool", true, false, 0.8f, new Vector2(2, 9)));
        Register(CreateUniformBlock(BlockType.GrayWool, "Gray Wool", true, false, 0.8f, new Vector2(2, 8)));
        Register(CreateUniformBlock(BlockType.LightGrayWool, "Light Gray Wool", true, false, 0.8f, new Vector2(1, 15)));
        Register(CreateUniformBlock(BlockType.CyanWool, "Cyan Wool", true, false, 0.8f, new Vector2(1, 14)));
        Register(CreateUniformBlock(BlockType.PurpleWool, "Purple Wool", true, false, 0.8f, new Vector2(1, 13)));
        Register(CreateUniformBlock(BlockType.BlueWool, "Blue Wool", true, false, 0.8f, new Vector2(1, 12)));
        Register(CreateUniformBlock(BlockType.BrownWool, "Brown Wool", true, false, 0.8f, new Vector2(1, 11)));
        Register(CreateUniformBlock(BlockType.GreenWool, "Green Wool", true, false, 0.8f, new Vector2(1, 10)));
        Register(CreateUniformBlock(BlockType.RedWool, "Red Wool", true, false, 0.8f, new Vector2(1, 9)));
        Register(CreateUniformBlock(BlockType.BlackWool, "Black Wool", true, false, 0.8f, new Vector2(1, 8)));
        Register(CreateUniformBlock(BlockType.RedstoneBlock, "Block of Redstone", true, false, 5.0f, new Vector2(8, 1),
            new Vector3(0.95f, 0.15f, 0.15f)));
        Register(CreateUniformBlock(BlockType.CoalBlock, "Block of Coal", true, false, 5.0f, new Vector2(6, 1),
            new Vector3(0.12f, 0.12f, 0.12f)));
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
        return Definitions[type]?.IsTransparent ?? true;
    }

    public static bool IsSolid(byte type)
    {
        return Definitions[type]?.IsSolid ?? false;
    }

    public static bool IsDirectionalBlock(BlockType type)
    {
        return type == BlockType.Furnace ||
               type == BlockType.LitFurnace ||
               type == BlockType.Dispenser ||
               type == BlockType.CarvedPumpkin ||
               type == BlockType.JackOLantern ||
               type == BlockType.CraftingTable ||
               type == BlockType.Chest;
    }

    public static bool IsRailBlock(BlockType type)
    {
        return type == BlockType.Rail ||
               type == BlockType.PoweredRail ||
               type == BlockType.PoweredRailOn;
    }

    public static bool IsRailBlock(byte type)
    {
        return IsRailBlock((BlockType)type);
    }

    public static bool IsDoorBlock(BlockType type)
    {
        return type == BlockType.DoorLower ||
               type == BlockType.DoorUpper ||
               type == BlockType.DoorLowerOpen ||
               type == BlockType.DoorUpperOpen ||
               type == BlockType.IronDoorLower ||
               type == BlockType.IronDoorUpper ||
               type == BlockType.IronDoorLowerOpen ||
               type == BlockType.IronDoorUpperOpen;
    }

    public static bool IsDoorBlock(byte type)
    {
        return IsDoorBlock((BlockType)type);
    }
}