using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine.src.UI
{
    public class VictoryScreen
    {
        private readonly UIRenderer renderer;

        public Action? OnContinuePlaying { get; set; }
        public Action? OnSaveAndMainMenu { get; set; }

        private float timer = 0f;
        private bool leftMouseWasDown = false;

        public VictoryScreen(UIRenderer renderer)
        {
            this.renderer = renderer;
        }

        public void Reset()
        {
            timer = 0f;
        }

        public void Update(KeyboardState keyboard, MouseState mouse, Vector2 mousePos, float deltaTime, int screenW, int screenH)
        {
            timer += deltaTime;

            bool leftDown = mouse.IsButtonDown(MouseButton.Left);
            bool leftClicked = leftDown && !leftMouseWasDown;
            leftMouseWasDown = leftDown;

            float btnW = 260f;
            float btnH = 44f;
            float btnY = screenH * 0.82f;
            float gap = 30f;
            float totalW = btnW * 2f + gap;
            float startX = (screenW - totalW) * 0.5f;

            // 1. "CONTINUE PLAYING" Button
            float btn1X = startX;
            if (leftClicked && mousePos.X >= btn1X && mousePos.X <= btn1X + btnW && mousePos.Y >= btnY && mousePos.Y <= btnY + btnH)
            {
                OnContinuePlaying?.Invoke();
                return;
            }

            // 2. "SAVE & MAIN MENU" Button
            float btn2X = btn1X + btnW + gap;
            if (leftClicked && mousePos.X >= btn2X && mousePos.X <= btn2X + btnW && mousePos.Y >= btnY && mousePos.Y <= btnY + btnH)
            {
                OnSaveAndMainMenu?.Invoke();
                return;
            }

            // Pressing Escape or Enter resumes playing
            if (keyboard.IsKeyDown(Keys.Escape))
            {
                OnContinuePlaying?.Invoke();
            }
        }

        public void Render(int screenW, int screenH, Vector2 mousePos, int daysSurvived, float playTime, int blocksBroken, int blocksPlaced, int seed, int worldChunks)
        {
            renderer.Begin(screenW, screenH);

            // Deep cosmic violet end-dimension vignette background
            renderer.DrawQuad(0, 0, screenW, screenH, new Vector4(0.04f, 0.02f, 0.08f, 0.94f));

            // Floating purple ambient rift banner
            renderer.DrawQuad(0, screenH * 0.10f, screenW, 4f, new Vector4(0.70f, 0.20f, 0.95f, 0.70f));
            renderer.DrawQuad(0, screenH * 0.78f, screenW, 4f, new Vector4(0.70f, 0.20f, 0.95f, 0.70f));

            // Main Title
            string title = "THE END?";
            float titleScale = 3.6f;
            Vector2 titleSz = renderer.MeasureText(title, titleScale);
            float tx = (screenW - titleSz.X) * 0.5f;
            float ty = screenH * 0.14f;

            // Double 3D drop shadow
            renderer.DrawText(title, tx + 4f, ty + 4f, titleScale, new Vector4(0.20f, 0.05f, 0.35f, 0.95f), shadow: false);
            renderer.DrawText(title, tx + 2f, ty + 2f, titleScale, new Vector4(0.40f, 0.10f, 0.60f, 0.95f), shadow: false);
            renderer.DrawText(title, tx, ty, titleScale, new Vector4(1.0f, 0.85f, 0.30f, 1.0f), shadow: false);

            // Subtitle
            string sub = "- VICTORY ACHIEVED: THE NETHER RIFT OPENED -";
            Vector2 subSz = renderer.MeasureText(sub, 1.25f);
            renderer.DrawText(sub, (screenW - subSz.X) * 0.5f, ty + titleSz.Y + 12f, 1.25f, new Vector4(0.85f, 0.55f, 1.0f, 0.95f), shadow: true);

            // Lore / Poem Box
            float loreBoxW = Math.Min(680f, screenW - 80f);
            float loreBoxH = 150f;
            float loreBoxX = (screenW - loreBoxW) * 0.5f;
            float loreBoxY = ty + titleSz.Y + 44f;

            renderer.DrawPanel(loreBoxX, loreBoxY, loreBoxW, loreBoxH, new Vector4(0.08f, 0.05f, 0.14f, 0.88f), new Vector4(0.45f, 0.20f, 0.70f, 0.75f), 1.5f);

            string[] poemLines = new string[]
            {
                "You built the sacred obsidian gateway and sparked the flame.",
                "The rift hums with ancient power, bridging mortal voxel realms.",
                "You have conquered the stone, the mines, and the skies.",
                "And the universe said: You are not alone. You are the creator.",
                "Your journey has reached its crescendo... but endless horizons await."
            };

            float lineY = loreBoxY + 18f;
            for (int i = 0; i < poemLines.Length; i++)
            {
                Vector2 lineSz = renderer.MeasureText(poemLines[i], 1.0f);
                float lx = loreBoxX + (loreBoxW - lineSz.X) * 0.5f;
                renderer.DrawText(poemLines[i], lx, lineY, 1.0f, new Vector4(0.92f, 0.88f, 1.0f, 0.95f), shadow: true);
                lineY += 24f;
            }

            // Statistics Panel
            float statsW = loreBoxW;
            float statsH = 75f;
            float statsX = loreBoxX;
            float statsY = loreBoxY + loreBoxH + 16f;

            renderer.DrawPanel(statsX, statsY, statsW, statsH, new Vector4(0.06f, 0.04f, 0.10f, 0.85f), new Vector4(0.35f, 0.18f, 0.55f, 0.65f), 1.5f);

            int days = Math.Max(1, daysSurvived);
            int minutes = (int)(playTime / 60f);
            int seconds = (int)(playTime % 60f);
            string timeStr = $"{minutes:D2}m {seconds:D2}s";

            string stat1 = $"Days Survived: {days}   |   Playtime: {timeStr}   |   World: {worldChunks}x{worldChunks} Chunks";
            Vector2 stat1Sz = renderer.MeasureText(stat1, 1.05f);
            renderer.DrawText(stat1, statsX + (statsW - stat1Sz.X) * 0.5f, statsY + 14f, 1.05f, new Vector4(0.65f, 0.85f, 1.0f, 0.95f), shadow: true);

            string stat2 = $"Blocks Mined: {blocksBroken}   |   Blocks Placed: {blocksPlaced}   |   Seed: {seed}";
            Vector2 stat2Sz = renderer.MeasureText(stat2, 1.05f);
            renderer.DrawText(stat2, statsX + (statsW - stat2Sz.X) * 0.5f, statsY + 42f, 1.05f, new Vector4(0.95f, 0.80f, 0.40f, 0.95f), shadow: true);

            // Action Buttons
            float btnW = 260f;
            float btnH = 44f;
            float btnY = screenH * 0.82f;
            float gap = 30f;
            float totalW = btnW * 2f + gap;
            float startX = (screenW - totalW) * 0.5f;

            // 1. CONTINUE PLAYING
            float btn1X = startX;
            RenderButton("CONTINUE PLAYING", btn1X, btnY, btnW, btnH, mousePos, isPrimary: true);

            // 2. MAIN MENU
            float btn2X = btn1X + btnW + gap;
            RenderButton("SAVE & MAIN MENU", btn2X, btnY, btnW, btnH, mousePos);

            renderer.End();
        }

        private void RenderButton(string text, float x, float y, float w, float h, Vector2 mousePos, bool isPrimary = false)
        {
            bool isHovered = mousePos.X >= x && mousePos.X <= x + w && mousePos.Y >= y && mousePos.Y <= y + h;

            Vector4 bg = isHovered
                ? (isPrimary ? new Vector4(0.35f, 0.15f, 0.55f, 0.95f) : new Vector4(0.25f, 0.20f, 0.35f, 0.95f))
                : (isPrimary ? new Vector4(0.20f, 0.10f, 0.35f, 0.88f) : new Vector4(0.12f, 0.10f, 0.18f, 0.85f));

            Vector4 border = isHovered
                ? new Vector4(0.85f, 0.50f, 1.0f, 0.98f)
                : new Vector4(0.50f, 0.25f, 0.75f, 0.70f);

            renderer.DrawPanel(x, y, w, h, bg, border, isHovered ? 2.0f : 1.5f);

            Vector2 textSz = renderer.MeasureText(text, 1.2f);
            float tx = x + (w - textSz.X) * 0.5f;
            float ty = y + (h - textSz.Y) * 0.5f;
            Vector4 textColor = isHovered ? Vector4.One : new Vector4(0.92f, 0.88f, 0.98f, 0.95f);
            renderer.DrawText(text, tx, ty, 1.2f, textColor, shadow: true);
        }
    }
}
