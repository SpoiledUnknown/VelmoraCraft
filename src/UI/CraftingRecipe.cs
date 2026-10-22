using VelmoraCraft.src.World;

namespace VelmoraCraft.src.UI;

public enum RecipeStation
{
    Personal,
    CraftingTable,
    Furnace
}

public class CraftingRecipe
{
    public CraftingRecipe(string name, ItemStack result, (BlockType, int)[] ingredients,
        RecipeStation station = RecipeStation.CraftingTable, bool isSmelting = false)
    {
        Name = name;
        Result = result;
        Ingredients = ingredients;
        Station = isSmelting ? RecipeStation.Furnace : station;
    }

    public string Name { get; }
    public ItemStack Result { get; }
    public (BlockType Type, int Count)[] Ingredients { get; }
    public RecipeStation Station { get; }
    public bool IsSmelting => Station == RecipeStation.Furnace;

    public bool CanCraft(Inventory inventory)
    {
        foreach (var ing in Ingredients)
            if (!inventory.HasItem(ing.Type, ing.Count))
                return false;

        return true;
    }

    public bool Craft(Inventory inventory)
    {
        if (!CanCraft(inventory)) return false;

        foreach (var ing in Ingredients) inventory.ConsumeItem(ing.Type, ing.Count);

        return inventory.AddItem(Result.Type, Result.Count);
    }
}

public static class RecipeRegistry
{
    public static readonly List<CraftingRecipe> Recipes = new();

    static RecipeRegistry()
    {
        Recipes.Add(new CraftingRecipe("Oak Planks (x4)", new ItemStack(BlockType.Planks, 4),
            new[] { (BlockType.Log, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Birch Planks (x4)", new ItemStack(BlockType.Planks, 4),
            new[] { (BlockType.BirchLog, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Spruce Planks (x4)", new ItemStack(BlockType.Planks, 4),
            new[] { (BlockType.SpruceLog, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Crafting Table", new ItemStack(BlockType.CraftingTable),
            new[] { (BlockType.Planks, 4) }, RecipeStation.Personal));
        Recipes.Add(
            new CraftingRecipe("Storage Chest", new ItemStack(BlockType.Chest), new[] { (BlockType.Planks, 8) }));
        Recipes.Add(new CraftingRecipe("Furnace", new ItemStack(BlockType.Furnace),
            new[] { (BlockType.Cobblestone, 8) }));
        Recipes.Add(new CraftingRecipe("Ladder (x3)", new ItemStack(BlockType.Ladder, 3),
            new[] { (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Trapdoor (x2)", new ItemStack(BlockType.Trapdoor, 2),
            new[] { (BlockType.Planks, 6) }));
        Recipes.Add(new CraftingRecipe("Wooden Door", new ItemStack(BlockType.DoorLower),
            new[] { (BlockType.Planks, 6) }));
        Recipes.Add(new CraftingRecipe("Iron Door", new ItemStack(BlockType.IronDoorLower),
            new[] { (BlockType.IronIngot, 6) }));
        Recipes.Add(new CraftingRecipe("Bed (White)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.WhiteWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Red)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.RedWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Blue)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.BlueWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Yellow)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.YellowWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Green)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.GreenWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Black)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.BlackWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Brown)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.BrownWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Orange)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.OrangeWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Purple)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.PurpleWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bed (Gray)", new ItemStack(BlockType.BedFoot),
            new[] { (BlockType.GrayWool, 3), (BlockType.Planks, 3) }));
        Recipes.Add(new CraftingRecipe("Bookshelf", new ItemStack(BlockType.Bookshelf),
            new[] { (BlockType.Planks, 6), (BlockType.SugarCane, 3) }));

        Recipes.Add(new CraftingRecipe("Block of Redstone", new ItemStack(BlockType.RedstoneBlock),
            new[] { (BlockType.RedstoneOre, 9) }));
        Recipes.Add(new CraftingRecipe("Block of Coal", new ItemStack(BlockType.CoalBlock),
            new[] { (BlockType.Coal, 9) }));
        Recipes.Add(new CraftingRecipe("Block of Iron", new ItemStack(BlockType.IronBlock),
            new[] { (BlockType.IronIngot, 9) }));
        Recipes.Add(new CraftingRecipe("Block of Gold", new ItemStack(BlockType.GoldBlock),
            new[] { (BlockType.GoldOre, 9) }));
        Recipes.Add(new CraftingRecipe("Block of Diamond", new ItemStack(BlockType.DiamondBlock),
            new[] { (BlockType.DiamondOre, 9) }));
        Recipes.Add(new CraftingRecipe("Block of Lapis", new ItemStack(BlockType.LapisBlock),
            new[] { (BlockType.LapisOre, 9) }));

        Recipes.Add(new CraftingRecipe("Redstone from Block", new ItemStack(BlockType.RedstoneOre, 9),
            new[] { (BlockType.RedstoneBlock, 1) }));
        Recipes.Add(new CraftingRecipe("Coal from Block", new ItemStack(BlockType.Coal, 9),
            new[] { (BlockType.CoalBlock, 1) }));
        Recipes.Add(new CraftingRecipe("Iron Ingots from Block", new ItemStack(BlockType.IronIngot, 9),
            new[] { (BlockType.IronBlock, 1) }));
        Recipes.Add(new CraftingRecipe("Gold Ore from Block", new ItemStack(BlockType.GoldOre, 9),
            new[] { (BlockType.GoldBlock, 1) }));
        Recipes.Add(new CraftingRecipe("Diamond Ore from Block", new ItemStack(BlockType.DiamondOre, 9),
            new[] { (BlockType.DiamondBlock, 1) }));
        Recipes.Add(new CraftingRecipe("Lapis Ore from Block", new ItemStack(BlockType.LapisOre, 9),
            new[] { (BlockType.LapisBlock, 1) }));

        Recipes.Add(new CraftingRecipe("Sandstone", new ItemStack(BlockType.Sandstone), new[] { (BlockType.Sand, 4) },
            RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Stone Slab (x3)", new ItemStack(BlockType.DoubleStoneSlab, 3),
            new[] { (BlockType.Stone, 3) }));
        Recipes.Add(new CraftingRecipe("Mossy Cobblestone", new ItemStack(BlockType.MossyCobblestone),
            new[] { (BlockType.Cobblestone, 1), (BlockType.Leaves, 1) }));
        Recipes.Add(new CraftingRecipe("Snow Block", new ItemStack(BlockType.SnowBlock), new[] { (BlockType.Ice, 4) },
            RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Farmland", new ItemStack(BlockType.Farmland),
            new[] { (BlockType.Dirt, 1), (BlockType.Wheat, 1) }));

        Recipes.Add(new CraftingRecipe("Torches (x4)", new ItemStack(BlockType.Torch, 4),
            new[] { (BlockType.Coal, 1), (BlockType.Planks, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Redstone Torch (x4)", new ItemStack(BlockType.RedstoneTorch, 4),
            new[] { (BlockType.RedstoneOre, 1), (BlockType.Planks, 1) }));
        Recipes.Add(new CraftingRecipe("Lever", new ItemStack(BlockType.Lever),
            new[] { (BlockType.Cobblestone, 1), (BlockType.Planks, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Stone Button", new ItemStack(BlockType.StoneButton),
            new[] { (BlockType.Stone, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Rail (x16)", new ItemStack(BlockType.Rail, 16),
            new[] { (BlockType.IronIngot, 6), (BlockType.Planks, 1) }));
        Recipes.Add(new CraftingRecipe("Powered Rail (x6)", new ItemStack(BlockType.PoweredRail, 6),
            new[] { (BlockType.IronIngot, 6), (BlockType.RedstoneTorch, 1) }));
        Recipes.Add(new CraftingRecipe("Minecart", new ItemStack(BlockType.MinecartItem),
            new[] { (BlockType.IronIngot, 5) }));
        Recipes.Add(new CraftingRecipe("TNT", new ItemStack(BlockType.TNT),
            new[] { (BlockType.Sand, 4), (BlockType.RedstoneOre, 1) }));
        Recipes.Add(new CraftingRecipe("Dispenser", new ItemStack(BlockType.Dispenser),
            new[] { (BlockType.Cobblestone, 7), (BlockType.RedstoneOre, 1) }));
        Recipes.Add(new CraftingRecipe("Jukebox", new ItemStack(BlockType.Jukebox),
            new[] { (BlockType.Planks, 8), (BlockType.DiamondOre, 1) }));
        Recipes.Add(new CraftingRecipe("Carved Pumpkin", new ItemStack(BlockType.CarvedPumpkin),
            new[] { (BlockType.Pumpkin, 1) }));
        Recipes.Add(new CraftingRecipe("Burning Furnace", new ItemStack(BlockType.LitFurnace),
            new[] { (BlockType.Furnace, 1), (BlockType.Coal, 1) }));
        Recipes.Add(new CraftingRecipe("Jack-o'-Lantern", new ItemStack(BlockType.JackOLantern),
            new[] { (BlockType.Pumpkin, 1), (BlockType.Torch, 1) }));
        Recipes.Add(new CraftingRecipe("Jack-o'-Lantern (Quick)", new ItemStack(BlockType.JackOLantern),
            new[] { (BlockType.Pumpkin, 1), (BlockType.Torch, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Bread", new ItemStack(BlockType.Bread), new[] { (BlockType.Wheat, 3) },
            RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Cake", new ItemStack(BlockType.Cake), new[] { (BlockType.Bread, 3) }));
        Recipes.Add(new CraftingRecipe("Flint & Steel", new ItemStack(BlockType.FlintAndSteel),
            new[] { (BlockType.IronIngot, 1), (BlockType.Cobblestone, 1) }));

        Recipes.Add(new CraftingRecipe("White Wool from Wheat", new ItemStack(BlockType.WhiteWool),
            new[] { (BlockType.Wheat, 4) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Cobweb from Tall Grass", new ItemStack(BlockType.Cobweb),
            new[] { (BlockType.TallGrass, 2) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("White Wool", new ItemStack(BlockType.WhiteWool),
            new[] { (BlockType.Cobweb, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("White Wool (x4)", new ItemStack(BlockType.WhiteWool, 4),
            new[] { (BlockType.Cobweb, 4) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Cobweb from Wool (x2)", new ItemStack(BlockType.Cobweb, 2),
            new[] { (BlockType.WhiteWool, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Wheat Seeds (x2)", new ItemStack(BlockType.WheatSeeds, 2),
            new[] { (BlockType.Wheat, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Red Wool", new ItemStack(BlockType.RedWool),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Rose, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Yellow Wool", new ItemStack(BlockType.YellowWool),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Dandelion, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Green Wool", new ItemStack(BlockType.GreenWool),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Cactus, 1) }));
        Recipes.Add(new CraftingRecipe("Blue Wool", new ItemStack(BlockType.BlueWool),
            new[] { (BlockType.WhiteWool, 1), (BlockType.LapisOre, 1) }));
        Recipes.Add(new CraftingRecipe("Black Wool", new ItemStack(BlockType.BlackWool),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Coal, 1) }, RecipeStation.Personal));
        Recipes.Add(new CraftingRecipe("Brown Wool", new ItemStack(BlockType.BrownWool),
            new[] { (BlockType.WhiteWool, 1), (BlockType.BrownMushroom, 1) }));
        Recipes.Add(new CraftingRecipe("Orange Wool", new ItemStack(BlockType.OrangeWool),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Pumpkin, 1) }));
        Recipes.Add(new CraftingRecipe("Lime Wool (x2)", new ItemStack(BlockType.LimeWool, 2),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Cactus, 1), (BlockType.Dandelion, 1) }));
        Recipes.Add(new CraftingRecipe("Pink Wool (x2)", new ItemStack(BlockType.PinkWool, 2),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Rose, 1), (BlockType.RedMushroom, 1) }));
        Recipes.Add(new CraftingRecipe("Light Blue Wool (x2)", new ItemStack(BlockType.LightBlueWool, 2),
            new[] { (BlockType.WhiteWool, 1), (BlockType.BlueWool, 1) }));
        Recipes.Add(new CraftingRecipe("Cyan Wool (x2)", new ItemStack(BlockType.CyanWool, 2),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Cactus, 1), (BlockType.LapisOre, 1) }));
        Recipes.Add(new CraftingRecipe("Purple Wool (x2)", new ItemStack(BlockType.PurpleWool, 2),
            new[] { (BlockType.WhiteWool, 1), (BlockType.Rose, 1), (BlockType.LapisOre, 1) }));
        Recipes.Add(new CraftingRecipe("Magenta Wool (x2)", new ItemStack(BlockType.MagentaWool, 2),
            new[] { (BlockType.PurpleWool, 1), (BlockType.PinkWool, 1) }));
        Recipes.Add(new CraftingRecipe("Gray Wool (x2)", new ItemStack(BlockType.GrayWool, 2),
            new[] { (BlockType.WhiteWool, 1), (BlockType.BlackWool, 1) }));
        Recipes.Add(new CraftingRecipe("Light Gray Wool (x2)", new ItemStack(BlockType.LightGrayWool, 2),
            new[] { (BlockType.WhiteWool, 1), (BlockType.GrayWool, 1) }));

        Recipes.Add(new CraftingRecipe("Smelt Iron Ingot", new ItemStack(BlockType.IronIngot),
            new[] { (BlockType.IronOre, 1), (BlockType.Coal, 1) }, isSmelting: true));
        Recipes.Add(new CraftingRecipe("Smelt Glass", new ItemStack(BlockType.Glass),
            new[] { (BlockType.Sand, 1), (BlockType.Coal, 1) }, isSmelting: true));
        Recipes.Add(new CraftingRecipe("Smelt Stone", new ItemStack(BlockType.Stone),
            new[] { (BlockType.Cobblestone, 1), (BlockType.Coal, 1) }, isSmelting: true));
        Recipes.Add(new CraftingRecipe("Smelt Smooth Stone", new ItemStack(BlockType.SmoothStone),
            new[] { (BlockType.Stone, 1), (BlockType.Coal, 1) }, isSmelting: true));
        Recipes.Add(new CraftingRecipe("Smelt Bricks", new ItemStack(BlockType.Bricks),
            new[] { (BlockType.Clay, 1), (BlockType.Coal, 1) }, isSmelting: true));
        Recipes.Add(new CraftingRecipe("Smelt Charcoal (x4)", new ItemStack(BlockType.Coal, 4),
            new[] { (BlockType.Log, 1), (BlockType.Coal, 1) }, isSmelting: true));
        Recipes.Add(new CraftingRecipe("Smelt Glowstone", new ItemStack(BlockType.Glowstone),
            new[] { (BlockType.Netherrack, 1), (BlockType.Coal, 1) }, isSmelting: true));
    }
}