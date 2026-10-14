using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine.src.UI
{
    public class CraftingRecipe
    {
        public string Name { get; }
        public ItemStack Result { get; }
        public (BlockType Type, int Count)[] Ingredients { get; }
        public bool IsSmelting { get; }

        public CraftingRecipe(string name, ItemStack result, (BlockType, int)[] ingredients, bool isSmelting = false)
        {
            Name = name;
            Result = result;
            Ingredients = ingredients;
            IsSmelting = isSmelting;
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
            // 1. Planks from Wood Log (1 Log -> 4 Planks)
            Recipes.Add(new CraftingRecipe("Wood Planks", new ItemStack(BlockType.Planks, 4), new[] { (BlockType.Log, 1) }));

            // 2. Crafting Table (4 Planks -> 1 Crafting Table)
            Recipes.Add(new CraftingRecipe("Crafting Table", new ItemStack(BlockType.CraftingTable, 1), new[] { (BlockType.Planks, 4) }));

            // 3. Chest (8 Planks -> 1 Chest)
            Recipes.Add(new CraftingRecipe("Storage Chest", new ItemStack(BlockType.Chest, 1), new[] { (BlockType.Planks, 8) }));

            // 4. Furnace (8 Cobblestone -> 1 Furnace)
            Recipes.Add(new CraftingRecipe("Furnace", new ItemStack(BlockType.Furnace, 1), new[] { (BlockType.Cobblestone, 8) }));

            // 5. Instant Smelting: Iron Ore -> Iron Ingot (1 Iron Ore + 1 Coal -> 1 Iron Ingot)
            Recipes.Add(new CraftingRecipe("Smelt Iron Ingot", new ItemStack(BlockType.IronIngot, 1), new[] { (BlockType.IronOre, 1), (BlockType.Coal, 1) }, isSmelting: true));

            // 6. Instant Smelting: Sand -> Glass (1 Sand + 1 Coal -> 1 Glass)
            Recipes.Add(new CraftingRecipe("Smelt Glass", new ItemStack(BlockType.Glass, 1), new[] { (BlockType.Sand, 1), (BlockType.Coal, 1) }, isSmelting: true));

            // 7. Instant Smelting: Cobblestone -> Stone (1 Cobble + 1 Coal -> 1 Stone)
            Recipes.Add(new CraftingRecipe("Smelt Stone", new ItemStack(BlockType.Stone, 1), new[] { (BlockType.Cobblestone, 1), (BlockType.Coal, 1) }, isSmelting: true));

            // 8. Flint and Steel (1 Iron Ingot + 1 Obsidian -> Flint and Steel for Portal!)
            Recipes.Add(new CraftingRecipe("Flint & Steel", new ItemStack(BlockType.FlintAndSteel, 1), new[] { (BlockType.IronIngot, 1), (BlockType.Cobblestone, 1) }));

            // 9. Torches (1 Coal + 1 Planks -> 4 Torches)
            Recipes.Add(new CraftingRecipe("Torch (x4)", new ItemStack(BlockType.Torch, 4), new[] { (BlockType.Coal, 1), (BlockType.Planks, 1) }));

            // 10. Wooden Door (6 Planks -> 1 Wooden Door)
            Recipes.Add(new CraftingRecipe("Wooden Door", new ItemStack(BlockType.DoorLower, 1), new[] { (BlockType.Planks, 6) }));

            // 11. Lever (1 Cobblestone + 1 Planks -> 1 Lever)
            Recipes.Add(new CraftingRecipe("Lever", new ItemStack(BlockType.Lever, 1), new[] { (BlockType.Cobblestone, 1), (BlockType.Planks, 1) }));

            // 12. Stone Button (1 Stone -> 1 Stone Button)
            Recipes.Add(new CraftingRecipe("Stone Button", new ItemStack(BlockType.StoneButton, 1), new[] { (BlockType.Stone, 1) }));

            // 13. Rail (6 Iron Ingot + 1 Planks -> 16 Rails)
            Recipes.Add(new CraftingRecipe("Rail (x16)", new ItemStack(BlockType.Rail, 16), new[] { (BlockType.IronIngot, 6), (BlockType.Planks, 1) }));

            // 14. Powered Rail (6 Iron Ingot + 1 Torch -> 6 Powered Rails)
            Recipes.Add(new CraftingRecipe("Powered Rail (x6)", new ItemStack(BlockType.PoweredRail, 6), new[] { (BlockType.IronIngot, 6), (BlockType.Torch, 1) }));

            // 15. Minecart (5 Iron Ingot -> 1 Minecart)
            Recipes.Add(new CraftingRecipe("Minecart", new ItemStack(BlockType.MinecartItem, 1), new[] { (BlockType.IronIngot, 5) }));
        }
    }
}
