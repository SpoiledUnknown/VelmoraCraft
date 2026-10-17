using VelmoraCraft.src.World;

namespace VelmoraCraft.src.UI
{
    public enum RecipeStation
    {
        Personal,      // Basic 2x2 crafting available directly in player inventory
        CraftingTable, // Full 3x3 workbench recipes requiring a placed Crafting Table
        Furnace        // Smelting recipes requiring an active Furnace
    }

    public class CraftingRecipe
    {
        public string Name { get; }
        public ItemStack Result { get; }
        public (BlockType Type, int Count)[] Ingredients { get; }
        public RecipeStation Station { get; }
        public bool IsSmelting => Station == RecipeStation.Furnace;

        public CraftingRecipe(string name, ItemStack result, (BlockType, int)[] ingredients, RecipeStation station = RecipeStation.CraftingTable, bool isSmelting = false)
        {
            Name = name;
            Result = result;
            Ingredients = ingredients;
            Station = isSmelting ? RecipeStation.Furnace : station;
        }

        public bool CanCraft(Inventory inventory)
        {
            foreach (var ing in Ingredients)
            {
                if (!inventory.HasItem(ing.Type, ing.Count))
                {
                    return false;
                }
            }
            return true;
        }

        public bool Craft(Inventory inventory)
        {
            if (!CanCraft(inventory)) return false;

            // Consume ingredients
            foreach (var ing in Ingredients)
            {
                inventory.ConsumeItem(ing.Type, ing.Count);
            }

            // Add result
            return inventory.AddItem(Result.Type, Result.Count);
        }
    }

    public static class RecipeRegistry
    {
        public static readonly List<CraftingRecipe> Recipes = new List<CraftingRecipe>();

        static RecipeRegistry()
        {
            // ==========================================
            // 1. Wood & Basic Survival Construction
            // ==========================================
            Recipes.Add(new CraftingRecipe("Oak Planks (x4)", new ItemStack(BlockType.Planks, 4), new[] { (BlockType.Log, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Birch Planks (x4)", new ItemStack(BlockType.Planks, 4), new[] { (BlockType.BirchLog, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Spruce Planks (x4)", new ItemStack(BlockType.Planks, 4), new[] { (BlockType.SpruceLog, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Crafting Table", new ItemStack(BlockType.CraftingTable, 1), new[] { (BlockType.Planks, 4) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Storage Chest", new ItemStack(BlockType.Chest, 1), new[] { (BlockType.Planks, 8) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Furnace", new ItemStack(BlockType.Furnace, 1), new[] { (BlockType.Cobblestone, 8) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Ladder (x3)", new ItemStack(BlockType.Ladder, 3), new[] { (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Trapdoor (x2)", new ItemStack(BlockType.Trapdoor, 2), new[] { (BlockType.Planks, 6) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Wooden Door", new ItemStack(BlockType.DoorLower, 1), new[] { (BlockType.Planks, 6) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Iron Door", new ItemStack(BlockType.IronDoorLower, 1), new[] { (BlockType.IronIngot, 6) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (White)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.WhiteWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Red)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.RedWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Blue)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.BlueWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Yellow)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.YellowWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Green)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.GreenWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Black)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.BlackWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Brown)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.BrownWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Orange)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.OrangeWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Purple)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.PurpleWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bed (Gray)", new ItemStack(BlockType.BedFoot, 1), new[] { (BlockType.GrayWool, 3), (BlockType.Planks, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Bookshelf", new ItemStack(BlockType.Bookshelf, 1), new[] { (BlockType.Planks, 6), (BlockType.SugarCane, 3) }, RecipeStation.CraftingTable));

            // ==========================================
            // 2. Mineral Blocks (Compression: Ore/Item -> Solid Block)
            // ==========================================
            Recipes.Add(new CraftingRecipe("Block of Redstone", new ItemStack(BlockType.RedstoneBlock, 1), new[] { (BlockType.RedstoneOre, 9) }));
            Recipes.Add(new CraftingRecipe("Block of Coal", new ItemStack(BlockType.CoalBlock, 1), new[] { (BlockType.Coal, 9) }));
            Recipes.Add(new CraftingRecipe("Block of Iron", new ItemStack(BlockType.IronBlock, 1), new[] { (BlockType.IronIngot, 9) }));
            Recipes.Add(new CraftingRecipe("Block of Gold", new ItemStack(BlockType.GoldBlock, 1), new[] { (BlockType.GoldOre, 9) }));
            Recipes.Add(new CraftingRecipe("Block of Diamond", new ItemStack(BlockType.DiamondBlock, 1), new[] { (BlockType.DiamondOre, 9) }));
            Recipes.Add(new CraftingRecipe("Block of Lapis", new ItemStack(BlockType.LapisBlock, 1), new[] { (BlockType.LapisOre, 9) }));

            // ==========================================
            // 3. Mineral Decompression (Solid Block -> Ore/Item)
            // ==========================================
            Recipes.Add(new CraftingRecipe("Redstone from Block", new ItemStack(BlockType.RedstoneOre, 9), new[] { (BlockType.RedstoneBlock, 1) }));
            Recipes.Add(new CraftingRecipe("Coal from Block", new ItemStack(BlockType.Coal, 9), new[] { (BlockType.CoalBlock, 1) }));
            Recipes.Add(new CraftingRecipe("Iron Ingots from Block", new ItemStack(BlockType.IronIngot, 9), new[] { (BlockType.IronBlock, 1) }));
            Recipes.Add(new CraftingRecipe("Gold Ore from Block", new ItemStack(BlockType.GoldOre, 9), new[] { (BlockType.GoldBlock, 1) }));
            Recipes.Add(new CraftingRecipe("Diamond Ore from Block", new ItemStack(BlockType.DiamondOre, 9), new[] { (BlockType.DiamondBlock, 1) }));
            Recipes.Add(new CraftingRecipe("Lapis Ore from Block", new ItemStack(BlockType.LapisOre, 9), new[] { (BlockType.LapisBlock, 1) }));

            // ==========================================
            // 4. Terrain, Masonry & Construction
            // ==========================================
            Recipes.Add(new CraftingRecipe("Sandstone", new ItemStack(BlockType.Sandstone, 1), new[] { (BlockType.Sand, 4) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Stone Slab (x3)", new ItemStack(BlockType.DoubleStoneSlab, 3), new[] { (BlockType.Stone, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Mossy Cobblestone", new ItemStack(BlockType.MossyCobblestone, 1), new[] { (BlockType.Cobblestone, 1), (BlockType.Leaves, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Snow Block", new ItemStack(BlockType.SnowBlock, 1), new[] { (BlockType.Ice, 4) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Farmland", new ItemStack(BlockType.Farmland, 1), new[] { (BlockType.Dirt, 1), (BlockType.Wheat, 1) }, RecipeStation.CraftingTable));

            // ==========================================
            // 5. Redstone, Transport & Explosives
            // ==========================================
            Recipes.Add(new CraftingRecipe("Torches (x4)", new ItemStack(BlockType.Torch, 4), new[] { (BlockType.Coal, 1), (BlockType.Planks, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Redstone Torch (x4)", new ItemStack(BlockType.RedstoneTorch, 4), new[] { (BlockType.RedstoneOre, 1), (BlockType.Planks, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Lever", new ItemStack(BlockType.Lever, 1), new[] { (BlockType.Cobblestone, 1), (BlockType.Planks, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Stone Button", new ItemStack(BlockType.StoneButton, 1), new[] { (BlockType.Stone, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Rail (x16)", new ItemStack(BlockType.Rail, 16), new[] { (BlockType.IronIngot, 6), (BlockType.Planks, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Powered Rail (x6)", new ItemStack(BlockType.PoweredRail, 6), new[] { (BlockType.IronIngot, 6), (BlockType.RedstoneTorch, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Minecart", new ItemStack(BlockType.MinecartItem, 1), new[] { (BlockType.IronIngot, 5) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("TNT", new ItemStack(BlockType.TNT, 1), new[] { (BlockType.Sand, 4), (BlockType.RedstoneOre, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Dispenser", new ItemStack(BlockType.Dispenser, 1), new[] { (BlockType.Cobblestone, 7), (BlockType.RedstoneOre, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Jukebox", new ItemStack(BlockType.Jukebox, 1), new[] { (BlockType.Planks, 8), (BlockType.DiamondOre, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Carved Pumpkin", new ItemStack(BlockType.CarvedPumpkin, 1), new[] { (BlockType.Pumpkin, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Burning Furnace", new ItemStack(BlockType.LitFurnace, 1), new[] { (BlockType.Furnace, 1), (BlockType.Coal, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Jack-o'-Lantern", new ItemStack(BlockType.JackOLantern, 1), new[] { (BlockType.Pumpkin, 1), (BlockType.Torch, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Jack-o'-Lantern (Quick)", new ItemStack(BlockType.JackOLantern, 1), new[] { (BlockType.Pumpkin, 1), (BlockType.Torch, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Bread", new ItemStack(BlockType.Bread, 1), new[] { (BlockType.Wheat, 3) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Cake", new ItemStack(BlockType.Cake, 1), new[] { (BlockType.Bread, 3) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Flint & Steel", new ItemStack(BlockType.FlintAndSteel, 1), new[] { (BlockType.IronIngot, 1), (BlockType.Cobblestone, 1) }, RecipeStation.CraftingTable));

            // ==========================================
            // 6. Wool Production & 16 Dyeing Recipes
            // ==========================================
            Recipes.Add(new CraftingRecipe("White Wool", new ItemStack(BlockType.WhiteWool, 1), new[] { (BlockType.Cobweb, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("White Wool (x4)", new ItemStack(BlockType.WhiteWool, 4), new[] { (BlockType.Cobweb, 4) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Cobweb from Wool (x2)", new ItemStack(BlockType.Cobweb, 2), new[] { (BlockType.WhiteWool, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Wheat Seeds (x2)", new ItemStack(BlockType.WheatSeeds, 2), new[] { (BlockType.Wheat, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Red Wool", new ItemStack(BlockType.RedWool, 1), new[] { (BlockType.WhiteWool, 1), (BlockType.Rose, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Yellow Wool", new ItemStack(BlockType.YellowWool, 1), new[] { (BlockType.WhiteWool, 1), (BlockType.Dandelion, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Green Wool", new ItemStack(BlockType.GreenWool, 1), new[] { (BlockType.WhiteWool, 1), (BlockType.Cactus, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Blue Wool", new ItemStack(BlockType.BlueWool, 1), new[] { (BlockType.WhiteWool, 1), (BlockType.LapisOre, 1) }, RecipeStation.CraftingTable));
            Recipes.Add(new CraftingRecipe("Black Wool", new ItemStack(BlockType.BlackWool, 1), new[] { (BlockType.WhiteWool, 1), (BlockType.Coal, 1) }, RecipeStation.Personal));
            Recipes.Add(new CraftingRecipe("Brown Wool", new ItemStack(BlockType.BrownWool, 1), new[] { (BlockType.WhiteWool, 1), (BlockType.BrownMushroom, 1) }));
            Recipes.Add(new CraftingRecipe("Orange Wool", new ItemStack(BlockType.OrangeWool, 1), new[] { (BlockType.WhiteWool, 1), (BlockType.Pumpkin, 1) }));
            Recipes.Add(new CraftingRecipe("Lime Wool (x2)", new ItemStack(BlockType.LimeWool, 2), new[] { (BlockType.WhiteWool, 1), (BlockType.Cactus, 1), (BlockType.Dandelion, 1) }));
            Recipes.Add(new CraftingRecipe("Pink Wool (x2)", new ItemStack(BlockType.PinkWool, 2), new[] { (BlockType.WhiteWool, 1), (BlockType.Rose, 1), (BlockType.RedMushroom, 1) }));
            Recipes.Add(new CraftingRecipe("Light Blue Wool (x2)", new ItemStack(BlockType.LightBlueWool, 2), new[] { (BlockType.WhiteWool, 1), (BlockType.BlueWool, 1) }));
            Recipes.Add(new CraftingRecipe("Cyan Wool (x2)", new ItemStack(BlockType.CyanWool, 2), new[] { (BlockType.WhiteWool, 1), (BlockType.Cactus, 1), (BlockType.LapisOre, 1) }));
            Recipes.Add(new CraftingRecipe("Purple Wool (x2)", new ItemStack(BlockType.PurpleWool, 2), new[] { (BlockType.WhiteWool, 1), (BlockType.Rose, 1), (BlockType.LapisOre, 1) }));
            Recipes.Add(new CraftingRecipe("Magenta Wool (x2)", new ItemStack(BlockType.MagentaWool, 2), new[] { (BlockType.PurpleWool, 1), (BlockType.PinkWool, 1) }));
            Recipes.Add(new CraftingRecipe("Gray Wool (x2)", new ItemStack(BlockType.GrayWool, 2), new[] { (BlockType.WhiteWool, 1), (BlockType.BlackWool, 1) }));
            Recipes.Add(new CraftingRecipe("Light Gray Wool (x2)", new ItemStack(BlockType.LightGrayWool, 2), new[] { (BlockType.WhiteWool, 1), (BlockType.GrayWool, 1) }));

            // ==========================================
            // 7. Smelting & Furnace Recipes
            // ==========================================
            Recipes.Add(new CraftingRecipe("Smelt Iron Ingot", new ItemStack(BlockType.IronIngot, 1), new[] { (BlockType.IronOre, 1), (BlockType.Coal, 1) }, isSmelting: true));
            Recipes.Add(new CraftingRecipe("Smelt Glass", new ItemStack(BlockType.Glass, 1), new[] { (BlockType.Sand, 1), (BlockType.Coal, 1) }, isSmelting: true));
            Recipes.Add(new CraftingRecipe("Smelt Stone", new ItemStack(BlockType.Stone, 1), new[] { (BlockType.Cobblestone, 1), (BlockType.Coal, 1) }, isSmelting: true));
            Recipes.Add(new CraftingRecipe("Smelt Smooth Stone", new ItemStack(BlockType.SmoothStone, 1), new[] { (BlockType.Stone, 1), (BlockType.Coal, 1) }, isSmelting: true));
            Recipes.Add(new CraftingRecipe("Smelt Bricks", new ItemStack(BlockType.Bricks, 1), new[] { (BlockType.Clay, 1), (BlockType.Coal, 1) }, isSmelting: true));
            Recipes.Add(new CraftingRecipe("Smelt Charcoal (x4)", new ItemStack(BlockType.Coal, 4), new[] { (BlockType.Log, 1), (BlockType.Coal, 1) }, isSmelting: true));
            Recipes.Add(new CraftingRecipe("Smelt Glowstone", new ItemStack(BlockType.Glowstone, 1), new[] { (BlockType.Netherrack, 1), (BlockType.Coal, 1) }, isSmelting: true));
        }
    }
}
