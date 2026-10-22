using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.UI;

public enum ModalField
{
    Seed,
    WorldSize
}

public class MainMenu
{
    private readonly UIRenderer renderer;

    private readonly string[] splashes = new[]
    {
        "Infinite Possibilities!",
        "A Minecraft Alpha Clone!",
        "Powered by C# & OpenTK!",
        "OpenGL 3.3 Core!",
        "Now with Working Beds!",
        "Swaying Foliage & Grass!",
        "Farming & Harvesting!",
        "Rideable Minecarts!",
        "Watch out for Creepers!",
        "Don't dig straight down!",
        "Dynamic Day & Night!",
        "Procedural Voxel Worlds!",
        "Punching Trees!",
        "Zero Startup Freeze!",
        "Sleeping skips the night!",
        "Multi-threaded Chunks!",
        "Alpha 1.2.6 Nostalgia!",
        "100% Pure Voxels!",
        "Fast Simplex Noise!",
        "Beware of the Dark!",
        "Water Buoyancy & Swimming!",
        "Crafting & Smelting!",
        "Press Ctrl to Sprint!",
        "Press F2 for Screenshots!",
        "Also try Classic!",
        "Block by block!",
        "Sunrise over the mountains!",
        "Caves and Cliffs!",
        "Torches light the way!",
        "Double Chests supported!"
    };

    private ModalField activeField = ModalField.Seed;
    private bool backspaceWasDown;
    private int currentSplashIndex;

    private float cursorBlinkTimer;
    private bool cursorVisible = true;
    private bool enterWasDown;
    private bool escWasDown;

    private bool hasActiveWorld;

    private bool leftMouseWasDown;

    private string seedInput = "1337";
    private float splashTimer;
    private bool tabWasDown;
    private string worldSizeInput = "64";

    public MainMenu(UIRenderer renderer)
    {
        this.renderer = renderer;
        RandomizeSplash();
    }

    public bool HasActiveWorld
    {
        get => hasActiveWorld || SaveSystem.HasSave();
        set => hasActiveWorld = value;
    }

    public bool IsSeedModalOpen { get; set; }

    public Action<int, int>? OnPlayNewGame { get; set; }
    public Action? OnContinue { get; set; }
    public Action? OnExit { get; set; }

    public void RandomizeSplash()
    {
        currentSplashIndex = Random.Shared.Next(splashes.Length);
    }

    public void HandleTextInput(char c)
    {
        if (!IsSeedModalOpen) return;

        if (activeField == ModalField.Seed)
        {
            // Accept alphanumeric characters, minus, and underscore
            if ((char.IsLetterOrDigit(c) || c == '-' || c == '_') && seedInput.Length < 16) seedInput += c;
        }
        else if (activeField == ModalField.WorldSize)
        {
            // Accept only digits for chunk count
            if (char.IsDigit(c) && worldSizeInput.Length < 4) worldSizeInput += c;
        }
    }

    public void Update(KeyboardState keyboard, MouseState mouse, Vector2 mousePos, float deltaTime, int screenW,
        int screenH)
    {
        splashTimer += deltaTime;
        cursorBlinkTimer += deltaTime;
        if (cursorBlinkTimer > 0.5f)
        {
            cursorBlinkTimer = 0f;
            cursorVisible = !cursorVisible;
        }

        var leftDown = mouse.IsButtonDown(MouseButton.Left);
        var leftClicked = leftDown && !leftMouseWasDown;
        leftMouseWasDown = leftDown;

        var backspaceDown = keyboard.IsKeyDown(Keys.Backspace);
        var backspaceClicked = backspaceDown && !backspaceWasDown;
        backspaceWasDown = backspaceDown;

        var enterDown = keyboard.IsKeyDown(Keys.Enter) || keyboard.IsKeyDown(Keys.KeyPadEnter);
        var enterClicked = enterDown && !enterWasDown;
        enterWasDown = enterDown;

        var escDown = keyboard.IsKeyDown(Keys.Escape);
        var escClicked = escDown && !escWasDown;
        escWasDown = escDown;

        var tabDown = keyboard.IsKeyDown(Keys.Tab);
        var tabClicked = tabDown && !tabWasDown;
        tabWasDown = tabDown;

        if (IsSeedModalOpen)
        {
            // Tab toggles between Seed and WorldSize fields
            if (tabClicked)
            {
                activeField = activeField == ModalField.Seed ? ModalField.WorldSize : ModalField.Seed;
                cursorBlinkTimer = 0f;
                cursorVisible = true;
            }

            // Backspace in active input field
            if (backspaceClicked)
            {
                if (activeField == ModalField.Seed && seedInput.Length > 0)
                    seedInput = seedInput.Substring(0, seedInput.Length - 1);
                else if (activeField == ModalField.WorldSize && worldSizeInput.Length > 0)
                    worldSizeInput = worldSizeInput.Substring(0, worldSizeInput.Length - 1);
            }

            // Enter confirms and creates world
            if (enterClicked)
            {
                StartGameWithCurrentSeedAndSize();
                return;
            }

            // Escape cancels modal
            if (escClicked)
            {
                IsSeedModalOpen = false;
                return;
            }

            if (leftClicked) HandleSeedModalClicks(mousePos.X, mousePos.Y, screenW, screenH);
        }
        else
        {
            if (leftClicked) HandleMainMenuClicks(mousePos.X, mousePos.Y, screenW, screenH);
        }
    }

    private void HandleMainMenuClicks(float mx, float my, int screenW, int screenH)
    {
        var btnW = 320f;
        var btnH = 44f;
        var btnX = (screenW - btnW) * 0.5f;

        var startY = screenH * 0.46f;
        var btn1Y = startY;
        if (mx >= btnX && mx <= btnX + btnW && my >= btn1Y && my <= btn1Y + btnH)
        {
            IsSeedModalOpen = true;
            activeField = ModalField.Seed;
            return;
        }

        var btn2Y = btn1Y + btnH + 16f;
        if (mx >= btnX && mx <= btnX + btnW && my >= btn2Y && my <= btn2Y + btnH)
        {
            if (HasActiveWorld) OnContinue?.Invoke();
            return;
        }

        var btn3Y = btn2Y + btnH + 16f;
        if (mx >= btnX && mx <= btnX + btnW && my >= btn3Y && my <= btn3Y + btnH) OnExit?.Invoke();
    }

    private void HandleSeedModalClicks(float mx, float my, int screenW, int screenH)
    {
        var modalW = 500f;
        var modalH = 410f;
        var modalX = (screenW - modalW) * 0.5f;
        var modalY = (screenH - modalH) * 0.5f;
        var seedX = modalX + 35f;
        var seedY = modalY + 105f;
        var seedW = 265f;
        var seedH = 36f;
        if (mx >= seedX && mx <= seedX + seedW && my >= seedY && my <= seedY + seedH)
        {
            activeField = ModalField.Seed;
            cursorBlinkTimer = 0f;
            cursorVisible = true;
            return;
        }

        var randBtnW = 140f;
        var randBtnH = 36f;
        var randBtnX = modalX + modalW - randBtnW - 35f;
        var randBtnY = seedY;
        if (mx >= randBtnX && mx <= randBtnX + randBtnW && my >= randBtnY && my <= randBtnY + randBtnH)
        {
            seedInput = Random.Shared.Next(1000, 999999).ToString();
            activeField = ModalField.Seed;
            return;
        }

        var sizeX = modalX + 35f;
        var sizeY = modalY + 175f;
        var sizeW = 140f;
        var sizeH = 36f;
        if (mx >= sizeX && mx <= sizeX + sizeW && my >= sizeY && my <= sizeY + sizeH)
        {
            activeField = ModalField.WorldSize;
            cursorBlinkTimer = 0f;
            cursorVisible = true;
            return;
        }

        var preBtnW = 65f;
        var preBtnH = 36f;
        var preBtn32X = sizeX + sizeW + 20f;
        var preBtn64X = preBtn32X + preBtnW + 12f;
        var preBtn256X = preBtn64X + preBtnW + 12f;

        if (my >= sizeY && my <= sizeY + preBtnH)
        {
            if (mx >= preBtn32X && mx <= preBtn32X + preBtnW)
            {
                worldSizeInput = "32";
                activeField = ModalField.WorldSize;
                return;
            }

            if (mx >= preBtn64X && mx <= preBtn64X + preBtnW)
            {
                worldSizeInput = "64";
                activeField = ModalField.WorldSize;
                return;
            }

            if (mx >= preBtn256X && mx <= preBtn256X + preBtnW)
            {
                worldSizeInput = "256";
                activeField = ModalField.WorldSize;
                return;
            }
        }

        var actBtnW = 195f;
        var actBtnH = 42f;
        var actBtnY = modalY + modalH - actBtnH - 24f;

        // "CREATE WORLD" button
        var createBtnX = modalX + 35f;
        if (mx >= createBtnX && mx <= createBtnX + actBtnW && my >= actBtnY && my <= actBtnY + actBtnH)
        {
            StartGameWithCurrentSeedAndSize();
            return;
        }

        // "CANCEL" button
        var cancelBtnX = modalX + modalW - actBtnW - 35f;
        if (mx >= cancelBtnX && mx <= cancelBtnX + actBtnW && my >= actBtnY && my <= actBtnY + actBtnH)
            IsSeedModalOpen = false;
    }

    private void StartGameWithCurrentSeedAndSize()
    {
        int seed;
        if (!int.TryParse(seedInput, out seed))
            seed = string.IsNullOrEmpty(seedInput) ? 1337 : Math.Abs(seedInput.GetHashCode());

        int worldSize;
        if (!int.TryParse(worldSizeInput, out worldSize)) worldSize = 64;
        worldSize = Math.Clamp(worldSize, 8, 256);

        IsSeedModalOpen = false;
        OnPlayNewGame?.Invoke(seed, worldSize);
    }

    public void Render(int screenW, int screenH, Vector2 mousePos)
    {
        renderer.Begin(screenW, screenH);

        // Title Banner: "VELMORACRAFT"
        RenderTitleLogo(screenW, screenH);

        // Main Menu Buttons
        RenderMenuButtons(screenW, screenH, mousePos);

        // Seed & World Setup Pop-up Modal if open
        if (IsSeedModalOpen) RenderSeedModal(screenW, screenH, mousePos);

        renderer.End();
    }

    private void RenderTitleLogo(int screenW, int screenH)
    {
        var title = "VELMORACRAFT";
        var scale = 3.6f;
        var titleSz = renderer.MeasureText(title, scale);
        var tx = (screenW - titleSz.X) * 0.5f;
        var ty = screenH * 0.16f;

        // Deep 3D drop shadow
        renderer.DrawText(title, tx + 4f, ty + 4f, scale, new Vector4(0.02f, 0.03f, 0.05f, 0.95f), false);
        renderer.DrawText(title, tx + 2f, ty + 2f, scale, new Vector4(0.15f, 0.12f, 0.08f, 0.90f), false);

        // Main golden blocky title
        renderer.DrawText(title, tx, ty, scale, new Vector4(1.0f, 0.82f, 0.28f, 1.0f), false);

        // Subtitle
        var sub = "A minecraft alpha clone in OpenTK (OpenGL C#)";
        var subScale = 1.15f;
        var subSz = renderer.MeasureText(sub, subScale);
        renderer.DrawText(sub, (screenW - subSz.X) * 0.5f, ty + titleSz.Y + 12f, subScale,
            new Vector4(0.9f, 0.92f, 0.98f, 0.9f));

        // Minecraft-style yellow splash text with sine wave pulse
        var splash = splashes[currentSplashIndex];
        var pulse = 1.0f + 0.10f * MathF.Sin(splashTimer * 4.5f);
        var splashScale = 1.2f * pulse;
        var splashSz = renderer.MeasureText(splash, splashScale);

        var splashX = tx + titleSz.X - 30f;
        var splashY = ty + titleSz.Y - 6f;

        renderer.DrawText(splash, splashX + 2f, splashY + 2f, splashScale, new Vector4(0.1f, 0.1f, 0.02f, 0.9f), false);
        renderer.DrawText(splash, splashX, splashY, splashScale, new Vector4(1.0f, 1.0f, 0.2f, 1.0f), false);
    }

    private void RenderMenuButtons(int screenW, int screenH, Vector2 mousePos)
    {
        var btnW = 320f;
        var btnH = 44f;
        var btnX = (screenW - btnW) * 0.5f;

        var startY = screenH * 0.46f;
        var btn1Y = startY;
        RenderButton("PLAY (NEW GAME)", btnX, btn1Y, btnW, btnH, mousePos, isPrimary: true);
        var btn2Y = btn1Y + btnH + 16f;
        RenderButton("CONTINUE", btnX, btn2Y, btnW, btnH, mousePos, HasActiveWorld);
        var btn3Y = btn2Y + btnH + 16f;
        RenderButton("EXIT", btnX, btn3Y, btnW, btnH, mousePos, isDestructive: true);
    }

    private void RenderButton(string text, float x, float y, float w, float h, Vector2 mousePos, bool isEnabled = true,
        bool isPrimary = false, bool isDestructive = false)
    {
        var isHovered = isEnabled && mousePos.X >= x && mousePos.X <= x + w && mousePos.Y >= y && mousePos.Y <= y + h;

        Vector4 bg;
        Vector4 border;

        if (!isEnabled)
        {
            bg = new Vector4(0.08f, 0.09f, 0.12f, 0.55f);
            border = new Vector4(0.20f, 0.22f, 0.28f, 0.40f);
        }
        else if (isHovered)
        {
            if (isDestructive)
            {
                bg = new Vector4(0.38f, 0.15f, 0.18f, 0.95f);
                border = new Vector4(1.0f, 0.35f, 0.40f, 0.95f);
            }
            else if (isPrimary)
            {
                bg = new Vector4(0.18f, 0.32f, 0.46f, 0.95f);
                border = new Vector4(0.40f, 0.80f, 1.0f, 0.98f);
            }
            else
            {
                bg = new Vector4(0.22f, 0.26f, 0.34f, 0.95f);
                border = new Vector4(0.60f, 0.75f, 0.95f, 0.90f);
            }
        }
        else
        {
            bg = new Vector4(0.12f, 0.14f, 0.18f, 0.85f);
            border = isPrimary ? new Vector4(0.32f, 0.55f, 0.85f, 0.75f) : new Vector4(0.26f, 0.29f, 0.38f, 0.65f);
        }

        renderer.DrawPanel(x, y, w, h, bg, border, isHovered ? 2.0f : 1.5f);

        var textSz = renderer.MeasureText(text, 1.25f);
        var tx = x + (w - textSz.X) * 0.5f;
        var ty = y + (h - textSz.Y) * 0.5f;

        Vector4 textColor;
        if (!isEnabled)
            textColor = new Vector4(0.45f, 0.48f, 0.55f, 0.6f);
        else if (isHovered)
            textColor = Vector4.One;
        else
            textColor = new Vector4(0.90f, 0.92f, 0.98f, 0.95f);

        renderer.DrawText(text, tx, ty, 1.25f, textColor);
    }

    private void RenderSeedModal(int screenW, int screenH, Vector2 mousePos)
    {
        // Darkened backdrop over full screen
        renderer.DrawQuad(0, 0, screenW, screenH, new Vector4(0.01f, 0.02f, 0.04f, 0.75f));

        var modalW = 500f;
        var modalH = 410f;
        var modalX = (screenW - modalW) * 0.5f;
        var modalY = (screenH - modalH) * 0.5f;

        var modalBg = new Vector4(0.10f, 0.12f, 0.16f, 0.98f);
        var modalBorder = new Vector4(0.35f, 0.60f, 0.90f, 0.90f);

        renderer.DrawPanel(modalX, modalY, modalW, modalH, modalBg, modalBorder, 2.0f);

        // Modal Header
        var title = "NEW WORLD SETUP";
        var titleSz = renderer.MeasureText(title, 1.5f);
        renderer.DrawText(title, modalX + (modalW - titleSz.X) * 0.5f, modalY + 18f, 1.5f,
            new Vector4(1.0f, 0.85f, 0.35f, 1.0f));

        // Warning / Info Note
        var note = "Starting a new game will overwrite the current world.";
        var noteSz = renderer.MeasureText(note, 0.95f);
        renderer.DrawText(note, modalX + (modalW - noteSz.X) * 0.5f, modalY + 46f, 0.95f,
            new Vector4(0.85f, 0.60f, 0.60f, 0.9f));

        // Divider
        renderer.DrawQuad(modalX + 25f, modalY + 68f, modalW - 50f, 1.5f, new Vector4(0.25f, 0.30f, 0.40f, 0.7f));
        // ROW 1: World Seed
        var seedLabel = "Enter World Seed:";
        renderer.DrawText(seedLabel, modalX + 35f, modalY + 82f, 1.15f, new Vector4(0.9f, 0.92f, 0.98f, 0.95f));

        var seedBoxW = 265f;
        var seedBoxH = 36f;
        var seedBoxX = modalX + 35f;
        var seedBoxY = modalY + 105f;

        var isSeedFocused = activeField == ModalField.Seed;
        var seedBoxBorder =
            isSeedFocused ? new Vector4(0.40f, 0.85f, 1.0f, 0.98f) : new Vector4(0.28f, 0.32f, 0.42f, 0.75f);
        renderer.DrawPanel(seedBoxX, seedBoxY, seedBoxW, seedBoxH, new Vector4(0.05f, 0.06f, 0.08f, 0.95f),
            seedBoxBorder, isSeedFocused ? 2.0f : 1.5f);

        var displaySeed = seedInput + (isSeedFocused && cursorVisible ? "|" : "");
        renderer.DrawText(displaySeed, seedBoxX + 10f, seedBoxY + 10f, 1.25f, Vector4.One, false);

        // Randomize button
        var randBtnW = 140f;
        var randBtnH = 36f;
        var randBtnX = modalX + modalW - randBtnW - 35f;
        var randBtnY = seedBoxY;
        RenderButton("RANDOM", randBtnX, randBtnY, randBtnW, randBtnH, mousePos);
        // ROW 2: World Size (below the seed)
        var sizeLabel = "World Size (in chunks):";
        renderer.DrawText(sizeLabel, modalX + 35f, modalY + 155f, 1.15f, new Vector4(0.9f, 0.92f, 0.98f, 0.95f));

        var sizeBoxW = 140f;
        var sizeBoxH = 36f;
        var sizeBoxX = modalX + 35f;
        var sizeBoxY = modalY + 178f;

        var isSizeFocused = activeField == ModalField.WorldSize;
        var sizeBoxBorder =
            isSizeFocused ? new Vector4(0.40f, 0.85f, 1.0f, 0.98f) : new Vector4(0.28f, 0.32f, 0.42f, 0.75f);
        renderer.DrawPanel(sizeBoxX, sizeBoxY, sizeBoxW, sizeBoxH, new Vector4(0.05f, 0.06f, 0.08f, 0.95f),
            sizeBoxBorder, isSizeFocused ? 2.0f : 1.5f);

        var displaySize = worldSizeInput + (isSizeFocused && cursorVisible ? "|" : "");
        renderer.DrawText(displaySize, sizeBoxX + 10f, sizeBoxY + 10f, 1.25f, Vector4.One, false);

        // Quick preset buttons: 32, 64, 256
        var preBtnW = 65f;
        var preBtnH = 36f;
        var preBtn32X = sizeBoxX + sizeBoxW + 20f;
        var preBtn64X = preBtn32X + preBtnW + 12f;
        var preBtn256X = preBtn64X + preBtnW + 12f;

        RenderButton("32", preBtn32X, sizeBoxY, preBtnW, preBtnH, mousePos);
        RenderButton("64", preBtn64X, sizeBoxY, preBtnW, preBtnH, mousePos);
        RenderButton("256", preBtn256X, sizeBoxY, preBtnW, preBtnH, mousePos);

        // Calculated size summary
        int.TryParse(worldSizeInput, out var currentSize);
        if (currentSize <= 0) currentSize = 64;
        var dimStr = $"Grid: {currentSize}x{currentSize} chunks  ({currentSize * 16}x{currentSize * 16} blocks)";
        renderer.DrawText(dimStr, modalX + 35f, modalY + 224f, 1.0f, new Vector4(0.55f, 0.82f, 1.0f, 0.90f));

        // Resource requirement notice text
        var resNotice = "More chunks means more resources are required.";
        renderer.DrawText(resNotice, modalX + 35f, modalY + 248f, 1.05f, new Vector4(1.0f, 0.78f, 0.35f, 0.98f));

        // Divider before action buttons
        renderer.DrawQuad(modalX + 25f, modalY + 276f, modalW - 50f, 1.5f, new Vector4(0.25f, 0.30f, 0.40f, 0.7f));
        // Action Buttons
        var actBtnW = 195f;
        var actBtnH = 42f;
        var actBtnY = modalY + modalH - actBtnH - 24f;

        // CREATE WORLD
        var createBtnX = modalX + 35f;
        RenderButton("CREATE WORLD", createBtnX, actBtnY, actBtnW, actBtnH, mousePos, isPrimary: true);

        // CANCEL
        var cancelBtnX = modalX + modalW - actBtnW - 35f;
        RenderButton("CANCEL", cancelBtnX, actBtnY, actBtnW, actBtnH, mousePos);
    }
}