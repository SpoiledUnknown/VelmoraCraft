using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VoxelRenderEngine.src.Entities;
using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine.src.UI
{
    public enum UIState
    {
        HUD,
        InventoryAndCrafting,
        Chest,
        Paused
    }

    public enum RecipeCategory
    {
        All,
        Crafting,
        Smelting
    }

    public class UIManager
    {
        private readonly UIRenderer renderer;
        private readonly ChestManager chestManager;

        public UIState CurrentState { get; private set; } = UIState.HUD;
        public bool IsScreenOpen => CurrentState != UIState.HUD;
        public bool IsPaused => CurrentState == UIState.Paused;
        public Action? OnQuitRequested { get; set; }
        public Action? OnReturnToMainMenuRequested { get; set; }
        public Action? OnSaveGameRequested { get; set; }
        public DayNightCycle? DayNightCycle { get; set; }
        private float saveNotificationTimer = 0f;

        public void ShowSaveNotification() => saveNotificationTimer = 2.5f;

        // Player & chest references
        private Player? player;
        private Inventory? currentChestInventory;
        private Vector3i? currentChestPos;

        // Held item for drag & drop
        public ItemStack HeldItem = ItemStack.Empty;

        // Crafting UI state
        private RecipeCategory activeCategory = RecipeCategory.All;
        private int recipeScrollOffset = 0;

        // Hover tracking
        private string? hoveredTooltip = null;

        // Key states for debouncing
        private bool eKeyWasDown = false;
        private bool escKeyWasDown = false;
        private bool leftMouseWasDown = false;
        private bool rightMouseWasDown = false;

        public UIManager(UIRenderer renderer, ChestManager chestManager)
        {
            this.renderer = renderer;
            this.chestManager = chestManager;
        }

        public void SetPlayer(Player player)
        {
            this.player = player;
        }

        public void OpenChest(Vector3i blockPos, Inventory chestInventory)
        {
            currentChestPos = blockPos;
            currentChestInventory = chestInventory;
            CurrentState = UIState.Chest;
        }

        public void CloseUI()
        {
            // If player was holding an item, return it to inventory or drop
            if (!HeldItem.IsEmpty && player != null)
            {
                if (!player.Inventory.AddItem(HeldItem.Type, HeldItem.Count))
                {
                    player.DroppedItems.Add(new ItemDrop(HeldItem.Type, player.Position, HeldItem.Count));
                }
                HeldItem = ItemStack.Empty;
            }

            currentChestInventory = null;
            currentChestPos = null;
            CurrentState = UIState.HUD;
        }

        // ==========================================
        // Update & Input Handling
        // ==========================================

        public void Update(KeyboardState keyboard, MouseState mouse, Vector2 mousePosition, int screenWidth, int screenHeight, out bool cursorShouldBeGrabbed)
        {
            cursorShouldBeGrabbed = (CurrentState == UIState.HUD);

            // Toggle Inventory with 'E'
            bool eDown = keyboard.IsKeyDown(Keys.E);
            if (eDown && !eKeyWasDown)
            {
                if (CurrentState == UIState.HUD)
                {
                    CurrentState = UIState.InventoryAndCrafting;
                }
                else
                {
                    CloseUI();
                }
            }
            eKeyWasDown = eDown;

            // Toggle Pause or Close UI with Escape
            bool escDown = keyboard.IsKeyDown(Keys.Escape);
            if (escDown && !escKeyWasDown)
            {
                if (CurrentState == UIState.HUD)
                {
                    CurrentState = UIState.Paused;
                }
                else
                {
                    CloseUI();
                }
            }
            escKeyWasDown = escDown;

            // Handle UI Clicks when screen is open
            if (CurrentState != UIState.HUD)
            {
                HandleUIClicks(keyboard, mouse, mousePosition, screenWidth, screenHeight);
            }

            cursorShouldBeGrabbed = (CurrentState == UIState.HUD);
        }

        public void HandleMouseScroll(float scrollDelta)
        {
            if (CurrentState == UIState.HUD && player != null)
            {
                // Cycle hotbar slot
                int newSlot = player.SelectedHotbarSlot - (int)MathF.Sign(scrollDelta);
                if (newSlot < 0) newSlot = 8;
                if (newSlot > 8) newSlot = 0;
                player.SelectedHotbarSlot = newSlot;
            }
            else if (CurrentState == UIState.InventoryAndCrafting)
            {
                recipeScrollOffset -= (int)MathF.Sign(scrollDelta);
                if (recipeScrollOffset < 0) recipeScrollOffset = 0;
            }
        }

        private void HandleUIClicks(KeyboardState keyboard, MouseState mouse, Vector2 mousePos, int screenW, int screenH)
        {
            bool leftDown = mouse.IsButtonDown(MouseButton.Left);
            bool rightDown = mouse.IsButtonDown(MouseButton.Right);
            bool leftClicked = leftDown && !leftMouseWasDown;
            bool rightClicked = rightDown && !rightMouseWasDown;
            bool shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            leftMouseWasDown = leftDown;
            rightMouseWasDown = rightDown;

            if (player == null) return;

            float mx = mousePos.X;
            float my = mousePos.Y;

            if (CurrentState == UIState.InventoryAndCrafting)
            {
                HandleInventoryAndCraftingClicks(mx, my, screenW, screenH, leftClicked, rightClicked, shiftHeld);
            }
            else if (CurrentState == UIState.Chest)
            {
                HandleChestClicks(mx, my, screenW, screenH, leftClicked, rightClicked, shiftHeld);
            }
            else if (CurrentState == UIState.Paused)
            {
                HandlePauseMenuClicks(mx, my, screenW, screenH, leftClicked);
            }
        }

        private void HandlePauseMenuClicks(float mx, float my, int screenW, int screenH, bool leftClicked)
        {
            if (!leftClicked) return;

            float panelWidth = 320f;
            float panelHeight = 350f;
            float startX = (screenW - panelWidth) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            float btnW = 240f;
            float btnH = 38f;
            float btnX = startX + (panelWidth - btnW) * 0.5f;

            // 1. Resume Game
            float btn1Y = startY + 54f;
            if (mx >= btnX && mx <= btnX + btnW && my >= btn1Y && my <= btn1Y + btnH)
            {
                CloseUI();
                return;
            }

            // 2. Save Game
            float btn2Y = btn1Y + btnH + 8f;
            if (mx >= btnX && mx <= btnX + btnW && my >= btn2Y && my <= btn2Y + btnH)
            {
                OnSaveGameRequested?.Invoke();
                ShowSaveNotification();
                return;
            }

            // 3. Save & Main Menu
            float btn3Y = btn2Y + btnH + 8f;
            if (mx >= btnX && mx <= btnX + btnW && my >= btn3Y && my <= btn3Y + btnH)
            {
                OnSaveGameRequested?.Invoke();
                CloseUI();
                OnReturnToMainMenuRequested?.Invoke();
                return;
            }

            // 4. Toggle Fly Mode
            float btn4Y = btn3Y + btnH + 8f;
            if (mx >= btnX && mx <= btnX + btnW && my >= btn4Y && my <= btn4Y + btnH)
            {
                if (player != null)
                {
                    player.IsFlying = !player.IsFlying;
                    player.Velocity = Vector3.Zero;
                }
                return;
            }

            // 5. Quit to Desktop
            float btn5Y = btn4Y + btnH + 8f;
            if (mx >= btnX && mx <= btnX + btnW && my >= btn5Y && my <= btn5Y + btnH)
            {
                OnSaveGameRequested?.Invoke();
                OnQuitRequested?.Invoke();
                return;
            }
        }

        // ==========================================
        // Inventory & Crafting Screen Interactions
        // ==========================================

        private void HandleInventoryAndCraftingClicks(float mx, float my, int screenW, int screenH, bool leftClicked, bool rightClicked, bool shiftHeld)
        {
            if (player == null) return;

            float panelWidth = 410f;
            float panelHeight = 340f;
            float gap = 16f;
            float totalW = panelWidth * 2f + gap;
            float startX = (screenW - totalW) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            float leftPanelX = startX;
            float rightPanelX = startX + panelWidth + gap;

            // 1. Close Button [X] on Right Panel
            float closeX = rightPanelX + panelWidth - 26f;
            float closeY = startY + 8f;
            if (leftClicked && mx >= closeX && mx <= closeX + 18f && my >= closeY && my <= closeY + 18f)
            {
                CloseUI();
                return;
            }

            // 2. Crafting Category Tabs on Left Panel
            float tabY = startY + 36f;
            float tabH = 24f;
            float tabW = 90f;

            if (leftClicked)
            {
                // Tab: All
                if (mx >= leftPanelX + 14f && mx <= leftPanelX + 14f + tabW && my >= tabY && my <= tabY + tabH)
                {
                    activeCategory = RecipeCategory.All;
                    recipeScrollOffset = 0;
                }
                // Tab: Crafting
                else if (mx >= leftPanelX + 14f + tabW + 6f && mx <= leftPanelX + 14f + (tabW + 6f) * 2 && my >= tabY && my <= tabY + tabH)
                {
                    activeCategory = RecipeCategory.Crafting;
                    recipeScrollOffset = 0;
                }
                // Tab: Smelting
                else if (mx >= leftPanelX + 14f + (tabW + 6f) * 2 && mx <= leftPanelX + 14f + (tabW + 6f) * 3 && my >= tabY && my <= tabY + tabH)
                {
                    activeCategory = RecipeCategory.Smelting;
                    recipeScrollOffset = 0;
                }
            }

            // 3. Crafting Recipe Cards
            var recipes = GetFilteredRecipes();
            float recipeStartY = startY + 68f;
            float cardH = 44f;
            int maxVisible = 5;

            for (int i = 0; i < maxVisible; i++)
            {
                int rIdx = i + recipeScrollOffset;
                if (rIdx >= recipes.Count) break;

                var recipe = recipes[rIdx];
                float cardY = recipeStartY + i * (cardH + 6f);
                float cardX = leftPanelX + 14f;
                float cardW = panelWidth - 28f;

                if (leftClicked && mx >= cardX && mx <= cardX + cardW && my >= cardY && my <= cardY + cardH)
                {
                    // Clicked recipe -> Try craft
                    if (recipe.CanCraft(player.Inventory))
                    {
                        recipe.Craft(player.Inventory);
                    }
                    return;
                }
            }

            // 4. Player Inventory Slots (Right Panel)
            float slotSize = 36f;
            float slotGap = 4f;
            float invStartX = rightPanelX + (panelWidth - (9 * slotSize + 8 * slotGap)) * 0.5f;

            // Main inventory (slots 9 to 35) -> 3 rows of 9
            float mainStartY = startY + 44f;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    int slotIndex = 9 + row * 9 + col;
                    float sx = invStartX + col * (slotSize + slotGap);
                    float sy = mainStartY + row * (slotSize + slotGap);

                    if (mx >= sx && mx <= sx + slotSize && my >= sy && my <= sy + slotSize)
                    {
                        HandleSlotClick(player.Inventory, slotIndex, leftClicked, rightClicked, shiftHeld, isMainToHotbar: true);
                        return;
                    }
                }
            }

            // Hotbar (slots 0 to 8) -> 1 row of 9
            float hotbarY = mainStartY + 3 * (slotSize + slotGap) + 16f;
            for (int col = 0; col < 9; col++)
            {
                int slotIndex = col;
                float sx = invStartX + col * (slotSize + slotGap);
                float sy = hotbarY;

                if (mx >= sx && mx <= sx + slotSize && my >= sy && my <= sy + slotSize)
                {
                    HandleSlotClick(player.Inventory, slotIndex, leftClicked, rightClicked, shiftHeld, isMainToHotbar: false);
                    return;
                }
            }
        }

        // ==========================================
        // Chest Screen Interactions
        // ==========================================

        private void HandleChestClicks(float mx, float my, int screenW, int screenH, bool leftClicked, bool rightClicked, bool shiftHeld)
        {
            if (player == null || currentChestInventory == null) return;

            float panelWidth = 430f;
            float panelHeight = 420f;
            float startX = (screenW - panelWidth) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            // Close Button [X]
            float closeX = startX + panelWidth - 26f;
            float closeY = startY + 8f;
            if (leftClicked && mx >= closeX && mx <= closeX + 18f && my >= closeY && my <= closeY + 18f)
            {
                CloseUI();
                return;
            }

            float slotSize = 36f;
            float slotGap = 4f;
            float invStartX = startX + (panelWidth - (9 * slotSize + 8 * slotGap)) * 0.5f;

            // 1. Chest Slots (18 slots = 2 rows of 9)
            float chestStartY = startY + 40f;
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    int slotIndex = row * 9 + col;
                    float sx = invStartX + col * (slotSize + slotGap);
                    float sy = chestStartY + row * (slotSize + slotGap);

                    if (mx >= sx && mx <= sx + slotSize && my >= sy && my <= sy + slotSize)
                    {
                        if (shiftHeld && leftClicked)
                        {
                            // Transfer from Chest to Player
                            var stack = currentChestInventory.Slots[slotIndex];
                            if (!stack.IsEmpty)
                            {
                                if (player.Inventory.AddItem(stack.Type, stack.Count))
                                {
                                    currentChestInventory.Slots[slotIndex].Clear();
                                }
                            }
                        }
                        else
                        {
                            HandleSlotClickDirect(currentChestInventory, slotIndex, leftClicked, rightClicked);
                        }
                        return;
                    }
                }
            }

            // 2. Player Inventory (Main 27 + Hotbar 9)
            float playerInvStartY = chestStartY + 2 * (slotSize + slotGap) + 36f;

            // Main (slots 9 to 35)
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    int slotIndex = 9 + row * 9 + col;
                    float sx = invStartX + col * (slotSize + slotGap);
                    float sy = playerInvStartY + row * (slotSize + slotGap);

                    if (mx >= sx && mx <= sx + slotSize && my >= sy && my <= sy + slotSize)
                    {
                        if (shiftHeld && leftClicked)
                        {
                            // Transfer from Player to Chest
                            var stack = player.Inventory.Slots[slotIndex];
                            if (!stack.IsEmpty)
                            {
                                if (currentChestInventory.AddItem(stack.Type, stack.Count))
                                {
                                    player.Inventory.Slots[slotIndex].Clear();
                                }
                            }
                        }
                        else
                        {
                            HandleSlotClickDirect(player.Inventory, slotIndex, leftClicked, rightClicked);
                        }
                        return;
                    }
                }
            }

            // Hotbar (slots 0 to 8)
            float playerHotbarY = playerInvStartY + 3 * (slotSize + slotGap) + 12f;
            for (int col = 0; col < 9; col++)
            {
                int slotIndex = col;
                float sx = invStartX + col * (slotSize + slotGap);
                float sy = playerHotbarY;

                if (mx >= sx && mx <= sx + slotSize && my >= sy && my <= sy + slotSize)
                {
                    if (shiftHeld && leftClicked)
                    {
                        var stack = player.Inventory.Slots[slotIndex];
                        if (!stack.IsEmpty)
                        {
                            if (currentChestInventory.AddItem(stack.Type, stack.Count))
                            {
                                player.Inventory.Slots[slotIndex].Clear();
                            }
                        }
                    }
                    else
                    {
                        HandleSlotClickDirect(player.Inventory, slotIndex, leftClicked, rightClicked);
                    }
                    return;
                }
            }
        }

        private void HandleSlotClick(Inventory inv, int slotIndex, bool leftClicked, bool rightClicked, bool shiftHeld, bool isMainToHotbar)
        {
            if (shiftHeld && leftClicked)
            {
                // Quick transfer between hotbar and main inventory
                var stack = inv.Slots[slotIndex];
                if (!stack.IsEmpty)
                {
                    int targetStart = isMainToHotbar ? 0 : 9;
                    int targetEnd = isMainToHotbar ? 9 : 36;

                    // Try to add to target section
                    for (int i = targetStart; i < targetEnd; i++)
                    {
                        if (inv.Slots[i].Type == stack.Type && inv.Slots[i].Count < 64)
                        {
                            int space = 64 - inv.Slots[i].Count;
                            int add = Math.Min(space, stack.Count);
                            inv.Slots[i].Count += add;
                            stack.Count -= add;
                            if (stack.Count <= 0) break;
                        }
                    }

                    if (stack.Count > 0)
                    {
                        for (int i = targetStart; i < targetEnd; i++)
                        {
                            if (inv.Slots[i].IsEmpty)
                            {
                                inv.Slots[i] = stack;
                                stack.Clear();
                                break;
                            }
                        }
                    }

                    inv.Slots[slotIndex] = stack;
                }
                return;
            }

            HandleSlotClickDirect(inv, slotIndex, leftClicked, rightClicked);
        }

        private void HandleSlotClickDirect(Inventory inv, int slotIndex, bool leftClicked, bool rightClicked)
        {
            if (leftClicked)
            {
                if (HeldItem.IsEmpty)
                {
                    // Pick up item
                    HeldItem = inv.Slots[slotIndex];
                    inv.Slots[slotIndex].Clear();
                }
                else
                {
                    // Place, merge, or swap
                    if (inv.Slots[slotIndex].IsEmpty)
                    {
                        inv.Slots[slotIndex] = HeldItem;
                        HeldItem = ItemStack.Empty;
                    }
                    else if (inv.Slots[slotIndex].Type == HeldItem.Type)
                    {
                        int space = 64 - inv.Slots[slotIndex].Count;
                        int add = Math.Min(space, HeldItem.Count);
                        inv.Slots[slotIndex].Count += add;
                        HeldItem.Count -= add;
                        if (HeldItem.Count <= 0) HeldItem = ItemStack.Empty;
                    }
                    else
                    {
                        // Swap
                        var temp = inv.Slots[slotIndex];
                        inv.Slots[slotIndex] = HeldItem;
                        HeldItem = temp;
                    }
                }
            }
            else if (rightClicked)
            {
                if (HeldItem.IsEmpty)
                {
                    // Split half stack
                    if (!inv.Slots[slotIndex].IsEmpty)
                    {
                        int take = (inv.Slots[slotIndex].Count + 1) / 2;
                        HeldItem = new ItemStack(inv.Slots[slotIndex].Type, take);
                        inv.Slots[slotIndex].Count -= take;
                        if (inv.Slots[slotIndex].Count <= 0) inv.Slots[slotIndex].Clear();
                    }
                }
                else
                {
                    // Place 1 item into slot
                    if (inv.Slots[slotIndex].IsEmpty)
                    {
                        inv.Slots[slotIndex] = new ItemStack(HeldItem.Type, 1);
                        HeldItem.Count--;
                        if (HeldItem.Count <= 0) HeldItem = ItemStack.Empty;
                    }
                    else if (inv.Slots[slotIndex].Type == HeldItem.Type && inv.Slots[slotIndex].Count < 64)
                    {
                        inv.Slots[slotIndex].Count++;
                        HeldItem.Count--;
                        if (HeldItem.Count <= 0) HeldItem = ItemStack.Empty;
                    }
                }
            }
        }

        private List<CraftingRecipe> GetFilteredRecipes()
        {
            return activeCategory switch
            {
                RecipeCategory.Crafting => RecipeRegistry.Recipes.Where(r => !r.IsSmelting).ToList(),
                RecipeCategory.Smelting => RecipeRegistry.Recipes.Where(r => r.IsSmelting).ToList(),
                _ => RecipeRegistry.Recipes
            };
        }

        // ==========================================
        // Rendering
        // ==========================================

        public void Render(int screenWidth, int screenHeight, Vector2 mousePosition)
        {
            hoveredTooltip = null;

            renderer.Begin(screenWidth, screenHeight);

            switch (CurrentState)
            {
                case UIState.HUD:
                    RenderHUD(screenWidth, screenHeight);
                    break;

                case UIState.InventoryAndCrafting:
                    RenderInventoryAndCrafting(screenWidth, screenHeight, mousePosition);
                    break;

                case UIState.Chest:
                    RenderChestScreen(screenWidth, screenHeight, mousePosition);
                    break;

                case UIState.Paused:
                    RenderPauseMenu(screenWidth, screenHeight, mousePosition);
                    break;
            }

            // Render Held Item (follows mouse cursor)
            if (!HeldItem.IsEmpty)
            {
                float iconSize = 32f;
                renderer.DrawItemIcon(HeldItem.Type, mousePosition.X - iconSize * 0.5f, mousePosition.Y - iconSize * 0.5f, iconSize);
                if (HeldItem.Count > 1)
                {
                    string cnt = HeldItem.Count.ToString();
                    renderer.DrawText(cnt, mousePosition.X + 6f, mousePosition.Y + 4f, 1.25f, Vector4.One, shadow: true);
                }
            }

            // Render Tooltip on top
            if (!string.IsNullOrEmpty(hoveredTooltip))
            {
                renderer.DrawTooltip(hoveredTooltip, mousePosition.X, mousePosition.Y, screenWidth, screenHeight);
            }

            renderer.End();
        }

        private void RenderHUD(int screenW, int screenH)
        {
            if (player == null) return;

            // 1. Center Crosshair
            renderer.DrawCrosshair(screenW * 0.5f, screenH * 0.5f);

            // In-game Clock Display (Top Right)
            if (DayNightCycle != null)
            {
                string timeStr = DayNightCycle.GetTimeString();
                float clockScale = 1.1f;
                Vector2 textSz = renderer.MeasureText(timeStr, clockScale);
                float clockX = screenW - textSz.X - 24f;
                float clockY = 16f;

                renderer.DrawPanel(clockX - 8f, clockY - 4f, textSz.X + 16f, textSz.Y + 8f,
                    new Vector4(0.08f, 0.09f, 0.12f, 0.75f),
                    new Vector4(0.28f, 0.32f, 0.40f, 0.65f), 1.0f);
                renderer.DrawText(timeStr, clockX, clockY, clockScale, new Vector4(0.95f, 0.95f, 0.95f, 0.95f), shadow: true);
            }

            // 2. Block Breaking Progress Bar (if breaking a block)
            if (player.BreakingProgress > 0.0f)
            {
                float barW = 140f;
                float barH = 10f;
                float barX = (screenW - barW) * 0.5f;
                float barY = screenH - 96f;

                Vector4 barBg = new Vector4(0.1f, 0.11f, 0.14f, 0.85f);
                Vector4 barFill = new Vector4(0.95f, 0.45f, 0.15f, 0.95f); // Fiery orange breaking color
                renderer.DrawProgressBar(barX, barY, barW, barH, player.BreakingProgress, barFill, barBg);
                renderer.DrawText("BREAKING...", barX + 24f, barY - 14f, 1.0f, new Vector4(0.9f, 0.9f, 0.9f, 0.9f));
            }

            // 3. Hotbar at bottom center
            float slotSize = 42f;
            float slotGap = 4f;
            float totalHotbarW = 9 * slotSize + 8 * slotGap;
            float pad = 6f;
            float hx = (screenW - totalHotbarW) * 0.5f;
            float hy = screenH - slotSize - 16f;

            // Semi-transparent frosted glass panel behind hotbar
            renderer.DrawPanel(
                hx - pad,
                hy - pad,
                totalHotbarW + pad * 2f,
                slotSize + pad * 2f,
                new Vector4(0.08f, 0.09f, 0.12f, 0.85f),
                new Vector4(0.28f, 0.32f, 0.40f, 0.70f),
                1.5f
            );

            for (int i = 0; i < 9; i++)
            {
                float sx = hx + i * (slotSize + slotGap);
                bool isSelected = (i == player.SelectedHotbarSlot);

                renderer.DrawSlot(sx, hy, slotSize, player.Inventory.Slots[i], isSelected: isSelected);

                // Slot number label 1..9
                renderer.DrawText((i + 1).ToString(), sx + 3f, hy + 3f, 0.9f, new Vector4(0.65f, 0.70f, 0.80f, 0.85f), shadow: true);
            }

            // Display currently selected item name above hotbar
            var selStack = player.Inventory.Slots[player.SelectedHotbarSlot];
            if (!selStack.IsEmpty)
            {
                var def = BlockRegistry.Get(selStack.Type);
                string selText = $"{def.Name} ({selStack.Count})";
                Vector2 textSize = renderer.MeasureText(selText, 1.1f);
                float textX = (screenW - textSize.X) * 0.5f;
                renderer.DrawText(selText, textX, hy - 20f, 1.1f, new Vector4(1f, 1f, 1f, 0.9f), shadow: true);
            }
        }

        private void RenderInventoryAndCrafting(int screenW, int screenH, Vector2 mousePos)
        {
            if (player == null) return;

            // Dimmed screen backdrop
            renderer.DrawQuad(0, 0, screenW, screenH, new Vector4(0.02f, 0.02f, 0.03f, 0.65f));

            float panelWidth = 410f;
            float panelHeight = 340f;
            float gap = 16f;
            float totalW = panelWidth * 2f + gap;
            float startX = (screenW - totalW) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            float leftPanelX = startX;
            float rightPanelX = startX + panelWidth + gap;

            Vector4 panelBg = new Vector4(0.12f, 0.13f, 0.16f, 0.92f);
            Vector4 panelBorder = new Vector4(0.28f, 0.32f, 0.40f, 0.80f);

            // =========================
            // Left Panel: Crafting List
            // =========================
            renderer.DrawPanel(leftPanelX, startY, panelWidth, panelHeight, panelBg, panelBorder, 1.5f);
            renderer.DrawText("CRAFTING & RECIPES", leftPanelX + 14f, startY + 12f, 1.3f, new Vector4(0.95f, 0.95f, 1.0f, 1.0f));

            // Category Tabs
            float tabY = startY + 36f;
            float tabH = 22f;
            float tabW = 86f;

            RenderTab("All", leftPanelX + 14f, tabY, tabW, tabH, activeCategory == RecipeCategory.All, mousePos);
            RenderTab("Crafting", leftPanelX + 14f + tabW + 6f, tabY, tabW, tabH, activeCategory == RecipeCategory.Crafting, mousePos);
            RenderTab("Smelting", leftPanelX + 14f + (tabW + 6f) * 2, tabY, tabW, tabH, activeCategory == RecipeCategory.Smelting, mousePos);

            // Recipe Cards
            var recipes = GetFilteredRecipes();
            float recipeStartY = startY + 68f;
            float cardH = 44f;
            int maxVisible = 5;

            for (int i = 0; i < maxVisible; i++)
            {
                int rIdx = i + recipeScrollOffset;
                if (rIdx >= recipes.Count) break;

                var recipe = recipes[rIdx];
                float cardY = recipeStartY + i * (cardH + 6f);
                float cardX = leftPanelX + 14f;
                float cardW = panelWidth - 28f;

                bool canCraft = recipe.CanCraft(player.Inventory);
                bool isHovered = mousePos.X >= cardX && mousePos.X <= cardX + cardW && mousePos.Y >= cardY && mousePos.Y <= cardY + cardH;

                Vector4 cardBg = isHovered
                    ? (canCraft ? new Vector4(0.18f, 0.24f, 0.22f, 0.90f) : new Vector4(0.18f, 0.18f, 0.20f, 0.90f))
                    : new Vector4(0.10f, 0.11f, 0.14f, 0.85f);

                Vector4 cardBorder = isHovered
                    ? (canCraft ? new Vector4(0.35f, 0.85f, 0.50f, 0.90f) : new Vector4(0.45f, 0.48f, 0.55f, 0.70f))
                    : (canCraft ? new Vector4(0.25f, 0.55f, 0.35f, 0.65f) : new Vector4(0.20f, 0.22f, 0.26f, 0.55f));

                renderer.DrawPanel(cardX, cardY, cardW, cardH, cardBg, cardBorder, 1.0f);

                // Result Icon
                renderer.DrawItemIcon(recipe.Result.Type, cardX + 6f, cardY + 6f, 32f);

                // Recipe Name & Quantity
                string title = $"{recipe.Name} (x{recipe.Result.Count})";
                Vector4 titleColor = canCraft ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.65f, 0.65f, 0.70f, 0.8f);
                renderer.DrawText(title, cardX + 44f, cardY + 7f, 1.1f, titleColor);

                // Ingredients list string
                string ingStr = string.Join(" + ", recipe.Ingredients.Select(ing => $"{ing.Count} {BlockRegistry.Get(ing.Type).Name}"));
                renderer.DrawText(ingStr, cardX + 44f, cardY + 24f, 0.9f, new Vector4(0.55f, 0.60f, 0.70f, 0.85f));

                // Status Badge [CRAFT] / [LOCKED]
                if (canCraft)
                {
                    renderer.DrawText("[CRAFT]", cardX + cardW - 64f, cardY + 16f, 1.0f, new Vector4(0.4f, 0.95f, 0.55f, 1.0f));
                }
                else
                {
                    renderer.DrawText("[NEED]", cardX + cardW - 56f, cardY + 16f, 1.0f, new Vector4(0.55f, 0.58f, 0.65f, 0.6f));
                }

                if (isHovered)
                {
                    hoveredTooltip = canCraft ? $"Click to craft {recipe.Name}" : $"Requires: {ingStr}";
                }
            }

            // =========================
            // Right Panel: Inventory
            // =========================
            renderer.DrawPanel(rightPanelX, startY, panelWidth, panelHeight, panelBg, panelBorder, 1.5f);
            renderer.DrawText("INVENTORY", rightPanelX + 14f, startY + 12f, 1.3f, new Vector4(0.95f, 0.95f, 1.0f, 1.0f));

            // Close Button [X]
            float closeX = rightPanelX + panelWidth - 26f;
            float closeY = startY + 8f;
            bool isCloseHovered = mousePos.X >= closeX && mousePos.X <= closeX + 18f && mousePos.Y >= closeY && mousePos.Y <= closeY + 18f;
            renderer.DrawPanel(
                closeX, closeY, 18f, 18f,
                isCloseHovered ? new Vector4(0.8f, 0.2f, 0.2f, 0.9f) : new Vector4(0.2f, 0.22f, 0.26f, 0.8f),
                new Vector4(0.4f, 0.45f, 0.55f, 0.7f),
                1.0f
            );
            renderer.DrawText("X", closeX + 5f, closeY + 4f, 1.1f, Vector4.One);

            float slotSize = 36f;
            float slotGap = 4f;
            float invStartX = rightPanelX + (panelWidth - (9 * slotSize + 8 * slotGap)) * 0.5f;

            // Main Inventory (slots 9 to 35)
            float mainStartY = startY + 44f;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    int slotIndex = 9 + row * 9 + col;
                    float sx = invStartX + col * (slotSize + slotGap);
                    float sy = mainStartY + row * (slotSize + slotGap);

                    bool isHovered = mousePos.X >= sx && mousePos.X <= sx + slotSize && mousePos.Y >= sy && mousePos.Y <= sy + slotSize;
                    var stack = player.Inventory.Slots[slotIndex];

                    renderer.DrawSlot(sx, sy, slotSize, stack, isSelected: false, isHovered: isHovered);

                    if (isHovered && !stack.IsEmpty)
                    {
                        var def = BlockRegistry.Get(stack.Type);
                        hoveredTooltip = $"{def.Name} (x{stack.Count})";
                    }
                }
            }

            // Divider bar
            float divY = mainStartY + 3 * (slotSize + slotGap) + 8f;
            renderer.DrawQuad(invStartX, divY, (9 * slotSize + 8 * slotGap), 1.5f, new Vector4(0.3f, 0.35f, 0.45f, 0.5f));

            // Hotbar (slots 0 to 8)
            float hotbarY = divY + 8f;
            renderer.DrawText("HOTBAR", invStartX, hotbarY - 12f, 0.85f, new Vector4(0.55f, 0.60f, 0.70f, 0.8f));

            for (int col = 0; col < 9; col++)
            {
                int slotIndex = col;
                float sx = invStartX + col * (slotSize + slotGap);
                float sy = hotbarY;

                bool isSelected = (col == player.SelectedHotbarSlot);
                bool isHovered = mousePos.X >= sx && mousePos.X <= sx + slotSize && mousePos.Y >= sy && mousePos.Y <= sy + slotSize;
                var stack = player.Inventory.Slots[slotIndex];

                renderer.DrawSlot(sx, sy, slotSize, stack, isSelected: isSelected, isHovered: isHovered);

                if (isHovered && !stack.IsEmpty)
                {
                    var def = BlockRegistry.Get(stack.Type);
                    hoveredTooltip = $"{def.Name} (x{stack.Count})";
                }
            }
        }

        private void RenderChestScreen(int screenW, int screenH, Vector2 mousePos)
        {
            if (player == null || currentChestInventory == null) return;

            // Dimmed backdrop
            renderer.DrawQuad(0, 0, screenW, screenH, new Vector4(0.02f, 0.02f, 0.03f, 0.65f));

            float panelWidth = 430f;
            float panelHeight = 420f;
            float startX = (screenW - panelWidth) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            Vector4 panelBg = new Vector4(0.12f, 0.13f, 0.16f, 0.94f);
            Vector4 panelBorder = new Vector4(0.28f, 0.32f, 0.40f, 0.85f);

            renderer.DrawPanel(startX, startY, panelWidth, panelHeight, panelBg, panelBorder, 1.5f);
            renderer.DrawText("CHEST STORAGE", startX + 16f, startY + 14f, 1.3f, new Vector4(0.95f, 0.95f, 1.0f, 1.0f));

            // Close Button [X]
            float closeX = startX + panelWidth - 28f;
            float closeY = startY + 10f;
            bool isCloseHovered = mousePos.X >= closeX && mousePos.X <= closeX + 18f && mousePos.Y >= closeY && mousePos.Y <= closeY + 18f;
            renderer.DrawPanel(
                closeX, closeY, 18f, 18f,
                isCloseHovered ? new Vector4(0.8f, 0.2f, 0.2f, 0.9f) : new Vector4(0.2f, 0.22f, 0.26f, 0.8f),
                new Vector4(0.4f, 0.45f, 0.55f, 0.7f),
                1.0f
            );
            renderer.DrawText("X", closeX + 5f, closeY + 4f, 1.1f, Vector4.One);

            float slotSize = 36f;
            float slotGap = 4f;
            float invStartX = startX + (panelWidth - (9 * slotSize + 8 * slotGap)) * 0.5f;

            // 1. Chest Slots (18 slots = 2 rows of 9)
            float chestStartY = startY + 40f;
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    int slotIndex = row * 9 + col;
                    float sx = invStartX + col * (slotSize + slotGap);
                    float sy = chestStartY + row * (slotSize + slotGap);

                    bool isHovered = mousePos.X >= sx && mousePos.X <= sx + slotSize && mousePos.Y >= sy && mousePos.Y <= sy + slotSize;
                    var stack = currentChestInventory.Slots[slotIndex];

                    renderer.DrawSlot(sx, sy, slotSize, stack, isSelected: false, isHovered: isHovered);

                    if (isHovered && !stack.IsEmpty)
                    {
                        var def = BlockRegistry.Get(stack.Type);
                        hoveredTooltip = $"{def.Name} (x{stack.Count})";
                    }
                }
            }

            // Divider line
            float divY = chestStartY + 2 * (slotSize + slotGap) + 16f;
            renderer.DrawQuad(invStartX, divY, (9 * slotSize + 8 * slotGap), 1.5f, new Vector4(0.3f, 0.35f, 0.45f, 0.5f));
            renderer.DrawText("PLAYER INVENTORY", invStartX, divY + 8f, 0.95f, new Vector4(0.85f, 0.88f, 0.95f, 0.9f));

            // 2. Player Inventory (Main 27 slots)
            float playerInvStartY = divY + 24f;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    int slotIndex = 9 + row * 9 + col;
                    float sx = invStartX + col * (slotSize + slotGap);
                    float sy = playerInvStartY + row * (slotSize + slotGap);

                    bool isHovered = mousePos.X >= sx && mousePos.X <= sx + slotSize && mousePos.Y >= sy && mousePos.Y <= sy + slotSize;
                    var stack = player.Inventory.Slots[slotIndex];

                    renderer.DrawSlot(sx, sy, slotSize, stack, isSelected: false, isHovered: isHovered);

                    if (isHovered && !stack.IsEmpty)
                    {
                        var def = BlockRegistry.Get(stack.Type);
                        hoveredTooltip = $"{def.Name} (x{stack.Count})";
                    }
                }
            }

            // Hotbar (slots 0 to 8)
            float playerHotbarY = playerInvStartY + 3 * (slotSize + slotGap) + 12f;
            for (int col = 0; col < 9; col++)
            {
                int slotIndex = col;
                float sx = invStartX + col * (slotSize + slotGap);
                float sy = playerHotbarY;

                bool isSelected = (col == player.SelectedHotbarSlot);
                bool isHovered = mousePos.X >= sx && mousePos.X <= sx + slotSize && mousePos.Y >= sy && mousePos.Y <= sy + slotSize;
                var stack = player.Inventory.Slots[slotIndex];

                renderer.DrawSlot(sx, sy, slotSize, stack, isSelected: isSelected, isHovered: isHovered);

                if (isHovered && !stack.IsEmpty)
                {
                    var def = BlockRegistry.Get(stack.Type);
                    hoveredTooltip = $"{def.Name} (x{stack.Count})";
                }
            }
        }

        private void RenderTab(string label, float x, float y, float w, float h, bool isActive, Vector2 mousePos)
        {
            bool isHovered = mousePos.X >= x && mousePos.X <= x + w && mousePos.Y >= y && mousePos.Y <= y + h;

            Vector4 bg = isActive
                ? new Vector4(0.24f, 0.28f, 0.36f, 0.95f)
                : (isHovered ? new Vector4(0.18f, 0.20f, 0.25f, 0.85f) : new Vector4(0.10f, 0.11f, 0.14f, 0.75f));

            Vector4 border = isActive
                ? new Vector4(0.45f, 0.65f, 0.95f, 0.95f)
                : new Vector4(0.25f, 0.28f, 0.35f, 0.6f);

            renderer.DrawPanel(x, y, w, h, bg, border, 1.0f);
            Vector2 textSz = renderer.MeasureText(label, 0.95f);
            float tx = x + (w - textSz.X) * 0.5f;
            float ty = y + (h - textSz.Y) * 0.5f;
            renderer.DrawText(label, tx, ty, 0.95f, isActive ? Vector4.One : new Vector4(0.7f, 0.72f, 0.8f, 0.85f), shadow: true);
        }

        private void RenderPauseMenu(int screenW, int screenH, Vector2 mousePos)
        {
            // Dimmed screen backdrop
            renderer.DrawQuad(0, 0, screenW, screenH, new Vector4(0.02f, 0.02f, 0.04f, 0.70f));

            float panelWidth = 320f;
            float panelHeight = 350f;
            float startX = (screenW - panelWidth) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            Vector4 panelBg = new Vector4(0.12f, 0.13f, 0.17f, 0.95f);
            Vector4 panelBorder = new Vector4(0.30f, 0.35f, 0.45f, 0.85f);

            // Dialog Panel
            renderer.DrawPanel(startX, startY, panelWidth, panelHeight, panelBg, panelBorder, 1.5f);

            // Title
            string title = "GAME PAUSED";
            Vector2 titleSz = renderer.MeasureText(title, 1.4f);
            renderer.DrawText(title, startX + (panelWidth - titleSz.X) * 0.5f, startY + 16f, 1.4f, new Vector4(0.95f, 0.95f, 1.0f, 1.0f));

            // Save Notification toast
            if (saveNotificationTimer > 0f)
            {
                string savedMsg = "WORLD SAVED SUCCESSFULLY!";
                Vector2 msgSz = renderer.MeasureText(savedMsg, 0.95f);
                renderer.DrawText(savedMsg, startX + (panelWidth - msgSz.X) * 0.5f, startY + 38f, 0.95f, new Vector4(0.40f, 1.0f, 0.50f, 1.0f));
            }
            else
            {
                // Divider line
                renderer.DrawQuad(startX + 20f, startY + 42f, panelWidth - 40f, 1.5f, new Vector4(0.30f, 0.35f, 0.45f, 0.6f));
            }

            float btnW = 240f;
            float btnH = 38f;
            float btnX = startX + (panelWidth - btnW) * 0.5f;

            // Button 1: Resume Game
            float btn1Y = startY + 54f;
            RenderMenuButton("RESUME GAME", btnX, btn1Y, btnW, btnH, mousePos);

            // Button 2: Save Game
            float btn2Y = btn1Y + btnH + 8f;
            RenderMenuButton("SAVE GAME", btnX, btn2Y, btnW, btnH, mousePos);

            // Button 3: Save & Main Menu
            float btn3Y = btn2Y + btnH + 8f;
            RenderMenuButton("SAVE & MAIN MENU", btnX, btn3Y, btnW, btnH, mousePos);

            // Button 4: Toggle Fly Mode
            float btn4Y = btn3Y + btnH + 8f;
            string flyText = (player?.IsFlying ?? false) ? "FLIGHT: ENABLED" : "FLIGHT: DISABLED";
            RenderMenuButton(flyText, btnX, btn4Y, btnW, btnH, mousePos);

            // Button 5: Quit to Desktop
            float btn5Y = btn4Y + btnH + 8f;
            RenderMenuButton("QUIT TO DESKTOP", btnX, btn5Y, btnW, btnH, mousePos, isDestructive: true);
        }

        private void RenderMenuButton(string text, float x, float y, float w, float h, Vector2 mousePos, bool isDestructive = false)
        {
            bool isHovered = mousePos.X >= x && mousePos.X <= x + w && mousePos.Y >= y && mousePos.Y <= y + h;

            Vector4 bg = isHovered
                ? (isDestructive ? new Vector4(0.35f, 0.15f, 0.18f, 0.95f) : new Vector4(0.20f, 0.25f, 0.35f, 0.95f))
                : new Vector4(0.14f, 0.15f, 0.19f, 0.85f);

            Vector4 border = isHovered
                ? (isDestructive ? new Vector4(0.9f, 0.3f, 0.35f, 0.95f) : new Vector4(0.45f, 0.70f, 1.0f, 0.95f))
                : new Vector4(0.28f, 0.32f, 0.40f, 0.70f);

            renderer.DrawPanel(x, y, w, h, bg, border, 1.5f);

            Vector2 textSz = renderer.MeasureText(text, 1.1f);
            float tx = x + (w - textSz.X) * 0.5f;
            float ty = y + (h - textSz.Y) * 0.5f;
            Vector4 textColor = isHovered ? Vector4.One : new Vector4(0.85f, 0.88f, 0.95f, 0.9f);
            renderer.DrawText(text, tx, ty, 1.1f, textColor, shadow: true);
        }
    }
}
