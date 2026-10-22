using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VelmoraCraft.src;
using VelmoraCraft.src.Audio;
using VelmoraCraft.src.Entities;
using VelmoraCraft.src.Graphics;
using VelmoraCraft.src.Physics;
using VelmoraCraft.src.UI;
using VelmoraCraft.src.World;

namespace VelmoraCraft
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

        // Day/Night Cycle & Sky
        private DayNightCycle? dayNightCycle;
        private SkyRenderer? skyRenderer;

        // Underwater & Under-lava screen tint transition states
        private float currentWaterTint = 0f;
        private float currentLavaTint = 0f;

        // Screenshot capture state (F2 - Minecraft standard)
        private bool takeScreenshotRequested = false;
        private bool f2KeyWasDown = false;

        /// <summary>
        /// Controls the strength of the foliage wind sway shader animation (leaves, grass, sugarcane, saplings, flowers, wheat).
        /// Easily tweakable variable to adjust the wind swaying intensity.
        /// </summary>
        public float WindStrength { get; set; } = 0.08f;

        private float runningTime = 0f;

        // Cached Shader Uniform Locations to eliminate per-frame driver string queries
        private int uModel = -1;
        private int uViewProj = -1;
        private int uTime = -1;
        private int uWindStrength = -1;
        private int uViewPos = -1;
        private int uFogColor = -1;
        private int uFogStart = -1;
        private int uFogEnd = -1;
        private int uLightDir = -1;
        private int uLightColor = -1;
        private int uAmbientColor = -1;
        private int uLightIntensity = -1;
        private int uNumPointLights = -1;
        private int uIsTranslucentPass = -1;
        private readonly int[] uPointLightPos = new int[16];
        private readonly int[] uPointLightCol = new int[16];
        private readonly int[] uPointLightRad = new int[16];
        private readonly int[] uPointLightInt = new int[16];
        private readonly List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> framePointLights = new List<(Vector3, Vector3, float, float)>(16);

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

            // Set Window Icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures", "icon.png");
                if (!File.Exists(iconPath)) iconPath = Path.Combine(Directory.GetCurrentDirectory(), "Textures", "icon.png");
                if (File.Exists(iconPath))
                {
                    StbImageSharp.StbImage.stbi_set_flip_vertically_on_load(0);
                    using var stream = File.OpenRead(iconPath);
                    var img = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                    Icon = new OpenTK.Windowing.Common.Input.WindowIcon(new OpenTK.Windowing.Common.Input.Image(img.Width, img.Height, img.Data));
                    StbImageSharp.StbImage.stbi_set_flip_vertically_on_load(1);
                }
            }
            catch { }
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
            InitShaderUniforms();
            texture = new Texture("atlas.png");

            // Initialize Sky & Celestial Renderer (Sun, Moon, Stars, Dynamic Clouds)
            skyRenderer = new SkyRenderer();
            skyRenderer.CloudAmount = 0.22f; // Cloud amount: 0.0 (clear sky) to 1.0 (heavy overcast). Edit this variable to easily tune cloudiness!

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
            uiManager.IsFullscreenGetter = () => WindowState == WindowState.Fullscreen;
            uiManager.OnToggleFullscreenRequested = () =>
            {
                if (WindowState == WindowState.Fullscreen)
                {
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowState = WindowState.Fullscreen;
                }
            };

            uiManager.OnSettingsChanged = () =>
            {
                SaveSystem.SaveSettings(new GameSettings
                {
                    IsFlying = player?.IsFlying ?? false,
                    IsFullscreen = WindowState == WindowState.Fullscreen,
                    IsHudEnabled = uiManager.IsHudEnabled,
                    RenderDistanceChunks = uiManager.RenderDistanceChunks,
                    FieldOfView = uiManager.FieldOfView
                });
            };

            var savedSettings = SaveSystem.LoadSettings();
            if (savedSettings != null)
            {
                if (savedSettings.IsFullscreen)
                {
                    WindowState = WindowState.Fullscreen;
                }
                uiManager.IsHudEnabled = savedSettings.IsHudEnabled;
                uiManager.RenderDistanceChunks = savedSettings.RenderDistanceChunks;
                uiManager.FieldOfView = savedSettings.FieldOfView;
            }

            // Initialize Victory Screen
            victoryScreen = new VictoryScreen(uiRenderer);
            victoryScreen.OnContinuePlaying = () =>
            {
                // Break portal blocks leaving only the obsidian structure
                if (world != null && player != null)
                {
                    int broken = PortalSystem.BreakPortalsNearPlayer(world, player);
                    if (broken > 0)
                    {
                        audioManager?.PlayBlockBreakSound(BlockType.Glass);
                    }
                }

                currentScene = SceneState.InGame;
                CursorState = CursorState.Grabbed;
            };
            victoryScreen.OnSaveAndMainMenu = () =>
            {
                // Also break the portal before saving so player does not re-trigger victory screen on reload
                if (world != null && player != null)
                {
                    PortalSystem.BreakPortalsNearPlayer(world, player);
                }

                SaveCurrentWorld();
                ReturnToMainMenu();
            };

            // Initialize Day/Night Cycle (Main Menu starts at Dawn: 45s)
            dayNightCycle = new DayNightCycle(startTimeSeconds: 45.0f);
            uiManager.DayNightCycle = dayNightCycle;

            // Main Menu Setup
            mainMenu = new MainMenu(uiRenderer);
            mainMenu.OnPlayNewGame = (seed, worldSize) => StartNewGame(seed, worldSize);
            mainMenu.OnContinue = () => ContinueGame();
            mainMenu.OnExit = () => Close();

            // Create initial scenic 3D world for Main Menu background
            world = new World(seed: 1337, worldChunks: 64);
            world.TranslucentPassUniformLoc = uIsTranslucentPass;
            world.DayNightCycle = dayNightCycle;

            // Scenic orbiting camera looking over the terrain
            int initCenter = world.WorldSizeX / 2;
            menuCamera = new Camera(width, height, new Vector3(initCenter, 75, initCenter));

            // Default InGame camera & player (placed at spawn)
            int spawnY = world.GetSurfaceHeight(initCenter, initCenter);
            Vector3 spawnPos = new Vector3(initCenter + 0.5f, spawnY + 2.5f, initCenter + 0.5f);
            camera = new Camera(width, height, spawnPos);
            player = new Player(spawnPos, camera);
            if (world != null) world.PlayerRef = player;
            if (savedSettings != null)
            {
                player.IsFlying = savedSettings.IsFlying;
            }
            SetupPlayerCallbacks(player);
            uiManager.SetPlayer(player);
            uiManager.SetWorld(world);

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
            world.TranslucentPassUniformLoc = uIsTranslucentPass;
            world.DayNightCycle = dayNightCycle;
            world.ChestManager = chestManager;

            chestManager?.Clear();
            totalPlayTime = 0f;

            int spawnBlockX = world.WorldSizeX / 2;
            int spawnBlockZ = world.WorldSizeZ / 2;
            int spawnY = world.GetSurfaceHeight(spawnBlockX, spawnBlockZ);
            Vector3 spawnPos = new Vector3(spawnBlockX + 0.5f, spawnY + 2.5f, spawnBlockZ + 0.5f);

            camera = new Camera(width, height, spawnPos);
            player = new Player(spawnPos, camera);
            if (world != null) world.PlayerRef = player;
            var currentSettings = SaveSystem.LoadSettings();
            if (currentSettings != null)
            {
                player.IsFlying = currentSettings.IsFlying;
            }
            SetupPlayerCallbacks(player);

            uiManager?.SetPlayer(player);
            uiManager?.SetWorld(world);

            if (dayNightCycle != null)
            {
                dayNightCycle.TimeOfDay = DayNightCycle.DAWN_DURATION + 60.0f; // Start in daytime
                dayNightCycle.Update(0f);
            }

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
            SaveSystem.SaveWorld(world, player, dayNightCycle, chestManager, totalPlayTime,
                uiManager: uiManager,
                isFullscreen: WindowState == WindowState.Fullscreen);
            if (mainMenu != null) mainMenu.HasActiveWorld = true;
        }

        private void LoadSavedWorld(SaveData data)
        {
            world?.Destroy();
            int playerChunkX = (int)MathF.Floor(data.PlayerX / Chunk.SIZE_X);
            int playerChunkZ = (int)MathF.Floor(data.PlayerZ / Chunk.SIZE_Z);
            world = new World(data.Seed, data.WorldChunks, new Vector2i(playerChunkX, playerChunkZ), data.ModifiedBlocks);
            world.TranslucentPassUniformLoc = uIsTranslucentPass;
            world.DayNightCycle = dayNightCycle;
            world.ChestManager = chestManager;

            chestManager?.Clear();

            var initialPos = new Vector3(data.PlayerX, data.PlayerY, data.PlayerZ);
            camera = new Camera(width, height, initialPos + new Vector3(0, 1.62f, 0));
            camera.Yaw = data.PlayerYaw;
            camera.Pitch = data.PlayerPitch;
            camera.UpdateVectors();

            player = new Player(initialPos, camera);
            if (world != null) world.PlayerRef = player;
            SetupPlayerCallbacks(player);

            SaveSystem.ApplyLoadedData(data, world!, player, dayNightCycle!, chestManager,
                uiManager: uiManager,
                setFullscreen: fs => WindowState = fs ? WindowState.Fullscreen : WindowState.Normal);
            totalPlayTime = data.PlayTimeSeconds;

            uiManager?.SetPlayer(player);
            uiManager?.SetWorld(world);
            currentScene = SceneState.InGame;
            if (mainMenu != null) mainMenu.HasActiveWorld = true;
            CursorState = CursorState.Grabbed;
        }

        private void SetupPlayerCallbacks(Player p)
        {
            p.ChestManager = chestManager;
            p.AudioManager = audioManager;
            p.OnFlyToggled = () => uiManager?.OnSettingsChanged?.Invoke();
            p.OnOpenChest = (pos, chestInv) =>
            {
                uiManager?.OpenChest(pos, chestInv);
                CursorState = CursorState.Normal;
            };
            p.OnOpenCraftingTable = () =>
            {
                uiManager?.OpenCraftingTable();
                CursorState = CursorState.Normal;
            };
            p.OnOpenFurnace = (pos) =>
            {
                uiManager?.OpenFurnace(pos);
                CursorState = CursorState.Normal;
            };
            p.OnPortalEntered = () => TriggerVictoryScreen();
            p.OnUseBed = (bedPos) =>
            {
                var phase = dayNightCycle?.CurrentPhase ?? DayNightPhase.Day;
                if (phase == DayNightPhase.Night || phase == DayNightPhase.Dusk)
                {
                    uiManager?.StartSleeping();
                    p.SpawnPosition = new Vector3(bedPos.X + 0.5f, bedPos.Y + 0.6f, bedPos.Z + 0.5f);
                    audioManager?.PlayCategorySound("wood", volume: 0.6f);
                }
                else
                {
                    uiManager?.ShowToast("You can only sleep at night!");
                }
            };
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
            if (dayNightCycle != null)
            {
                dayNightCycle.TimeOfDay = 45.0f; // Dawn only
                dayNightCycle.Update(0f);
            }
            if (mainMenu != null)
            {
                mainMenu.RandomizeSplash();
                mainMenu.HasActiveWorld = (world != null && player != null) || SaveSystem.HasSave();
            }
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
            skyRenderer?.Dispose();
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

            // Compute dynamic distance fog based on current Render Distance slider
            int viewChunks = (currentScene == SceneState.MainMenu)
                ? 10
                : (world?.ViewDistanceChunks ?? uiManager?.RenderDistanceChunks ?? 8);
            float maxRenderDistance = viewChunks * Chunk.SIZE_X;

            // Dynamic atmospheric sky & fog color
            Vector3 baseSkyColor = dayNightCycle?.SkyColor ?? new Vector3(0.68f, 0.82f, 0.95f);
            Vector3 skyColor = baseSkyColor;
            Vector3 fogColor = baseSkyColor;

            // Fog begins at 65% of max render distance and reaches 100% at the edge of loaded chunks
            float fogEnd = maxRenderDistance;
            float fogStart = MathF.Max(8.0f, maxRenderDistance * 0.65f);

            // Adapt camera farPlane to ensure frustum never prematurely clips beyond the fog horizon
            activeCam.FarPlane = MathF.Max(320.0f, maxRenderDistance + 64.0f);

            // Sync SkyRenderer cloud horizon fade with render distance
            if (skyRenderer != null)
            {
                skyRenderer.CloudFadeStart = fogStart;
                skyRenderer.CloudFadeEnd = fogEnd * 1.15f;
            }

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

            // Render Sky Celestial Bodies (Sun, Moon, Stars) behind the terrain
            if (skyRenderer != null)
            {
                GL.Disable(EnableCap.CullFace);
                GL.DepthMask(false);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                skyRenderer.RenderCelestials(activeCam, dayNightCycle);

                GL.DepthMask(true);
                GL.Enable(EnableCap.CullFace);
            }

            // Transformation matrices
            Matrix4 model = Matrix4.Identity;
            Matrix4 view = activeCam.GetViewMatrix();
            Matrix4 projection = activeCam.GetProjectionMatrix();
            Matrix4 viewProj = view * projection;

            shaderProgram!.Bind();
            texture!.Bind();

            GL.UniformMatrix4f(uModel, 1, true, ref model);
            GL.UniformMatrix4f(uViewProj, 1, true, ref viewProj);
            GL.Uniform1f(uTime, runningTime);
            GL.Uniform1f(uWindStrength, WindStrength);

            // Fog & camera uniforms
            Vector3 camPos = activeCam.Position;
            GL.Uniform3f(uViewPos, 1, ref camPos);
            GL.Uniform3f(uFogColor, 1, ref fogColor);
            GL.Uniform1f(uFogStart, fogStart);
            GL.Uniform1f(uFogEnd, fogEnd);

            // Dynamic Day/Night Lighting uniforms
            Vector3 lightDir = dayNightCycle?.ActiveLightDirection ?? Vector3.UnitY;
            Vector3 lightColor = dayNightCycle?.ActiveLightColor ?? Vector3.One;
            Vector3 ambientColor = dayNightCycle?.AmbientColor ?? new Vector3(0.4f, 0.4f, 0.4f);
            float lightIntensity = dayNightCycle?.ActiveLightIntensity ?? 1.0f;

            GL.Uniform3f(uLightDir, 1, ref lightDir);
            GL.Uniform3f(uLightColor, 1, ref lightColor);
            GL.Uniform3f(uAmbientColor, 1, ref ambientColor);
            GL.Uniform1f(uLightIntensity, lightIntensity);

            // Dynamic Point Lights (Placed Torches, Lava, Handheld Torch, Redstone Torch, Lanterns, Glowstone)
            framePointLights.Clear();

            // Handheld dynamic point light when equipped/selected in hotbar
            if (currentScene == SceneState.InGame && player != null)
            {
                BlockType held = player.SelectedPlacementBlock;
                if (held == BlockType.Torch)
                {
                    Vector3 torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                    framePointLights.Add((torchPos, new Vector3(1.0f, 0.74f, 0.40f), 16.0f, 1.4f));
                }
                else if (held == BlockType.RedstoneTorch)
                {
                    Vector3 torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                    framePointLights.Add((torchPos, new Vector3(1.0f, 0.20f, 0.15f), 12.0f, 1.2f));
                }
                else if (held == BlockType.JackOLantern)
                {
                    Vector3 torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                    framePointLights.Add((torchPos, new Vector3(1.0f, 0.65f, 0.20f), 15.0f, 1.4f));
                }
                else if (held == BlockType.Glowstone)
                {
                    Vector3 torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                    framePointLights.Add((torchPos, new Vector3(1.0f, 0.92f, 0.60f), 15.0f, 1.4f));
                }
            }

            // Add placed world point lights up to max limit (16 total)
            int maxWorldLights = 16 - framePointLights.Count;
            if (world != null && maxWorldLights > 0)
            {
                var worldLights = world.GetActivePointLights(activeCam.Position, maxCount: maxWorldLights, dt);
                for (int i = 0; i < worldLights.Count && framePointLights.Count < 16; i++)
                {
                    framePointLights.Add(worldLights[i]);
                }
            }

            int count = framePointLights.Count;
            GL.Uniform1i(uNumPointLights, count);

            for (int i = 0; i < count; i++)
            {
                var light = framePointLights[i];
                Vector3 pos = light.Position;
                Vector3 col = light.Color;
                GL.Uniform3f(uPointLightPos[i], 1, ref pos);
                GL.Uniform3f(uPointLightCol[i], 1, ref col);
                GL.Uniform1f(uPointLightRad[i], light.Radius);
                GL.Uniform1f(uPointLightInt[i], light.Intensity);
            }

            // Render 3D World with true 3D frustum and distance culling
            world?.Render(shaderProgram, activeCam);

            // Render Dynamic Voxel Clouds overhead
            if (skyRenderer != null)
            {
                GL.Disable(EnableCap.CullFace);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.DepthFunc(DepthFunction.Lequal);

                skyRenderer.RenderClouds(activeCam, dayNightCycle);

                GL.Enable(EnableCap.CullFace);
                GL.DepthFunc(DepthFunction.Less);
            }

            // Render 2D full-screen underwater, lava tint, or damage flash overlay
            float damageFlash = player?.DamageFlashTimer ?? 0f;
            if (uiRenderer != null && (currentWaterTint > 0.01f || currentLavaTint > 0.01f || damageFlash > 0.01f))
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

                if (damageFlash > 0.01f)
                {
                    // Red damage flash when taking damage
                    float flashAlpha = Math.Clamp(damageFlash * 0.8f, 0f, 0.35f);
                    uiRenderer.DrawQuad(0, 0, width, height, new Vector4(0.95f, 0.05f, 0.05f, flashAlpha));
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

            // Capture screenshot if requested (F2 - Minecraft standard) before buffer swap
            if (takeScreenshotRequested)
            {
                try
                {
                    string savedPath = ScreenshotHelper.CaptureAndSave(width, height);
                    string filename = Path.GetFileName(savedPath);
                    uiManager?.ShowScreenshotNotification(filename);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Screenshot] Error capturing screenshot: {ex.Message}");
                }
                takeScreenshotRequested = false;
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
            runningTime += dt;

            // Dynamic audio update for ambient & day/night music transitions
            audioManager?.Update(dayNightCycle?.CurrentPhase ?? DayNightPhase.Day, dt);

            // Dynamic sky & drifting clouds simulation
            skyRenderer?.Update(dt);

            // Global screenshot key (F2 - Minecraft standard)
            bool f2Down = keyboard.IsKeyDown(Keys.F2);
            if (f2Down && !f2KeyWasDown)
            {
                takeScreenshotRequested = true;
            }
            f2KeyWasDown = f2Down;

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

                // World streaming and lock day/night cycle to Dawn only
                world?.Update(menuCamera?.Position ?? new Vector3(512, 70, 512), dt);
                if (dayNightCycle != null)
                {
                    dayNightCycle.TimeOfDay = 45.0f; // Dawn only
                    dayNightCycle.Update(0f);
                }

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

        private void InitShaderUniforms()
        {
            if (shaderProgram == null) return;
            int pid = shaderProgram.ID;
            uModel = GL.GetUniformLocation(pid, "model");
            uViewProj = GL.GetUniformLocation(pid, "viewProj");
            uTime = GL.GetUniformLocation(pid, "uTime");
            uWindStrength = GL.GetUniformLocation(pid, "uWindStrength");
            uViewPos = GL.GetUniformLocation(pid, "viewPos");
            uFogColor = GL.GetUniformLocation(pid, "fogColor");
            uFogStart = GL.GetUniformLocation(pid, "fogStart");
            uFogEnd = GL.GetUniformLocation(pid, "fogEnd");
            uLightDir = GL.GetUniformLocation(pid, "lightDir");
            uLightColor = GL.GetUniformLocation(pid, "lightColor");
            uAmbientColor = GL.GetUniformLocation(pid, "ambientColor");
            uLightIntensity = GL.GetUniformLocation(pid, "lightIntensity");
            uNumPointLights = GL.GetUniformLocation(pid, "numPointLights");
            uIsTranslucentPass = GL.GetUniformLocation(pid, "isTranslucentPass");

            for (int i = 0; i < 16; i++)
            {
                uPointLightPos[i] = GL.GetUniformLocation(pid, $"pointLightPositions[{i}]");
                uPointLightCol[i] = GL.GetUniformLocation(pid, $"pointLightColors[{i}]");
                uPointLightRad[i] = GL.GetUniformLocation(pid, $"pointLightRadii[{i}]");
                uPointLightInt[i] = GL.GetUniformLocation(pid, $"pointLightIntensities[{i}]");
            }
        }
    }
}
