using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VelmoraCraft.src.Entities;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.UI
{
    public enum UIState
    {
        HUD,
        PersonalInventory,
        CraftingTable,
        Furnace,
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
        public Func<bool>? IsFullscreenGetter { get; set; }
        public Action? OnToggleFullscreenRequested { get; set; }
        public Action? OnSettingsChanged { get; set; }
        public bool IsFullscreen => IsFullscreenGetter?.Invoke() ?? false;
        public bool IsHudEnabled { get; set; } = true;
        public bool IsDebugCoordinatesEnabled { get; set; } = false;
        public DayNightCycle? DayNightCycle { get; set; }
        private float saveNotificationTimer = 0f;

        public void ShowSaveNotification() => saveNotificationTimer = 2.5f;

        // Toast notification (screenshot, bed, info)
        private long screenshotNotificationStartTime = 0;
        private string screenshotNotificationMessage = "";

        public void ShowToast(string message)
        {
            screenshotNotificationMessage = message;
            screenshotNotificationStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public void ShowScreenshotNotification(string filename)
        {
            ShowToast($"Saved screenshot as {filename}");
        }

        // Sleep screen transition
        private long sleepStartTime = 0;
        private const double SLEEP_TOTAL_SECONDS = 1.8;
        private bool hasSkippedNightThisSleep = false;

        public void StartSleeping()
        {
            sleepStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
            hasSkippedNightThisSleep = false;
        }

        public bool IsSleeping => sleepStartTime != 0;

        // Player, world, chest & station references
        private Player? player;
        private World.World? world;
        private Inventory? currentChestInventory;
        private Vector3i? currentChestPos;
        private Vector3i? currentFurnacePos;

        // Render Distance & FOV Slider settings
        private int renderDistanceChunks = 8;
        private bool isDraggingRenderDistanceSlider = false;
        public int RenderDistanceChunks
        {
            get => renderDistanceChunks;
            set => SetRenderDistance(value);
        }

        private bool isDraggingFovSlider = false;
        private float savedFov = 70f;
        public float FieldOfView
        {
            get => player?.Camera?.BaseFov ?? savedFov;
            set
            {
                savedFov = Math.Clamp(value, 30f, 110f);
                if (player?.Camera != null)
                {
                    player.Camera.BaseFov = savedFov;
                    player.Camera.CurrentFov = savedFov;
                }
            }
        }

        // Held item for drag & drop
        public ItemStack HeldItem = ItemStack.Empty;

        // Crafting UI state
        private RecipeCategory activeCategory = RecipeCategory.All;
        private int recipeScrollOffset = 0;

        // Hover tracking
        private string? hoveredTooltip = null;

        // FPS Counter tracking
        private long lastFpsTimestamp = 0;
        private int fpsFrameCounter = 0;
        private int cachedFpsValue = 60;
        private string cachedFpsString = "FPS: 60";

        // Key states for debouncing
        private bool eKeyWasDown = false;
        private bool escKeyWasDown = false;
        private bool f1KeyWasDown = false;
        private bool f3KeyWasDown = false;
        private bool f11KeyWasDown = false;
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
            if (player?.Camera != null)
            {
                player.Camera.BaseFov = savedFov;
                player.Camera.CurrentFov = savedFov;
            }
        }

        public void SetWorld(World.World? world)
        {
            this.world = world;
            if (world != null)
            {
                world.ViewDistanceChunks = renderDistanceChunks;
            }
        }

        public void SetRenderDistance(int chunks)
        {
            renderDistanceChunks = Math.Clamp(chunks, 2, 16);
            if (world != null)
            {
                world.ViewDistanceChunks = renderDistanceChunks;
            }
        }

        public void OpenChest(Vector3i blockPos, Inventory chestInventory)
        {
            currentChestPos = blockPos;
            currentChestInventory = chestInventory;
            CurrentState = UIState.Chest;
        }

        public void OpenCraftingTable()
        {
            activeCategory = RecipeCategory.All;
            recipeScrollOffset = 0;
            CurrentState = UIState.CraftingTable;
        }

        public void OpenFurnace(Vector3i blockPos)
        {
            currentFurnacePos = blockPos;
            activeCategory = RecipeCategory.Smelting;
            recipeScrollOffset = 0;
            CurrentState = UIState.Furnace;
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

            if (currentChestInventory is DoubleChestInventory doubleChest)
            {
                doubleChest.SyncToChests();
            }

            currentChestInventory = null;
            currentChestPos = null;
            currentFurnacePos = null;
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
                    activeCategory = RecipeCategory.All;
                    recipeScrollOffset = 0;
                    CurrentState = UIState.PersonalInventory;
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

            // Toggle HUD with F1
            bool f1Down = keyboard.IsKeyDown(Keys.F1);
            if (f1Down && !f1KeyWasDown)
            {
                IsHudEnabled = !IsHudEnabled;
                OnSettingsChanged?.Invoke();
            }
            f1KeyWasDown = f1Down;

            // Toggle Coordinates / F3 Debug Screen with F3
            bool f3Down = keyboard.IsKeyDown(Keys.F3);
            if (f3Down && !f3KeyWasDown)
            {
                IsDebugCoordinatesEnabled = !IsDebugCoordinatesEnabled;
            }
            f3KeyWasDown = f3Down;

            // Toggle Fullscreen with F11
            bool f11Down = keyboard.IsKeyDown(Keys.F11);
            if (f11Down && !f11KeyWasDown)
            {
                OnToggleFullscreenRequested?.Invoke();
                OnSettingsChanged?.Invoke();
            }
            f11KeyWasDown = f11Down;

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
            else if (CurrentState == UIState.PersonalInventory || CurrentState == UIState.CraftingTable || CurrentState == UIState.Furnace)
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

            if (CurrentState == UIState.PersonalInventory || CurrentState == UIState.CraftingTable || CurrentState == UIState.Furnace)
            {
                HandleInventoryAndCraftingClicks(mx, my, screenW, screenH, leftClicked, rightClicked, shiftHeld);
            }
            else if (CurrentState == UIState.Chest)
            {
                HandleChestClicks(mx, my, screenW, screenH, leftClicked, rightClicked, shiftHeld);
            }
            else if (CurrentState == UIState.Paused)
            {
                HandlePauseMenuClicks(mx, my, screenW, screenH, leftClicked, leftDown);
            }
        }

        private void HandlePauseMenuClicks(float mx, float my, int screenW, int screenH, bool leftClicked, bool leftDown)
        {
            float panelWidth = 320f;
            float panelHeight = 390f;
            float startX = (screenW - panelWidth) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            float btnW = 250f;
            float btnH = 32f;
            float btnSpacing = 6f;
            float btnX = startX + (panelWidth - btnW) * 0.5f;

            // 1. Resume Game
            float btn1Y = startY + 48f;
            if (leftClicked && mx >= btnX && mx <= btnX + btnW && my >= btn1Y && my <= btn1Y + btnH)
            {
                CloseUI();
                return;
            }

            // 2. Save Game
            float btn2Y = btn1Y + btnH + btnSpacing;
            if (leftClicked && mx >= btnX && mx <= btnX + btnW && my >= btn2Y && my <= btn2Y + btnH)
            {
                OnSaveGameRequested?.Invoke();
                ShowSaveNotification();
                return;
            }

            // 3. Save & Main Menu
            float btn3Y = btn2Y + btnH + btnSpacing;
            if (leftClicked && mx >= btnX && mx <= btnX + btnW && my >= btn3Y && my <= btn3Y + btnH)
            {
                OnSaveGameRequested?.Invoke();
                CloseUI();
                OnReturnToMainMenuRequested?.Invoke();
                return;
            }

            // 4. Toggle Fly Mode
            float btn4Y = btn3Y + btnH + btnSpacing;
            if (leftClicked && mx >= btnX && mx <= btnX + btnW && my >= btn4Y && my <= btn4Y + btnH)
            {
                if (player != null)
                {
                    player.IsFlying = !player.IsFlying;
                    player.Velocity = Vector3.Zero;
                    OnSettingsChanged?.Invoke();
                }
                return;
            }

            // 5. Side-by-side toggles: Fullscreen and HUD
            float btn5Y = btn4Y + btnH + btnSpacing;
            float toggleGap = 8f;
            float toggleW = (btnW - toggleGap) * 0.5f;
            float toggle1X = btnX;
            float toggle2X = btnX + toggleW + toggleGap;

            if (leftClicked && mx >= toggle1X && mx <= toggle1X + toggleW && my >= btn5Y && my <= btn5Y + btnH)
            {
                OnToggleFullscreenRequested?.Invoke();
                OnSettingsChanged?.Invoke();
                return;
            }

            if (leftClicked && mx >= toggle2X && mx <= toggle2X + toggleW && my >= btn5Y && my <= btn5Y + btnH)
            {
                IsHudEnabled = !IsHudEnabled;
                OnSettingsChanged?.Invoke();
                return;
            }

            // 6. Render Distance Slider (2 to 16 chunks)
            float slider1Y = btn5Y + btnH + btnSpacing + 2f;
            float track1X = btnX;
            float track1Y = slider1Y + 16f;
            float track1W = btnW;
            float track1H = 9f;

            if (leftClicked && mx >= track1X - 8f && mx <= track1X + track1W + 8f && my >= slider1Y && my <= track1Y + track1H + 6f)
            {
                isDraggingRenderDistanceSlider = true;
            }

            if (!leftDown)
            {
                isDraggingRenderDistanceSlider = false;
            }

            if (isDraggingRenderDistanceSlider && leftDown)
            {
                float pct = Math.Clamp((mx - track1X) / track1W, 0f, 1f);
                int chunks = 2 + (int)MathF.Round(pct * 14f);
                if (chunks != renderDistanceChunks)
                {
                    SetRenderDistance(chunks);
                    OnSettingsChanged?.Invoke();
                }
            }

            // 7. FOV Slider (30 to 110 degrees)
            float slider2Y = track1Y + track1H + 8f;
            float track2X = btnX;
            float track2Y = slider2Y + 16f;
            float track2W = btnW;
            float track2H = 9f;

            if (leftClicked && mx >= track2X - 8f && mx <= track2X + track2W + 8f && my >= slider2Y && my <= track2Y + track2H + 6f)
            {
                isDraggingFovSlider = true;
            }

            if (!leftDown)
            {
                isDraggingFovSlider = false;
            }

            if (isDraggingFovSlider && leftDown)
            {
                float pct = Math.Clamp((mx - track2X) / track2W, 0f, 1f);
                int newFov = 30 + (int)MathF.Round(pct * 80f);
                if ((int)MathF.Round(FieldOfView) != newFov)
                {
                    FieldOfView = newFov;
                    OnSettingsChanged?.Invoke();
                }
            }

            // 8. Quit to Desktop
            float btn6Y = track2Y + track2H + 12f;
            if (leftClicked && mx >= btnX && mx <= btnX + btnW && my >= btn6Y && my <= btn6Y + btnH)
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

            if (leftClicked && CurrentState == UIState.CraftingTable)
            {
                // Tab: All
                if (mx >= leftPanelX + 14f && mx <= leftPanelX + 14f + tabW && my >= tabY && my <= tabY + tabH)
                {
                    activeCategory = RecipeCategory.All;
                    recipeScrollOffset = 0;
                }
                // Tab: Workbench
                else if (mx >= leftPanelX + 14f + tabW + 6f && mx <= leftPanelX + 14f + (tabW + 6f) * 2 && my >= tabY && my <= tabY + tabH)
                {
                    activeCategory = RecipeCategory.Crafting;
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

            int chestRows = currentChestInventory.SlotCount > 18 ? 4 : 2;
            float panelWidth = 430f;
            float panelHeight = (chestRows == 4) ? 500f : 420f;
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

            // 1. Chest Slots
            float chestStartY = startY + 40f;
            for (int row = 0; row < chestRows; row++)
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

                        if (currentChestInventory is DoubleChestInventory doubleChest)
                        {
                            doubleChest.SyncToChests();
                        }
                        return;
                    }
                }
            }

            // 2. Player Inventory (Main 27 + Hotbar 9)
            float playerInvStartY = chestStartY + chestRows * (slotSize + slotGap) + 36f;

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

                        if (currentChestInventory is DoubleChestInventory doubleChest)
                        {
                            doubleChest.SyncToChests();
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

                    if (currentChestInventory is DoubleChestInventory doubleChest)
                    {
                        doubleChest.SyncToChests();
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
            if (CurrentState == UIState.PersonalInventory)
            {
                // Only basic 2x2 survival recipes (no workbench required, no smelting)
                return RecipeRegistry.Recipes.Where(r => r.Station == RecipeStation.Personal).ToList();
            }
            else if (CurrentState == UIState.Furnace)
            {
                // Only furnace smelting recipes
                return RecipeRegistry.Recipes.Where(r => r.Station == RecipeStation.Furnace).ToList();
            }
            else if (CurrentState == UIState.CraftingTable)
            {
                // Workbench 3x3: All crafting recipes (Personal + CraftingTable), absolutely NO furnace smelting
                var workbenchRecipes = RecipeRegistry.Recipes.Where(r => r.Station != RecipeStation.Furnace);
                return activeCategory switch
                {
                    RecipeCategory.Crafting => workbenchRecipes.Where(r => r.Station == RecipeStation.CraftingTable).ToList(),
                    RecipeCategory.Smelting => new List<CraftingRecipe>(),
                    _ => workbenchRecipes.ToList()
                };
            }

            return RecipeRegistry.Recipes;
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
                    UpdateFpsCounter();
                    if (IsHudEnabled)
                    {
                        RenderHUD(screenWidth, screenHeight);
                    }
                    break;

                case UIState.PersonalInventory:
                case UIState.CraftingTable:
                case UIState.Furnace:
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

            // Render Sleeping / Night-skip screen if active
            RenderSleepingScreen(screenWidth, screenHeight);

            // Render Screenshot Toast notification if active
            RenderScreenshotToast(screenWidth, screenHeight);

            renderer.End();
        }

        private void RenderHUD(int screenW, int screenH)
        {
            if (player == null) return;

            // 1. Center Crosshair
            renderer.DrawCrosshair(screenW * 0.5f, screenH * 0.5f);

            // Coordinates & Direction Badge / F3 Debug Overlay (Top Left)
            RenderCoordinatesAndDebug(screenW, screenH);

            // In-game Clock Display (Top Right)
            float clockBottomY = 16f;
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

                clockBottomY = clockY + textSz.Y + 8f;
            }

            // In-game FPS Display (Just below Time)
            UpdateFpsCounter();
            float fpsScale = 0.95f;
            Vector2 fpsSz = renderer.MeasureText(cachedFpsString, fpsScale);
            float fpsX = screenW - fpsSz.X - 24f;
            float fpsY = clockBottomY + 4f;

            renderer.DrawPanel(fpsX - 8f, fpsY - 3f, fpsSz.X + 16f, fpsSz.Y + 6f,
                new Vector4(0.08f, 0.09f, 0.12f, 0.75f),
                new Vector4(0.28f, 0.32f, 0.40f, 0.65f), 1.0f);

            Vector4 fpsColor = cachedFpsValue >= 50
                ? new Vector4(0.40f, 0.95f, 0.45f, 0.95f) // Smooth green for good FPS
                : (cachedFpsValue >= 30
                    ? new Vector4(0.95f, 0.85f, 0.30f, 0.95f) // Yellow
                    : new Vector4(0.95f, 0.35f, 0.30f, 0.95f)); // Red for low FPS

            renderer.DrawText(cachedFpsString, fpsX, fpsY, fpsScale, fpsColor, shadow: true);

            // 2. Block Breaking Progress Bar (if breaking a block)
            float slotSize = 42f;
            float slotGap = 4f;
            float totalHotbarW = 9 * slotSize + 8 * slotGap;
            float pad = 6f;
            float hx = (screenW - totalHotbarW) * 0.5f;
            float hy = screenH - slotSize - 16f;

            // Status Sliders: Health and Oxygen (Always visible, side by side just above the hotbar)
            float sliderGap = 10f;
            float sliderW = (totalHotbarW - sliderGap) * 0.5f; // ~200px each
            float sliderH = 14f;
            float sliderY = hy - sliderH - 10f;

            if (player.BreakingProgress > 0.0f)
            {
                float barW = 140f;
                float barH = 10f;
                float barX = (screenW - barW) * 0.5f;
                float barY = sliderY - 40f;

                Vector4 barBg = new Vector4(0.1f, 0.11f, 0.14f, 0.85f);
                Vector4 barFill = new Vector4(0.95f, 0.45f, 0.15f, 0.95f); // Fiery orange breaking color
                renderer.DrawProgressBar(barX, barY, barW, barH, player.BreakingProgress, barFill, barBg);
                renderer.DrawText("BREAKING...", barX + 24f, barY - 14f, 1.0f, new Vector4(0.9f, 0.9f, 0.9f, 0.9f));
            }

            // 3. Hotbar at bottom center
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

            // 4. Status Sliders: Health and Oxygen (Always visible, side by side just above the hotbar)
            // --- Health Slider (Left) ---
            float hpPct = Math.Clamp(player.Health / player.MaxHealth, 0f, 1f);
            Vector4 hpBg = new Vector4(0.18f, 0.05f, 0.05f, 0.85f);
            Vector4 hpFill = (hpPct > 0.30f)
                ? new Vector4(0.90f, 0.18f, 0.20f, 0.95f) // Vibrant Red
                : new Vector4(0.98f, 0.10f, 0.10f, 0.98f); // Critical flashing red
            renderer.DrawProgressBar(hx, sliderY, sliderW, sliderH, hpPct, hpFill, hpBg);

            string hpText = $"HP {(int)MathF.Ceiling(player.Health)}/{(int)player.MaxHealth}";
            renderer.DrawText(hpText, hx + 6f, sliderY + 2f, 0.85f, new Vector4(1f, 0.95f, 0.95f, 1f), shadow: true);

            // --- Second Slider: Oxygen (Underwater) OR Sprint/Stamina (On Land) ---
            float secondBarX = hx + sliderW + sliderGap;
            if (player.IsUnderwater)
            {
                // Underwater: Oxygen Bar
                float oxPct = Math.Clamp(player.Oxygen / player.MaxOxygen, 0f, 1f);
                Vector4 oxBg = new Vector4(0.04f, 0.12f, 0.22f, 0.85f);
                Vector4 oxFill = new Vector4(0.15f, 0.65f, 0.95f, 0.95f); // Deep Cyan/Ocean Blue
                renderer.DrawProgressBar(secondBarX, sliderY, sliderW, sliderH, oxPct, oxFill, oxBg);

                string oxText = $"O2 {(int)MathF.Ceiling(player.Oxygen)}%";
                renderer.DrawText(oxText, secondBarX + 6f, sliderY + 2f, 0.85f, new Vector4(0.9f, 0.98f, 1f, 1f), shadow: true);
            }
            else
            {
                // On Land: Sprint / Stamina Bar (lasts 30s, recharges in 8s)
                float stPct = Math.Clamp(player.Stamina / player.MaxStamina, 0f, 1f);
                Vector4 stBg = new Vector4(0.15f, 0.12f, 0.05f, 0.85f);
                Vector4 stFill = (stPct > 0.20f)
                    ? new Vector4(0.95f, 0.72f, 0.15f, 0.95f) // Vibrant Golden Amber
                    : new Vector4(0.98f, 0.35f, 0.10f, 0.98f); // Warning orange-red
                renderer.DrawProgressBar(secondBarX, sliderY, sliderW, sliderH, stPct, stFill, stBg);

                string stText = $"SPRINT {(int)MathF.Ceiling(player.Stamina)}%";
                renderer.DrawText(stText, secondBarX + 6f, sliderY + 2f, 0.85f, new Vector4(1f, 0.96f, 0.88f, 1f), shadow: true);
            }

            // Display currently selected item name above status sliders
            var selStack = player.Inventory.Slots[player.SelectedHotbarSlot];
            if (!selStack.IsEmpty)
            {
                var def = BlockRegistry.Get(selStack.Type);
                string selText = $"{def.Name} ({selStack.Count})";
                Vector2 textSize = renderer.MeasureText(selText, 1.1f);
                float textX = (screenW - textSize.X) * 0.5f;
                renderer.DrawText(selText, textX, sliderY - 20f, 1.1f, new Vector4(1f, 1f, 1f, 0.9f), shadow: true);
            }
        }

        private void RenderCoordinatesAndDebug(int screenW, int screenH)
        {
            if (player == null) return;

            float posX = player.Position.X;
            float posY = player.Position.Y;
            float posZ = player.Position.Z;

            int blockX = (int)MathF.Floor(posX);
            int blockY = (int)MathF.Floor(posY);
            int blockZ = (int)MathF.Floor(posZ);

            int chunkX = (int)MathF.Floor(posX / Chunk.SIZE_X);
            int chunkZ = (int)MathF.Floor(posZ / Chunk.SIZE_Z);
            int subX = ((blockX % Chunk.SIZE_X) + Chunk.SIZE_X) % Chunk.SIZE_X;
            int subZ = ((blockZ % Chunk.SIZE_Z) + Chunk.SIZE_Z) % Chunk.SIZE_Z;

            Vector3 front = player.Camera?.Forward ?? -Vector3.UnitZ;
            string facing = "North";
            string axisDesc = "-Z";
            if (MathF.Abs(front.X) > MathF.Abs(front.Z))
            {
                if (front.X > 0) { facing = "East"; axisDesc = "+X"; }
                else { facing = "West"; axisDesc = "-X"; }
            }
            else
            {
                if (front.Z > 0) { facing = "South"; axisDesc = "+Z"; }
                else { facing = "North"; axisDesc = "-Z"; }
            }

            string biomeName = (world != null && World.World.IsInsideWorld(blockX, blockZ))
                ? world.GetBiome(blockX, blockZ).ToString()
                : "Plains";

            if (IsDebugCoordinatesEnabled)
            {
                // Full F3 Debug Screen (top-left)
                List<string> debugLines = new List<string>
                {
                    "VelmoraCraft Engine (v1.2.0)",
                    $"XYZ: {posX:F2} / {posY:F2} / {posZ:F2}",
                    $"Block: {blockX} {blockY} {blockZ} [Chunk {chunkX}, {chunkZ} in sub {subX}, {subZ}]",
                    $"Facing: {facing} ({axisDesc}) (Yaw: {player.Camera?.Yaw:F1}, Pitch: {player.Camera?.Pitch:F1})",
                    $"Biome: {biomeName}",
                    $"Day/Time: {(DayNightCycle != null ? DayNightCycle.GetTimeString() : "Day")} (Daytime: {DayNightCycle?.TimeOfDay:F1}s)",
                    $"Mode: {(player.IsFlying ? "Creative Flying" : "Survival Walking")} | HP: {(int)MathF.Ceiling(player.Health)}/{(int)player.MaxHealth}",
                    $"Render Distance: {renderDistanceChunks} chunks | FOV: {(int)FieldOfView}"
                };

                float debugX = 16f;
                float debugY = 16f;
                float lineHeight = 18f;
                float maxW = 0f;
                float textScale = 0.9f;

                foreach (var line in debugLines)
                {
                    Vector2 sz = renderer.MeasureText(line, textScale);
                    if (sz.X > maxW) maxW = sz.X;
                }

                float totalH = debugLines.Count * lineHeight + 8f;
                renderer.DrawPanel(debugX - 6f, debugY - 4f, maxW + 16f, totalH,
                    new Vector4(0.06f, 0.08f, 0.10f, 0.85f),
                    new Vector4(0.28f, 0.35f, 0.45f, 0.70f), 1.0f);

                for (int i = 0; i < debugLines.Count; i++)
                {
                    Vector4 col = (i == 0) ? new Vector4(0.95f, 0.85f, 0.35f, 1f) : new Vector4(0.92f, 0.94f, 0.96f, 0.95f);
                    renderer.DrawText(debugLines[i], debugX, debugY + i * lineHeight, textScale, col, shadow: true);
                }
            }
            else
            {
                // Sleek, minimal coordinates badge (top-left)
                string coordStr = $"XYZ: {blockX}, {blockY}, {blockZ}  |  {facing} ({axisDesc})  |  {biomeName}";
                float badgeScale = 0.95f;
                Vector2 badgeSz = renderer.MeasureText(coordStr, badgeScale);
                float badgeX = 16f;
                float badgeY = 16f;

                renderer.DrawPanel(badgeX - 8f, badgeY - 4f, badgeSz.X + 16f, badgeSz.Y + 8f,
                    new Vector4(0.08f, 0.09f, 0.12f, 0.75f),
                    new Vector4(0.28f, 0.32f, 0.40f, 0.65f), 1.0f);

                renderer.DrawText(coordStr, badgeX, badgeY, badgeScale, new Vector4(0.95f, 0.95f, 0.95f, 0.95f), shadow: true);
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

            string titleText = CurrentState switch
            {
                UIState.PersonalInventory => "PERSONAL CRAFTING (2x2)",
                UIState.CraftingTable => "CRAFTING TABLE (3x3)",
                UIState.Furnace => "FURNACE SMELTING",
                _ => "CRAFTING & RECIPES"
            };
            renderer.DrawText(titleText, leftPanelX + 14f, startY + 12f, 1.3f, new Vector4(0.95f, 0.95f, 1.0f, 1.0f));

            // Category Tabs or Subtitle
            float tabY = startY + 36f;
            float tabH = 22f;
            float tabW = 86f;

            if (CurrentState == UIState.CraftingTable)
            {
                RenderTab("All", leftPanelX + 14f, tabY, tabW, tabH, activeCategory == RecipeCategory.All, mousePos);
                RenderTab("Workbench", leftPanelX + 14f + tabW + 6f, tabY, tabW + 14f, tabH, activeCategory == RecipeCategory.Crafting, mousePos);
            }
            else if (CurrentState == UIState.Furnace)
            {
                renderer.DrawText("Requires 1 Coal fuel per smelted item.", leftPanelX + 14f, tabY + 4f, 0.9f, new Vector4(1.0f, 0.70f, 0.30f, 1.0f));
            }
            else if (CurrentState == UIState.PersonalInventory)
            {
                renderer.DrawText("Basic 2x2. Right-click Crafting Table for workbench.", leftPanelX + 14f, tabY + 4f, 0.85f, new Vector4(0.65f, 0.70f, 0.80f, 0.85f));
            }

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

            int chestRows = currentChestInventory.SlotCount > 18 ? 4 : 2;
            string titleText = (chestRows == 4) ? "LARGE CHEST STORAGE" : "CHEST STORAGE";
            float panelWidth = 430f;
            float panelHeight = (chestRows == 4) ? 500f : 420f;
            float startX = (screenW - panelWidth) * 0.5f;
            float startY = (screenH - panelHeight) * 0.5f;

            Vector4 panelBg = new Vector4(0.12f, 0.13f, 0.16f, 0.94f);
            Vector4 panelBorder = new Vector4(0.28f, 0.32f, 0.40f, 0.85f);

            renderer.DrawPanel(startX, startY, panelWidth, panelHeight, panelBg, panelBorder, 1.5f);
            renderer.DrawText(titleText, startX + 16f, startY + 14f, 1.3f, new Vector4(0.95f, 0.95f, 1.0f, 1.0f));

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

            // 1. Chest Slots
            float chestStartY = startY + 40f;
            for (int row = 0; row < chestRows; row++)
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
            float divY = chestStartY + chestRows * (slotSize + slotGap) + 16f;
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
            float panelHeight = 390f;
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
                renderer.DrawQuad(startX + 20f, startY + 40f, panelWidth - 40f, 1.5f, new Vector4(0.30f, 0.35f, 0.45f, 0.6f));
            }

            float btnW = 250f;
            float btnH = 32f;
            float btnSpacing = 6f;
            float btnX = startX + (panelWidth - btnW) * 0.5f;

            // Button 1: Resume Game
            float btn1Y = startY + 48f;
            RenderMenuButton("RESUME GAME", btnX, btn1Y, btnW, btnH, mousePos);

            // Button 2: Save Game
            float btn2Y = btn1Y + btnH + btnSpacing;
            RenderMenuButton("SAVE GAME", btnX, btn2Y, btnW, btnH, mousePos);

            // Button 3: Save & Main Menu
            float btn3Y = btn2Y + btnH + btnSpacing;
            RenderMenuButton("SAVE & MAIN MENU", btnX, btn3Y, btnW, btnH, mousePos);

            // Button 4: Toggle Fly Mode
            float btn4Y = btn3Y + btnH + btnSpacing;
            string flyText = (player?.IsFlying ?? false) ? "FLIGHT: ENABLED" : "FLIGHT: DISABLED";
            RenderMenuButton(flyText, btnX, btn4Y, btnW, btnH, mousePos);

            // Row 5: 2 Toggles side by side (Fullscreen & HUD)
            float btn5Y = btn4Y + btnH + btnSpacing;
            float toggleGap = 8f;
            float toggleW = (btnW - toggleGap) * 0.5f;
            float toggle1X = btnX;
            float toggle2X = btnX + toggleW + toggleGap;

            string fsText = IsFullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF";
            string hudText = IsHudEnabled ? "HUD: ON" : "HUD: OFF";

            RenderMenuButton(fsText, toggle1X, btn5Y, toggleW, btnH, mousePos, scale: 0.82f);
            RenderMenuButton(hudText, toggle2X, btn5Y, toggleW, btnH, mousePos, scale: 0.82f);

            // Section 6: Render Distance Slider (2 to 16 chunks)
            float slider1Y = btn5Y + btnH + btnSpacing + 2f;
            int curDist = world?.ViewDistanceChunks ?? renderDistanceChunks;
            string distText = $"RENDER DISTANCE: {curDist} CHUNKS";
            Vector2 distSz = renderer.MeasureText(distText, 0.90f);
            renderer.DrawText(distText, btnX + (btnW - distSz.X) * 0.5f, slider1Y, 0.90f, new Vector4(0.85f, 0.90f, 1.0f, 0.95f), shadow: true);

            float track1X = btnX;
            float track1Y = slider1Y + 16f;
            float track1W = btnW;
            float track1H = 9f;

            // Slider background track
            renderer.DrawPanel(track1X, track1Y, track1W, track1H, new Vector4(0.06f, 0.08f, 0.12f, 0.95f), new Vector4(0.30f, 0.35f, 0.45f, 0.8f), 1.0f);

            // Slider fill
            float t = Math.Clamp((curDist - 2f) / 14f, 0f, 1f);
            float fillW = t * track1W;
            if (fillW > 0f)
            {
                renderer.DrawQuad(track1X + 1f, track1Y + 1f, fillW - 2f, track1H - 2f, new Vector4(0.25f, 0.65f, 1.0f, 0.85f));
            }

            // Slider handle / knob
            float knobW = 16f;
            float knobH = 18f;
            float knobX = track1X + t * track1W - knobW * 0.5f;
            float knobY = track1Y + (track1H - knobH) * 0.5f;

            bool isKnobHovered = (mousePos.X >= knobX && mousePos.X <= knobX + knobW && mousePos.Y >= knobY && mousePos.Y <= knobY + knobH) || isDraggingRenderDistanceSlider;
            Vector4 knobBg = isKnobHovered ? new Vector4(0.95f, 0.98f, 1.0f, 1.0f) : new Vector4(0.75f, 0.85f, 0.98f, 0.95f);
            Vector4 knobBorder = isKnobHovered ? new Vector4(0.30f, 0.80f, 1.0f, 1.0f) : new Vector4(0.35f, 0.45f, 0.60f, 0.9f);
            renderer.DrawPanel(knobX, knobY, knobW, knobH, knobBg, knobBorder, 1.5f);

            // Section 7: FOV Slider (30 to 110 degrees)
            float slider2Y = track1Y + track1H + 8f;
            int curFov = (int)MathF.Round(player?.Camera?.BaseFov ?? 70f);
            string fovText = curFov == 70 ? "FOV: 70 (NORMAL)" : (curFov == 110 ? "FOV: 110 (QUAKE PRO)" : $"FOV: {curFov}");
            Vector2 fovSz = renderer.MeasureText(fovText, 0.90f);
            renderer.DrawText(fovText, btnX + (btnW - fovSz.X) * 0.5f, slider2Y, 0.90f, new Vector4(0.85f, 0.90f, 1.0f, 0.95f), shadow: true);

            float track2X = btnX;
            float track2Y = slider2Y + 16f;
            float track2W = btnW;
            float track2H = 9f;

            // FOV Slider background track
            renderer.DrawPanel(track2X, track2Y, track2W, track2H, new Vector4(0.06f, 0.08f, 0.12f, 0.95f), new Vector4(0.30f, 0.35f, 0.45f, 0.8f), 1.0f);

            // FOV Slider fill
            float fovT = Math.Clamp((curFov - 30f) / 80f, 0f, 1f);
            float fovFillW = fovT * track2W;
            if (fovFillW > 0f)
            {
                renderer.DrawQuad(track2X + 1f, track2Y + 1f, fovFillW - 2f, track2H - 2f, new Vector4(0.25f, 0.65f, 1.0f, 0.85f));
            }

            // FOV Slider handle / knob
            float fovKnobW = 16f;
            float fovKnobH = 18f;
            float fovKnobX = track2X + fovT * track2W - fovKnobW * 0.5f;
            float fovKnobY = track2Y + (track2H - fovKnobH) * 0.5f;

            bool isFovKnobHovered = (mousePos.X >= fovKnobX && mousePos.X <= fovKnobX + fovKnobW && mousePos.Y >= fovKnobY && mousePos.Y <= fovKnobY + fovKnobH) || isDraggingFovSlider;
            Vector4 fovKnobBg = isFovKnobHovered ? new Vector4(0.95f, 0.98f, 1.0f, 1.0f) : new Vector4(0.75f, 0.85f, 0.98f, 0.95f);
            Vector4 fovKnobBorder = isFovKnobHovered ? new Vector4(0.30f, 0.80f, 1.0f, 1.0f) : new Vector4(0.35f, 0.45f, 0.60f, 0.9f);
            renderer.DrawPanel(fovKnobX, fovKnobY, fovKnobW, fovKnobH, fovKnobBg, fovKnobBorder, 1.5f);

            // Button 8: Quit to Desktop
            float btn6Y = track2Y + track2H + 12f;
            RenderMenuButton("QUIT TO DESKTOP", btnX, btn6Y, btnW, btnH, mousePos, isDestructive: true);
        }

        private void RenderMenuButton(string text, float x, float y, float w, float h, Vector2 mousePos, bool isDestructive = false, float scale = 1.1f)
        {
            bool isHovered = mousePos.X >= x && mousePos.X <= x + w && mousePos.Y >= y && mousePos.Y <= y + h;

            Vector4 bg = isHovered
                ? (isDestructive ? new Vector4(0.35f, 0.15f, 0.18f, 0.95f) : new Vector4(0.20f, 0.25f, 0.35f, 0.95f))
                : new Vector4(0.14f, 0.15f, 0.19f, 0.85f);

            Vector4 border = isHovered
                ? (isDestructive ? new Vector4(0.9f, 0.3f, 0.35f, 0.95f) : new Vector4(0.45f, 0.70f, 1.0f, 0.95f))
                : new Vector4(0.28f, 0.32f, 0.40f, 0.70f);

            renderer.DrawPanel(x, y, w, h, bg, border, 1.5f);

            Vector2 textSz = renderer.MeasureText(text, scale);
            float tx = x + (w - textSz.X) * 0.5f;
            float ty = y + (h - textSz.Y) * 0.5f;
            Vector4 textColor = isHovered ? Vector4.One : new Vector4(0.85f, 0.88f, 0.95f, 0.9f);
            renderer.DrawText(text, tx, ty, scale, textColor, shadow: true);
        }

        private void UpdateFpsCounter()
        {
            fpsFrameCounter++;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();

            if (lastFpsTimestamp == 0)
            {
                lastFpsTimestamp = now;
                return;
            }

            double elapsed = (double)(now - lastFpsTimestamp) / System.Diagnostics.Stopwatch.Frequency;
            // Update FPS readout every ~0.33 seconds (3 times per second) for smooth readability and zero GC/lag
            if (elapsed >= 0.33)
            {
                if (elapsed <= 1.0)
                {
                    int newFps = (int)Math.Round(fpsFrameCounter / elapsed);
                    if (newFps != cachedFpsValue)
                    {
                        cachedFpsValue = newFps;
                        cachedFpsString = $"FPS: {cachedFpsValue}";
                    }
                }
                fpsFrameCounter = 0;
                lastFpsTimestamp = now;
            }
        }

        private void RenderScreenshotToast(int screenW, int screenH)
        {
            if (screenshotNotificationStartTime == 0) return;

            double elapsed = (double)(System.Diagnostics.Stopwatch.GetTimestamp() - screenshotNotificationStartTime) / System.Diagnostics.Stopwatch.Frequency;
            if (elapsed >= 3.5)
            {
                screenshotNotificationStartTime = 0;
                return;
            }

            float alpha = 1.0f;
            if (elapsed > 2.5)
            {
                alpha = (float)((3.5 - elapsed) / 1.0);
            }

            float textScale = 0.95f;
            Vector2 textSz = renderer.MeasureText(screenshotNotificationMessage, textScale);
            float padX = 10f;
            float padY = 6f;
            float toastW = textSz.X + padX * 2f;
            float toastH = textSz.Y + padY * 2f;

            float toastX = 16f;
            float toastY = screenH - toastH - 16f;

            // Semi-transparent dark panel with subtle Minecraft-style green accent
            Vector4 bg = new Vector4(0.08f, 0.09f, 0.12f, 0.85f * alpha);
            Vector4 border = new Vector4(0.35f, 0.80f, 0.45f, 0.90f * alpha);
            renderer.DrawPanel(toastX, toastY, toastW, toastH, bg, border, 1.2f);

            // Toast message text
            Vector4 textColor = new Vector4(0.95f, 0.98f, 0.95f, alpha);
            renderer.DrawText(screenshotNotificationMessage, toastX + padX, toastY + padY, textScale, textColor, shadow: true);
        }

        private void RenderSleepingScreen(int screenW, int screenH)
        {
            if (sleepStartTime == 0) return;

            double elapsed = (double)(System.Diagnostics.Stopwatch.GetTimestamp() - sleepStartTime) / System.Diagnostics.Stopwatch.Frequency;
            if (elapsed >= SLEEP_TOTAL_SECONDS)
            {
                sleepStartTime = 0;
                return;
            }

            // At midpoint (0.75s), skip night!
            if (elapsed >= 0.75 && !hasSkippedNightThisSleep)
            {
                hasSkippedNightThisSleep = true;
                if (DayNightCycle != null)
                {
                    DayNightCycle.TimeOfDay = DayNightCycle.DAWN_DURATION;
                    DayNightCycle.DaysSurvived++;
                    DayNightCycle.Update(0f);
                }
                if (player != null)
                {
                    player.Health = player.MaxHealth;
                    player.Stamina = player.MaxStamina;
                    player.FallDistance = 0f;
                }
            }

            float progress = (float)(elapsed / SLEEP_TOTAL_SECONDS); // 0.0 to 1.0
            float overlayAlpha;
            if (progress < 0.40f)
            {
                overlayAlpha = (progress / 0.40f) * 0.95f;
            }
            else if (progress < 0.65f)
            {
                overlayAlpha = 0.95f;
            }
            else
            {
                overlayAlpha = (1.0f - (progress - 0.65f) / 0.35f) * 0.95f;
            }

            // Draw full-screen dark sleeping backdrop
            renderer.DrawQuad(0, 0, screenW, screenH, new Vector4(0.04f, 0.05f, 0.08f, overlayAlpha));

            float centerX = screenW * 0.5f;
            float centerY = screenH * 0.5f;
            string sleepingText = progress < 0.55f ? "Sleeping..." : "Waking up...";
            string subText = "Passing the night...";

            float textAlpha = Math.Clamp(overlayAlpha / 0.95f, 0f, 1f);
            Vector2 sz1 = renderer.MeasureText(sleepingText, 1.6f);
            Vector2 sz2 = renderer.MeasureText(subText, 1.05f);

            renderer.DrawText(sleepingText, centerX - sz1.X * 0.5f, centerY - 32f, 1.6f, new Vector4(1f, 1f, 1f, textAlpha), shadow: true);
            renderer.DrawText(subText, centerX - sz2.X * 0.5f, centerY + 8f, 1.05f, new Vector4(0.75f, 0.85f, 0.95f, textAlpha * 0.9f), shadow: true);

            // Progress bar
            float barW = 200f;
            float barH = 6f;
            float barX = centerX - barW * 0.5f;
            float barY = centerY + 42f;
            renderer.DrawQuad(barX, barY, barW, barH, new Vector4(0.2f, 0.2f, 0.25f, textAlpha * 0.6f));
            renderer.DrawQuad(barX, barY, barW * progress, barH, new Vector4(0.35f, 0.75f, 1.0f, textAlpha * 0.95f));
        }
    }
}
