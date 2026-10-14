using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VoxelRenderEngine.src;
using VoxelRenderEngine.src.Audio;
using VoxelRenderEngine.src.Entities;
using VoxelRenderEngine.src.Graphics;
using VoxelRenderEngine.src.Physics;
using VoxelRenderEngine.src.UI;
using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine
{
    public enum SceneState
    {
        MainMenu,
        InGame,
        VictoryScreen
    }

    internal class Game : GameWindow
    {
        private SceneState currentScene = SceneState.MainMenu;

        private World? world;
        private Texture? texture;
        private ShaderProgram? shaderProgram;

        // In-game Camera & Player
        private Camera? camera;
        private Player? player;

        // Main Menu & Scenic 3D Camera
        private MainMenu? mainMenu;
        private Camera? menuCamera;
        private float menuOrbitAngle = 0f;

        // UI System
        private ChestManager? chestManager;
        private UIRenderer? uiRenderer;
        private UIManager? uiManager;

        // Audio & Victory & Save System
        private AudioManager? audioManager;
        private VictoryScreen? victoryScreen;
        private float totalPlayTime = 0f;

        // Day/Night Cycle
        private DayNightCycle? dayNightCycle;

        // Underwater & Under-lava screen tint transition states
        private float currentWaterTint = 0f;
        private float currentLavaTint = 0f;

        private int width;
        private int height;

        public Game(int width, int height, string title, VSyncMode vsync, WindowState isFullscreen, WindowBorder windowBorder) : base(
                  GameWindowSettings.Default,
                  new NativeWindowSettings()
                  {
                      Title = title,
                      Vsync = vsync,
                      WindowState = isFullscreen,
                      WindowBorder = windowBorder,
                      StartFocused = true,
                      API = ContextAPI.OpenGL,
                      Profile = ContextProfile.Core
                  })
        {
            this.width = width;
            this.height = height;

            // Center The Window
            CenterWindow(new Vector2i(width, height));
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);

            width = e.Width;
            height = e.Height;

            camera?.UpdateAspectRatio(width, height);
            menuCamera?.UpdateAspectRatio(width, height);

            base.OnResize(e);
        }

        protected override void OnLoad()
        {
            shaderProgram = new ShaderProgram("Default.vert", "Default.frag");
            texture = new Texture("atlas.png");

            GL.Enable(EnableCap.DepthTest);
            GL.FrontFace(FrontFaceDirection.Cw);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);

            // Initialize Audio Engine
            audioManager = new AudioManager();

            // Initialize UI Renderer and Chest Manager
            chestManager = new ChestManager();
            uiRenderer = new UIRenderer(texture);
            uiManager = new UIManager(uiRenderer, chestManager);
            uiManager.OnQuitRequested = () => Close();
            uiManager.OnReturnToMainMenuRequested = () => ReturnToMainMenu();
            uiManager.OnSaveGameRequested = () => SaveCurrentWorld();

            // Initialize Victory Screen
            victoryScreen = new VictoryScreen(uiRenderer);
            victoryScreen.OnContinuePlaying = () =>
            {
                currentScene = SceneState.InGame;
                CursorState = CursorState.Grabbed;
            };
            victoryScreen.OnSaveAndMainMenu = () =>
            {
                SaveCurrentWorld();
                ReturnToMainMenu();
            };

            // Initialize Day/Night Cycle (Minecraft timings: 10m day, 1.5m dusk, 7m night, 1.5m dawn = 20 mins)
            dayNightCycle = new DayNightCycle(startTimeSeconds: 150.0f);
            uiManager.DayNightCycle = dayNightCycle;

            // Main Menu Setup
            mainMenu = new MainMenu(uiRenderer);
            mainMenu.OnPlayNewGame = (seed, worldSize) => StartNewGame(seed, worldSize);
            mainMenu.OnContinue = () => ContinueGame();
            mainMenu.OnExit = () => Close();

            // Create initial scenic 3D world for Main Menu background
            world = new World(seed: 1337, worldChunks: 64);

            // Scenic orbiting camera looking over the terrain
            int initCenter = world.WorldSizeX / 2;
            menuCamera = new Camera(width, height, new Vector3(initCenter, 75, initCenter));

            // Default InGame camera & player (placed at spawn)
            int spawnY = world.GetSurfaceHeight(initCenter, initCenter);
            Vector3 spawnPos = new Vector3(initCenter + 0.5f, spawnY + 2.5f, initCenter + 0.5f);
            camera = new Camera(width, height, spawnPos);
            player = new Player(spawnPos, camera);
            player.ChestManager = chestManager;
            player.AudioManager = audioManager;
            player.OnOpenChest = (pos, chestInv) =>
            {
                uiManager.OpenChest(pos, chestInv);
                CursorState = CursorState.Normal;
            };
            player.OnPortalEntered = () => TriggerVictoryScreen();
            uiManager.SetPlayer(player);

            // Start on Main Menu scene with visible cursor
            currentScene = SceneState.MainMenu;
            mainMenu.HasActiveWorld = SaveSystem.HasSave();
            CursorState = CursorState.Normal;

            base.OnLoad();
        }

        private void StartNewGame(int seed, int worldSize)
        {
            // Destroy existing world and recreate cleanly with given seed and world size
            world?.Destroy();
            world = new World(seed, worldSize);

            chestManager?.Clear();
            totalPlayTime = 0f;

            int spawnBlockX = world.WorldSizeX / 2;
            int spawnBlockZ = world.WorldSizeZ / 2;
            int spawnY = world.GetSurfaceHeight(spawnBlockX, spawnBlockZ);
            Vector3 spawnPos = new Vector3(spawnBlockX + 0.5f, spawnY + 2.5f, spawnBlockZ + 0.5f);

            camera = new Camera(width, height, spawnPos);
            player = new Player(spawnPos, camera);
            player.ChestManager = chestManager;
            player.AudioManager = audioManager;
            player.OnOpenChest = (pos, chestInv) =>
            {
                uiManager?.OpenChest(pos, chestInv);
                CursorState = CursorState.Normal;
            };
            player.OnPortalEntered = () => TriggerVictoryScreen();

            // Populate initial helpful starter inventory (including Obsidian & Flint and Steel for portal testing)
            player.Inventory.Clear();
            player.Inventory.AddItem(BlockType.Planks, 64);
            player.Inventory.AddItem(BlockType.Cobblestone, 64);
            player.Inventory.AddItem(BlockType.Torch, 16);
            player.Inventory.AddItem(BlockType.MinecartItem, 2);
            player.Inventory.AddItem(BlockType.Rail, 32);
            player.Inventory.AddItem(BlockType.PoweredRail, 8);
            player.Inventory.AddItem(BlockType.Lever, 4);
            player.Inventory.AddItem(BlockType.Obsidian, 14);
            player.Inventory.AddItem(BlockType.FlintAndSteel, 1);

            uiManager?.SetPlayer(player);

            currentScene = SceneState.InGame;
            if (mainMenu != null) mainMenu.HasActiveWorld = true;
            CursorState = CursorState.Grabbed;
        }

        private void ContinueGame()
        {
            if (SaveSystem.HasSave())
            {
                var data = SaveSystem.LoadSaveData();
                if (data != null)
                {
                    LoadSavedWorld(data);
                    return;
                }
            }

            if (player == null || world == null) return;

            currentScene = SceneState.InGame;
            CursorState = CursorState.Grabbed;
        }

        private void SaveCurrentWorld()
        {
            if (world == null || player == null || dayNightCycle == null) return;
            SaveSystem.SaveWorld(world, player, dayNightCycle, chestManager, totalPlayTime);
            if (mainMenu != null) mainMenu.HasActiveWorld = true;
        }

        private void LoadSavedWorld(SaveData data)
        {
            world?.Destroy();
            world = new World(data.Seed, data.WorldChunks);

            chestManager?.Clear();

            camera = new Camera(width, height, new Vector3(data.PlayerX, data.PlayerY, data.PlayerZ));
            camera.Yaw = data.PlayerYaw;
            camera.Pitch = data.PlayerPitch;
            camera.UpdateVectors();

            player = new Player(new Vector3(data.PlayerX, data.PlayerY, data.PlayerZ), camera);
            player.ChestManager = chestManager;
            player.AudioManager = audioManager;
            player.OnOpenChest = (pos, chestInv) =>
            {
                uiManager?.OpenChest(pos, chestInv);
                CursorState = CursorState.Normal;
            };
            player.OnPortalEntered = () => TriggerVictoryScreen();

            SaveSystem.ApplyLoadedData(data, world, player, dayNightCycle!, chestManager);
            totalPlayTime = data.PlayTimeSeconds;

            uiManager?.SetPlayer(player);
            currentScene = SceneState.InGame;
            if (mainMenu != null) mainMenu.HasActiveWorld = true;
            CursorState = CursorState.Grabbed;
        }

        private void TriggerVictoryScreen()
        {
            if (currentScene == SceneState.VictoryScreen) return;
            currentScene = SceneState.VictoryScreen;
            CursorState = CursorState.Normal;
            audioManager?.PlayPortalTravel();
        }

        private void ReturnToMainMenu()
        {
            currentScene = SceneState.MainMenu;
            if (mainMenu != null) mainMenu.HasActiveWorld = (world != null && player != null) || SaveSystem.HasSave();
            CursorState = CursorState.Normal;
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            if (currentScene == SceneState.MainMenu && mainMenu != null)
            {
                if (!string.IsNullOrEmpty(e.AsString))
                {
                    foreach (char c in e.AsString)
                    {
                        mainMenu.HandleTextInput(c);
                    }
                }
            }

            base.OnTextInput(e);
        }

        protected override void OnUnload()
        {
            audioManager?.Dispose();
            uiRenderer?.Delete();
            shaderProgram?.Delete();
            texture?.Delete();
            world?.Destroy();

            base.OnUnload();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            Camera activeCam = (currentScene == SceneState.MainMenu) ? (menuCamera ?? camera!) : camera!;

            // Detect if camera is submerged in water or lava
            int eyeX = (int)Math.Floor(activeCam.Position.X);
            int eyeY = (int)Math.Floor(activeCam.Position.Y);
            int eyeZ = (int)Math.Floor(activeCam.Position.Z);
            byte eyeBlock = world?.GetBlockWorld(eyeX, eyeY, eyeZ) ?? (byte)BlockType.Empty;

            bool isUnderwater = (eyeBlock == (byte)BlockType.Water);
            bool isUnderLava = (eyeBlock == (byte)BlockType.Lava);

            float dt = (float)args.Time;
            float targetWater = isUnderwater ? 0.38f : 0.0f;
            float targetLava = isUnderLava ? 0.62f : 0.0f;
            currentWaterTint = MathHelper.Lerp(currentWaterTint, targetWater, Math.Clamp(dt * 12.0f, 0f, 1f));
            currentLavaTint = MathHelper.Lerp(currentLavaTint, targetLava, Math.Clamp(dt * 12.0f, 0f, 1f));

            // Dynamic atmospheric sky & fog color
            Vector3 baseSkyColor = dayNightCycle?.SkyColor ?? new Vector3(0.68f, 0.82f, 0.95f);
            Vector3 skyColor = baseSkyColor;
            Vector3 fogColor = baseSkyColor;
            float fogStart = 50.0f;
            float fogEnd = 140.0f;

            if (currentLavaTint > 0.01f)
            {
                float t = Math.Clamp(currentLavaTint / 0.62f, 0f, 1f);
                Vector3 lavaFog = new Vector3(0.85f, 0.18f, 0.04f);
                skyColor = Vector3.Lerp(skyColor, lavaFog, t);
                fogColor = Vector3.Lerp(fogColor, lavaFog, t);
                fogStart = MathHelper.Lerp(fogStart, 0.5f, t);
                fogEnd = MathHelper.Lerp(fogEnd, 6.0f, t);
            }
            else if (currentWaterTint > 0.01f)
            {
                float t = Math.Clamp(currentWaterTint / 0.38f, 0f, 1f);
                Vector3 waterFog = new Vector3(0.08f, 0.28f, 0.60f);
                skyColor = Vector3.Lerp(skyColor, waterFog, t);
                fogColor = Vector3.Lerp(fogColor, waterFog, t);
                fogStart = MathHelper.Lerp(fogStart, 1.0f, t);
                fogEnd = MathHelper.Lerp(fogEnd, 26.0f, t);
            }

            GL.ClearColor(skyColor.X, skyColor.Y, skyColor.Z, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Transformation matrices
            Matrix4 model = Matrix4.Identity;
            Matrix4 view = activeCam.GetViewMatrix();
            Matrix4 projection = activeCam.GetProjectionMatrix();

            shaderProgram!.Bind();
            texture!.Bind();

            int modelLocation = GL.GetUniformLocation(shaderProgram.ID, "model");
            int viewLocation = GL.GetUniformLocation(shaderProgram.ID, "view");
            int projectionLocation = GL.GetUniformLocation(shaderProgram.ID, "projection");

            GL.UniformMatrix4f(modelLocation, 1, true, ref model);
            GL.UniformMatrix4f(viewLocation, 1, true, ref view);
            GL.UniformMatrix4f(projectionLocation, 1, true, ref projection);

            // Fog & camera uniforms
            Vector3 camPos = activeCam.Position;
            int viewPosLoc = GL.GetUniformLocation(shaderProgram.ID, "viewPos");
            int fogColorLoc = GL.GetUniformLocation(shaderProgram.ID, "fogColor");
            int fogStartLoc = GL.GetUniformLocation(shaderProgram.ID, "fogStart");
            int fogEndLoc = GL.GetUniformLocation(shaderProgram.ID, "fogEnd");

            GL.Uniform3f(viewPosLoc, 1, ref camPos);
            GL.Uniform3f(fogColorLoc, 1, ref fogColor);
            GL.Uniform1f(fogStartLoc, fogStart);
            GL.Uniform1f(fogEndLoc, fogEnd);

            // Dynamic Day/Night Lighting uniforms
            int lightDirLoc = GL.GetUniformLocation(shaderProgram.ID, "lightDir");
            int lightColorLoc = GL.GetUniformLocation(shaderProgram.ID, "lightColor");
            int ambientColorLoc = GL.GetUniformLocation(shaderProgram.ID, "ambientColor");
            int lightIntensityLoc = GL.GetUniformLocation(shaderProgram.ID, "lightIntensity");

            Vector3 lightDir = dayNightCycle?.ActiveLightDirection ?? Vector3.UnitY;
            Vector3 lightColor = dayNightCycle?.ActiveLightColor ?? Vector3.One;
            Vector3 ambientColor = dayNightCycle?.AmbientColor ?? new Vector3(0.4f, 0.4f, 0.4f);
            float lightIntensity = dayNightCycle?.ActiveLightIntensity ?? 1.0f;

            GL.Uniform3f(lightDirLoc, 1, ref lightDir);
            GL.Uniform3f(lightColorLoc, 1, ref lightColor);
            GL.Uniform3f(ambientColorLoc, 1, ref ambientColor);
            GL.Uniform1f(lightIntensityLoc, lightIntensity);

            // Dynamic Point Lights (Placed Torches, Lava, Handheld Torch)
            var pointLights = world?.GetActivePointLights(activeCam.Position, maxCount: 15)
                ?? new List<(Vector3, Vector3, float, float)>();

            if (currentScene == SceneState.InGame && player != null && player.SelectedPlacementBlock == BlockType.Torch)
            {
                pointLights.Insert(0, (camera!.Position, new Vector3(1.0f, 0.72f, 0.38f), 16.0f, 1.4f));
            }

            int count = Math.Min(16, pointLights.Count);
            int numLightsLoc = GL.GetUniformLocation(shaderProgram.ID, "numPointLights");
            GL.Uniform1i(numLightsLoc, count);

            for (int i = 0; i < count; i++)
            {
                var light = pointLights[i];
                int posLoc = GL.GetUniformLocation(shaderProgram.ID, $"pointLightPositions[{i}]");
                int colLoc = GL.GetUniformLocation(shaderProgram.ID, $"pointLightColors[{i}]");
                int radLoc = GL.GetUniformLocation(shaderProgram.ID, $"pointLightRadii[{i}]");
                int intLoc = GL.GetUniformLocation(shaderProgram.ID, $"pointLightIntensities[{i}]");

                Vector3 pos = light.Position;
                Vector3 col = light.Color;
                GL.Uniform3f(posLoc, 1, ref pos);
                GL.Uniform3f(colLoc, 1, ref col);
                GL.Uniform1f(radLoc, light.Radius);
                GL.Uniform1f(intLoc, light.Intensity);
            }

            // Render 3D World with directional and distance culling
            world?.Render(shaderProgram, activeCam);

            // Render 2D full-screen underwater or lava tint overlay
            if (uiRenderer != null && (currentWaterTint > 0.01f || currentLavaTint > 0.01f))
            {
                uiRenderer.Begin(width, height);
                if (currentLavaTint > 0.01f)
                {
                    // Reddish fiery tint when under lava
                    uiRenderer.DrawQuad(0, 0, width, height, new Vector4(0.88f, 0.15f, 0.04f, currentLavaTint));
                }
                else if (currentWaterTint > 0.01f)
                {
                    // Bluish aquatic tint when under water
                    uiRenderer.DrawQuad(0, 0, width, height, new Vector4(0.06f, 0.35f, 0.75f, currentWaterTint));
                }
                uiRenderer.End();
            }

            // Render UI based on current Scene
            if (currentScene == SceneState.MainMenu)
            {
                mainMenu?.Render(width, height, MouseState.Position);
            }
            else if (currentScene == SceneState.VictoryScreen)
            {
                victoryScreen?.Render(width, height, MouseState.Position,
                    dayNightCycle?.DaysSurvived ?? 0,
                    totalPlayTime,
                    player?.BlocksBroken ?? 0,
                    player?.BlocksPlaced ?? 0,
                    world?.Seed ?? 0,
                    world?.WorldChunks ?? 64);
            }
            else
            {
                uiManager?.Render(width, height, MouseState.Position);
            }

            Context.SwapBuffers();

            base.OnRenderFrame(args);
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            if (currentScene == SceneState.InGame)
            {
                if (CursorState == CursorState.Grabbed && !(uiManager?.IsScreenOpen ?? false))
                {
                    player?.ProcessMouseMovement(e.Delta);
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (currentScene == SceneState.InGame)
            {
                uiManager?.HandleMouseScroll(e.OffsetY);
            }

            base.OnMouseWheel(e);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            MouseState mouse = MouseState;
            KeyboardState keyboard = KeyboardState;
            float dt = (float)args.Time;

            // Dynamic audio update for ambient & day/night music transitions
            audioManager?.Update(dayNightCycle?.CurrentPhase ?? DayNightPhase.Day, dt);

            if (currentScene == SceneState.MainMenu)
            {
                // Orbiting 3D camera pan over terrain
                menuOrbitAngle += dt * 0.06f;
                float camDist = 65.0f;
                float camX = 512f + MathF.Cos(menuOrbitAngle) * camDist;
                float camZ = 512f + MathF.Sin(menuOrbitAngle) * camDist;

                if (menuCamera != null)
                {
                    menuCamera.Position = new Vector3(camX, 74f, camZ);
                    Vector3 lookTarget = new Vector3(512f, 56f, 512f);
                    Vector3 toCenter = Vector3.Normalize(lookTarget - menuCamera.Position);

                    menuCamera.Yaw = MathHelper.RadiansToDegrees(MathF.Atan2(toCenter.Z, toCenter.X));
                    menuCamera.Pitch = MathHelper.RadiansToDegrees(MathF.Asin(toCenter.Y));
                    menuCamera.UpdateVectors();
                }

                // Update world streaming and day/night cycle for moving clouds & light
                world?.Update(menuCamera?.Position ?? new Vector3(512, 70, 512), dt);
                dayNightCycle?.Update(dt * 0.4f);

                // Update Main Menu UI
                mainMenu?.Update(keyboard, mouse, mouse.Position, dt, width, height);
                CursorState = CursorState.Normal;
            }
            else if (currentScene == SceneState.VictoryScreen)
            {
                victoryScreen?.Update(keyboard, mouse, mouse.Position, dt, width, height);
                CursorState = CursorState.Normal;
            }
            else // SceneState.InGame
            {
                bool cursorShouldBeGrabbed = true;
                if (uiManager != null)
                {
                    uiManager.Update(keyboard, mouse, mouse.Position, width, height, out cursorShouldBeGrabbed);
                }
                CursorState = cursorShouldBeGrabbed ? CursorState.Grabbed : CursorState.Normal;

                bool isPaused = uiManager?.IsPaused ?? false;

                // Only update world simulation & time when not paused
                if (!isPaused)
                {
                    totalPlayTime += dt;
                    world?.Update(camera!.Position, dt);
                    dayNightCycle?.Update(dt);
                }

                // Only update player controls and movement when not paused and in gameplay
                if (cursorShouldBeGrabbed && !isPaused && player != null)
                {
                    // Number keys 1-9 for quick hotbar slot selection
                    if (keyboard.IsKeyDown(Keys.D1)) player.SelectedHotbarSlot = 0;
                    if (keyboard.IsKeyDown(Keys.D2)) player.SelectedHotbarSlot = 1;
                    if (keyboard.IsKeyDown(Keys.D3)) player.SelectedHotbarSlot = 2;
                    if (keyboard.IsKeyDown(Keys.D4)) player.SelectedHotbarSlot = 3;
                    if (keyboard.IsKeyDown(Keys.D5)) player.SelectedHotbarSlot = 4;
                    if (keyboard.IsKeyDown(Keys.D6)) player.SelectedHotbarSlot = 5;
                    if (keyboard.IsKeyDown(Keys.D7)) player.SelectedHotbarSlot = 6;
                    if (keyboard.IsKeyDown(Keys.D8)) player.SelectedHotbarSlot = 7;
                    if (keyboard.IsKeyDown(Keys.D9)) player.SelectedHotbarSlot = 8;

                    player.Update(world!, keyboard, mouse, dt);
                }
            }

            base.OnUpdateFrame(args);
        }
    }
}
