using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;
using VelmoraCraft.src;
using VelmoraCraft.src.Audio;
using VelmoraCraft.src.Entities;
using VelmoraCraft.src.Graphics;
using VelmoraCraft.src.UI;
using VelmoraCraft.src.World;
using Image = OpenTK.Windowing.Common.Input.Image;

namespace VelmoraCraft;

public enum SceneState
{
    MainMenu,
    InGame,
    VictoryScreen
}

internal class Game : GameWindow
{
    private readonly List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> framePointLights = new(16);
    private readonly int[] uPointLightCol = new int[16];
    private readonly int[] uPointLightInt = new int[16];
    private readonly int[] uPointLightPos = new int[16];
    private readonly int[] uPointLightRad = new int[16];

    private AudioManager? audioManager;

    private Camera? camera;

    private ChestManager? chestManager;
    private float currentLavaTint;
    private SceneState currentScene = SceneState.MainMenu;

    private float currentWaterTint;

    private DayNightCycle? dayNightCycle;
    private bool f2KeyWasDown;
    private int height;

    private MainMenu? mainMenu;
    private Camera? menuCamera;
    private float menuOrbitAngle;
    private Player? player;
    private float runningTime;
    private ShaderProgram? shaderProgram;
    private SkyRenderer? skyRenderer;

    private bool takeScreenshotRequested;
    private Texture? texture;
    private float totalPlayTime;
    private int uAmbientColor = -1;
    private int uFogColor = -1;
    private int uFogEnd = -1;
    private int uFogStart = -1;
    private int uIsTranslucentPass = -1;
    private int uLightColor = -1;
    private int uLightDir = -1;
    private int uLightIntensity = -1;

    private int uModel = -1;
    private int uNumPointLights = -1;
    private int uTime = -1;
    private int uViewPos = -1;
    private int uViewProj = -1;
    private int uWindStrength = -1;
    private UIManager? uiManager;
    private UIRenderer? uiRenderer;
    private VictoryScreen? victoryScreen;

    private int width;

    private World? world;

    public Game(int width, int height, string title, VSyncMode vsync, WindowState isFullscreen,
        WindowBorder windowBorder) : base(
        GameWindowSettings.Default,
        new NativeWindowSettings
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

        CenterWindow(new Vector2i(width, height));

        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures", "icon.png");
            if (!File.Exists(iconPath))
                iconPath = Path.Combine(Directory.GetCurrentDirectory(), "Textures", "icon.png");
            if (File.Exists(iconPath))
            {
                StbImage.stbi_set_flip_vertically_on_load(0);
                using var stream = File.OpenRead(iconPath);
                var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                Icon = new WindowIcon(new Image(img.Width, img.Height, img.Data));
                StbImage.stbi_set_flip_vertically_on_load(1);
            }
        }
        catch
        {
        }
    }

    public float WindStrength { get; set; } = 0.08f;

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

        skyRenderer = new SkyRenderer { CloudAmount = 0.22f };

        GL.Enable(EnableCap.DepthTest);
        GL.FrontFace(FrontFaceDirection.Cw);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);

        audioManager = new AudioManager();
        chestManager = new ChestManager();
        uiRenderer = new UIRenderer(texture);
        uiManager = new UIManager(uiRenderer, chestManager)
        {
            OnQuitRequested = Close,
            OnReturnToMainMenuRequested = ReturnToMainMenu,
            OnSaveGameRequested = SaveCurrentWorld,
            IsFullscreenGetter = () => WindowState == WindowState.Fullscreen,
            OnToggleFullscreenRequested = () =>
                WindowState = WindowState == WindowState.Fullscreen ? WindowState.Normal : WindowState.Fullscreen,
            OnSettingsChanged = () =>
            {
                SaveSystem.SaveSettings(new GameSettings
                {
                    IsFlying = player?.IsFlying ?? false,
                    IsFullscreen = WindowState == WindowState.Fullscreen,
                    IsHudEnabled = uiManager?.IsHudEnabled ?? true,
                    RenderDistanceChunks = uiManager?.RenderDistanceChunks ?? 8,
                    FieldOfView = uiManager?.FieldOfView ?? 70f
                });
            }
        };

        var savedSettings = SaveSystem.LoadSettings();
        if (savedSettings != null)
        {
            if (savedSettings.IsFullscreen) WindowState = WindowState.Fullscreen;
            uiManager.IsHudEnabled = savedSettings.IsHudEnabled;
            uiManager.RenderDistanceChunks = savedSettings.RenderDistanceChunks;
            uiManager.FieldOfView = savedSettings.FieldOfView;
        }

        victoryScreen = new VictoryScreen(uiRenderer)
        {
            OnContinuePlaying = () =>
            {
                if (world != null && player != null)
                {
                    var broken = PortalSystem.BreakPortalsNearPlayer(world, player);
                    if (broken > 0) audioManager?.PlayBlockBreakSound(BlockType.Glass);
                }

                currentScene = SceneState.InGame;
                CursorState = CursorState.Grabbed;
            },
            OnSaveAndMainMenu = () =>
            {
                if (world != null && player != null) PortalSystem.BreakPortalsNearPlayer(world, player);

                SaveCurrentWorld();
                ReturnToMainMenu();
            }
        };

        dayNightCycle = new DayNightCycle(45.0f);
        uiManager.DayNightCycle = dayNightCycle;

        mainMenu = new MainMenu(uiRenderer)
        {
            OnPlayNewGame = StartNewGame,
            OnContinue = ContinueGame,
            OnExit = Close
        };

        world = new World
        {
            TranslucentPassUniformLoc = uIsTranslucentPass,
            DayNightCycle = dayNightCycle
        };

        var initCenter = world.WorldSizeX / 2;
        menuCamera = new Camera(width, height, new Vector3(initCenter, 75, initCenter));

        var spawnY = world.GetSurfaceHeight(initCenter, initCenter);
        var spawnPos = new Vector3(initCenter + 0.5f, spawnY + 2.5f, initCenter + 0.5f);
        camera = new Camera(width, height, spawnPos);
        player = new Player(spawnPos, camera);
        world.PlayerRef = player;
        if (savedSettings != null) player.IsFlying = savedSettings.IsFlying;
        SetupPlayerCallbacks(player);
        uiManager.SetPlayer(player);
        uiManager.SetWorld(world);

        currentScene = SceneState.MainMenu;
        mainMenu.HasActiveWorld = SaveSystem.HasSave();
        CursorState = CursorState.Normal;

        base.OnLoad();
    }

    private void StartNewGame(int seed, int worldSize)
    {
        world?.Destroy();
        world = new World(seed, worldSize)
        {
            TranslucentPassUniformLoc = uIsTranslucentPass,
            DayNightCycle = dayNightCycle,
            ChestManager = chestManager
        };

        chestManager?.Clear();
        totalPlayTime = 0f;

        var spawnBlockX = world.WorldSizeX / 2;
        var spawnBlockZ = world.WorldSizeZ / 2;
        var spawnY = world.GetSurfaceHeight(spawnBlockX, spawnBlockZ);
        var spawnPos = new Vector3(spawnBlockX + 0.5f, spawnY + 2.5f, spawnBlockZ + 0.5f);

        camera = new Camera(width, height, spawnPos);
        player = new Player(spawnPos, camera);
        world.PlayerRef = player;
        var currentSettings = SaveSystem.LoadSettings();
        if (currentSettings != null) player.IsFlying = currentSettings.IsFlying;
        SetupPlayerCallbacks(player);

        uiManager?.SetPlayer(player);
        uiManager?.SetWorld(world);

        if (dayNightCycle != null)
        {
            dayNightCycle.TimeOfDay = DayNightCycle.DAWN_DURATION + 60.0f;
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
        var playerChunkX = (int)MathF.Floor(data.PlayerX / Chunk.SIZE_X);
        var playerChunkZ = (int)MathF.Floor(data.PlayerZ / Chunk.SIZE_Z);
        world = new World(data.Seed, data.WorldChunks, new Vector2i(playerChunkX, playerChunkZ), data.ModifiedBlocks)
        {
            TranslucentPassUniformLoc = uIsTranslucentPass,
            DayNightCycle = dayNightCycle,
            ChestManager = chestManager
        };

        chestManager?.Clear();

        var initialPos = new Vector3(data.PlayerX, data.PlayerY, data.PlayerZ);
        camera = new Camera(width, height, initialPos + new Vector3(0, 1.62f, 0))
        {
            Yaw = data.PlayerYaw,
            Pitch = data.PlayerPitch
        };
        camera.UpdateVectors();

        player = new Player(initialPos, camera);
        world.PlayerRef = player;
        SetupPlayerCallbacks(player);

        SaveSystem.ApplyLoadedData(data, world, player, dayNightCycle!, chestManager,
            uiManager,
            fs => WindowState = fs ? WindowState.Fullscreen : WindowState.Normal);
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
        p.OnOpenFurnace = pos =>
        {
            uiManager?.OpenFurnace(pos);
            CursorState = CursorState.Normal;
        };
        p.OnPortalEntered = TriggerVictoryScreen;
        p.OnUseBed = bedPos =>
        {
            var phase = dayNightCycle?.CurrentPhase ?? DayNightPhase.Day;
            if (phase is DayNightPhase.Night or DayNightPhase.Dusk)
            {
                uiManager?.StartSleeping();
                p.SpawnPosition = new Vector3(bedPos.X + 0.5f, bedPos.Y + 0.6f, bedPos.Z + 0.5f);
                audioManager?.PlayCategorySound("wood", 0.6f);
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
            dayNightCycle.TimeOfDay = 45.0f;
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
        if (currentScene == SceneState.MainMenu && mainMenu != null && !string.IsNullOrEmpty(e.AsString))
            foreach (var c in e.AsString)
                mainMenu.HandleTextInput(c);

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
        var activeCam = currentScene == SceneState.MainMenu ? menuCamera ?? camera! : camera!;

        var eyeX = (int)Math.Floor(activeCam.Position.X);
        var eyeY = (int)Math.Floor(activeCam.Position.Y);
        var eyeZ = (int)Math.Floor(activeCam.Position.Z);
        var eyeBlock = world?.GetBlockWorld(eyeX, eyeY, eyeZ) ?? (byte)BlockType.Empty;

        var isUnderwater = eyeBlock == (byte)BlockType.Water;
        var isUnderLava = eyeBlock == (byte)BlockType.Lava;

        var dt = (float)args.Time;
        var targetWater = isUnderwater ? 0.38f : 0.0f;
        var targetLava = isUnderLava ? 0.62f : 0.0f;
        currentWaterTint = MathHelper.Lerp(currentWaterTint, targetWater, Math.Clamp(dt * 12.0f, 0f, 1f));
        currentLavaTint = MathHelper.Lerp(currentLavaTint, targetLava, Math.Clamp(dt * 12.0f, 0f, 1f));

        var viewChunks = currentScene == SceneState.MainMenu
            ? 10
            : world?.ViewDistanceChunks ?? uiManager?.RenderDistanceChunks ?? 8;
        float maxRenderDistance = viewChunks * Chunk.SIZE_X;

        var baseSkyColor = dayNightCycle?.SkyColor ?? new Vector3(0.68f, 0.82f, 0.95f);
        var skyColor = baseSkyColor;
        var fogColor = baseSkyColor;

        var fogEnd = maxRenderDistance;
        var fogStart = MathF.Max(8.0f, maxRenderDistance * 0.65f);

        activeCam.FarPlane = MathF.Max(320.0f, maxRenderDistance + 64.0f);

        if (skyRenderer != null)
        {
            skyRenderer.CloudFadeStart = fogStart;
            skyRenderer.CloudFadeEnd = fogEnd * 1.15f;
        }

        if (currentLavaTint > 0.01f)
        {
            var t = Math.Clamp(currentLavaTint / 0.62f, 0f, 1f);
            var lavaFog = new Vector3(0.85f, 0.18f, 0.04f);
            skyColor = Vector3.Lerp(skyColor, lavaFog, t);
            fogColor = Vector3.Lerp(fogColor, lavaFog, t);
            fogStart = MathHelper.Lerp(fogStart, 0.5f, t);
            fogEnd = MathHelper.Lerp(fogEnd, 6.0f, t);
        }
        else if (currentWaterTint > 0.01f)
        {
            var t = Math.Clamp(currentWaterTint / 0.38f, 0f, 1f);
            var waterFog = new Vector3(0.08f, 0.28f, 0.60f);
            skyColor = Vector3.Lerp(skyColor, waterFog, t);
            fogColor = Vector3.Lerp(fogColor, waterFog, t);
            fogStart = MathHelper.Lerp(fogStart, 1.0f, t);
            fogEnd = MathHelper.Lerp(fogEnd, 26.0f, t);
        }

        GL.ClearColor(skyColor.X, skyColor.Y, skyColor.Z, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

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

        var model = Matrix4.Identity;
        var view = activeCam.GetViewMatrix();
        var projection = activeCam.GetProjectionMatrix();
        var viewProj = view * projection;

        shaderProgram!.Bind();
        texture!.Bind();

        GL.UniformMatrix4f(uModel, 1, true, ref model);
        GL.UniformMatrix4f(uViewProj, 1, true, ref viewProj);
        GL.Uniform1f(uTime, runningTime);
        GL.Uniform1f(uWindStrength, WindStrength);

        var camPos = activeCam.Position;
        GL.Uniform3f(uViewPos, 1, ref camPos);
        GL.Uniform3f(uFogColor, 1, ref fogColor);
        GL.Uniform1f(uFogStart, fogStart);
        GL.Uniform1f(uFogEnd, fogEnd);

        var lightDir = dayNightCycle?.ActiveLightDirection ?? Vector3.UnitY;
        var lightColor = dayNightCycle?.ActiveLightColor ?? Vector3.One;
        var ambientColor = dayNightCycle?.AmbientColor ?? new Vector3(0.4f, 0.4f, 0.4f);
        var lightIntensity = dayNightCycle?.ActiveLightIntensity ?? 1.0f;

        GL.Uniform3f(uLightDir, 1, ref lightDir);
        GL.Uniform3f(uLightColor, 1, ref lightColor);
        GL.Uniform3f(uAmbientColor, 1, ref ambientColor);
        GL.Uniform1f(uLightIntensity, lightIntensity);

        framePointLights.Clear();

        if (currentScene == SceneState.InGame && player != null)
        {
            var held = player.SelectedPlacementBlock;
            if (held == BlockType.Torch)
            {
                var torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                framePointLights.Add((torchPos, new Vector3(1.0f, 0.74f, 0.40f), 16.0f, 1.4f));
            }
            else if (held == BlockType.RedstoneTorch)
            {
                var torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                framePointLights.Add((torchPos, new Vector3(1.0f, 0.20f, 0.15f), 12.0f, 1.2f));
            }
            else if (held == BlockType.JackOLantern)
            {
                var torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                framePointLights.Add((torchPos, new Vector3(1.0f, 0.65f, 0.20f), 15.0f, 1.4f));
            }
            else if (held == BlockType.Glowstone)
            {
                var torchPos = camera!.Position + camera.Forward * 0.35f - camera.Up * 0.15f;
                framePointLights.Add((torchPos, new Vector3(1.0f, 0.92f, 0.60f), 15.0f, 1.4f));
            }
        }

        var maxWorldLights = 16 - framePointLights.Count;
        if (world != null && maxWorldLights > 0)
        {
            var worldLights = world.GetActivePointLights(activeCam.Position, maxWorldLights, dt);
            for (var i = 0; i < worldLights.Count && framePointLights.Count < 16; i++)
                framePointLights.Add(worldLights[i]);
        }

        var count = framePointLights.Count;
        GL.Uniform1i(uNumPointLights, count);

        for (var i = 0; i < count; i++)
        {
            var light = framePointLights[i];
            var pos = light.Position;
            var col = light.Color;
            GL.Uniform3f(uPointLightPos[i], 1, ref pos);
            GL.Uniform3f(uPointLightCol[i], 1, ref col);
            GL.Uniform1f(uPointLightRad[i], light.Radius);
            GL.Uniform1f(uPointLightInt[i], light.Intensity);
        }

        world?.Render(shaderProgram, activeCam);

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

        var damageFlash = player?.DamageFlashTimer ?? 0f;
        if (uiRenderer != null && (currentWaterTint > 0.01f || currentLavaTint > 0.01f || damageFlash > 0.01f))
        {
            uiRenderer.Begin(width, height);
            if (currentLavaTint > 0.01f)
                uiRenderer.DrawQuad(0, 0, width, height, new Vector4(0.88f, 0.15f, 0.04f, currentLavaTint));
            else if (currentWaterTint > 0.01f)
                uiRenderer.DrawQuad(0, 0, width, height, new Vector4(0.06f, 0.35f, 0.75f, currentWaterTint));

            if (damageFlash > 0.01f)
            {
                var flashAlpha = Math.Clamp(damageFlash * 0.8f, 0f, 0.35f);
                uiRenderer.DrawQuad(0, 0, width, height, new Vector4(0.95f, 0.05f, 0.05f, flashAlpha));
            }

            uiRenderer.End();
        }

        if (currentScene == SceneState.MainMenu)
            mainMenu?.Render(width, height, MouseState.Position);
        else if (currentScene == SceneState.VictoryScreen)
            victoryScreen?.Render(width, height, MouseState.Position,
                dayNightCycle?.DaysSurvived ?? 0,
                totalPlayTime,
                player?.BlocksBroken ?? 0,
                player?.BlocksPlaced ?? 0,
                world?.Seed ?? 0,
                world?.WorldChunks ?? 64);
        else
            uiManager?.Render(width, height, MouseState.Position);

        if (takeScreenshotRequested)
        {
            try
            {
                var savedPath = ScreenshotHelper.CaptureAndSave(width, height);
                var filename = Path.GetFileName(savedPath);
                uiManager?.ShowScreenshotNotification(filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Screenshot] Error: {ex.Message}");
            }

            takeScreenshotRequested = false;
        }

        Context.SwapBuffers();

        base.OnRenderFrame(args);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        if (currentScene == SceneState.InGame && CursorState == CursorState.Grabbed &&
            !(uiManager?.IsScreenOpen ?? false)) player?.ProcessMouseMovement(e.Delta);

        base.OnMouseMove(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (currentScene == SceneState.InGame) uiManager?.HandleMouseScroll(e.OffsetY);

        base.OnMouseWheel(e);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        var mouse = MouseState;
        var keyboard = KeyboardState;
        var dt = (float)args.Time;
        runningTime += dt;

        audioManager?.Update(dayNightCycle?.CurrentPhase ?? DayNightPhase.Day, dt);
        skyRenderer?.Update(dt);

        var f2Down = keyboard.IsKeyDown(Keys.F2);
        if (f2Down && !f2KeyWasDown) takeScreenshotRequested = true;
        f2KeyWasDown = f2Down;

        if (currentScene == SceneState.MainMenu)
        {
            menuOrbitAngle += dt * 0.06f;
            var camDist = 65.0f;
            var camX = 512f + MathF.Cos(menuOrbitAngle) * camDist;
            var camZ = 512f + MathF.Sin(menuOrbitAngle) * camDist;

            if (menuCamera != null)
            {
                menuCamera.Position = new Vector3(camX, 74f, camZ);
                var lookTarget = new Vector3(512f, 56f, 512f);
                var toCenter = Vector3.Normalize(lookTarget - menuCamera.Position);

                menuCamera.Yaw = MathHelper.RadiansToDegrees(MathF.Atan2(toCenter.Z, toCenter.X));
                menuCamera.Pitch = MathHelper.RadiansToDegrees(MathF.Asin(toCenter.Y));
                menuCamera.UpdateVectors();
            }

            world?.Update(menuCamera?.Position ?? new Vector3(512, 70, 512), dt);
            if (dayNightCycle != null)
            {
                dayNightCycle.TimeOfDay = 45.0f;
                dayNightCycle.Update(0f);
            }

            mainMenu?.Update(keyboard, mouse, mouse.Position, dt, width, height);
            CursorState = CursorState.Normal;
        }
        else if (currentScene == SceneState.VictoryScreen)
        {
            victoryScreen?.Update(keyboard, mouse, mouse.Position, dt, width, height);
            CursorState = CursorState.Normal;
        }
        else
        {
            var cursorShouldBeGrabbed = true;
            uiManager?.Update(keyboard, mouse, mouse.Position, width, height, out cursorShouldBeGrabbed);
            CursorState = cursorShouldBeGrabbed ? CursorState.Grabbed : CursorState.Normal;

            var isPaused = uiManager?.IsPaused ?? false;

            if (!isPaused)
            {
                totalPlayTime += dt;
                world?.Update(camera!.Position, dt);
                dayNightCycle?.Update(dt);
            }

            if (cursorShouldBeGrabbed && !isPaused && player != null)
            {
                for (var i = 0; i < 9; i++)
                    if (keyboard.IsKeyDown(Keys.D1 + i))
                    {
                        player.SelectedHotbarSlot = i;
                        break;
                    }

                player.Update(world!, keyboard, mouse, dt);
            }
        }

        base.OnUpdateFrame(args);
    }

    private void InitShaderUniforms()
    {
        if (shaderProgram == null) return;
        var pid = shaderProgram.ID;
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

        for (var i = 0; i < 16; i++)
        {
            uPointLightPos[i] = GL.GetUniformLocation(pid, $"pointLightPositions[{i}]");
            uPointLightCol[i] = GL.GetUniformLocation(pid, $"pointLightColors[{i}]");
            uPointLightRad[i] = GL.GetUniformLocation(pid, $"pointLightRadii[{i}]");
            uPointLightInt[i] = GL.GetUniformLocation(pid, $"pointLightIntensities[{i}]");
        }
    }
}