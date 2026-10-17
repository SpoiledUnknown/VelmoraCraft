# VelmoraCraft — Execution & System Flow

This document outlines the complete architectural flow of the program, detailing the lifecycle from application startup to scene management, asynchronous terrain streaming, physics, multi-pass rendering, ecosystem simulations, and shutdown.

---

## 1. High-Level System Architecture

```mermaid
flowchart TD
    Init["Program.cs / Game.cs Init"] --> Subsystems["Init OpenAL, UIRenderer, Shaders, Atlas"]
    Subsystems --> SettingsLoad["Load GameSettings (options.json)"]
    SettingsLoad --> SceneInit["Initialize Scene State (MainMenu)"]
    SceneInit --> MenuWorld["Create Scenic 3D World (Dawn)"]
    MenuWorld --> GameLoop["Run Main Game Loop"]

    subgraph GameLoop["Per-Frame Game Loop"]
        direction TB
        Input["Process Input & Window Events"] --> SceneBranch{"Current SceneState"}
        SceneBranch -->|MainMenu| MenuUpdate["Orbit Menu Camera & Draw Main Menu"]
        SceneBranch -->|InGame| InGameUpdate["Update Physics, Survival, Entities & Streaming"]
        SceneBranch -->|VictoryScreen| VictoryUpdate["Break Nearby Portals & Draw Victory Screen"]
        InGameUpdate --> Simulations["Run Living Ecosystem (Fluids, Crops, Grass Spread, Nocturnal Cobwebs)"]
        Simulations --> GPUUpload["Upload Ready Meshes (Max 6/frame)"]
        GPUUpload --> RenderPasses["Render Frame (Sky, Opaque, Water, Entities, Custom UI)"]
        RenderPasses --> Swap["Swap Buffers"]
    end

    GameLoop --> Shutdown["Window Close & Cleanup"]
```

---

## 2. Program Startup & Scene Flow

1. **Entrypoint (`Program.cs`)**:
   * Creates the `Game` window configuration (resolution, title: *VelmoraCraft*, OpenGL 3.3 Core profile).
   * Launches `game.Run()`.

2. **Initialization (`Game.OnLoad`)**:
   * **OpenGL Configuration**: Enables depth testing (`GL.Enable(EnableCap.DepthTest)`), backface culling, and alpha blending.
   * **Audio (`AudioManager`)**: Initializes OpenAL device, listener, sound effect buffer cache, and starts background music playback.
   * **Custom UI Subsystem (`UIRenderer` & `FontRenderer`)**:
     * Instantiates the OpenGL 2D batch renderer and bitmap font glyph cache.
     * Sets up `UIManager` (HUD, pause menu, inventory, chest, furnace, crafting stations).
   * **Settings Restoration (`SaveSystem.LoadSettings`)**:
     * Reads `saves/options.json` and restores WindowState (fullscreen), HUD visibility, View Distance, and FOV.
   * **Graphics Assets**:
     * Compiles shader programs (`Default.vert`, `Default.frag`, `Sky.vert`, `Sky.frag`).
     * Loads the 256-block texture atlas with mipmapping and nearest-neighbor filtering.
   * **Scene Initialization**:
     * Creates a scenic 64-chunk 3D world locked at Dawn lighting (45s).
     * Sets `menuCamera` in an orbit looking over the landscape.
     * Sets `currentScene = SceneState.MainMenu` with active mouse cursor.

3. **Game Transitions**:
   * **Start New Game**: Destroys scenic world, creates clean world with chosen seed/size, sets player spawn atop terrain, and enters `SceneState.InGame` with grabbed mouse cursor.
   * **Continue Game**: Loads `SaveData` via `SaveSystem.LoadSaveData()`, reinstates player position, health, inventory, chests, modified blocks, and settings.
   * **Nether Portal Entry**: Triggers `SceneState.VictoryScreen`, breaks nearby portal blocks to prevent infinite reload loops, and offers "Continue Playing" or "Save & Main Menu".

---

## 3. Asynchronous Chunk Streaming Flow

The chunk lifecycle is managed asynchronously across background threads and synchronized onto the main thread for GPU upload:

```mermaid
stateDiagram-v2
    [*] --> Queued: Player moves near ungenerated chunk
    Queued --> GeneratingTerrain: Worker Thread dequeues
    GeneratingTerrain --> WaitingNeighbors: Generate 3D simplex noise
    WaitingNeighbors --> Decorating: Check/generate 4 adjacent chunks
    Decorating --> Meshing: Place trees, flora, structures
    Meshing --> ReadyToUpload: Build ChunkVertex mesh via ArrayPool
    ReadyToUpload --> UploadedToGPU: Main thread uploads (budgeted 6/frame)
    UploadedToGPU --> Rendered: Frustum & distance test passed
    Rendered --> Unloaded: Distance > ViewDistance + 4 (GPU buffers freed)
```

1. **Streaming Check (`World.StreamChunksAround`)**:
   * Evaluates chunks within `ViewDistanceChunks` (2 to 16 chunks radius).
   * Sorts candidate chunks by distance squared to prioritize nearest chunks.
   * Enqueues up to 32 chunks per batch into `workQueue`, marking `IsGeneratingMesh = true`.
2. **Worker Threads (`World.WorkerLoop`)**:
   * Dequeue chunk coordinate from `workQueue`.
   * Throttles execution if `readyToUploadMeshes.Count >= 24` to prevent memory backlogs.
   * Generate 3D noise terrain for center and adjacent 4 neighbor chunks.
   * Apply decorations (trees, flora, structures).
   * Execute `ChunkMesher.BuildMesh()` renting arrays from `ArrayPool<T>.Shared`.
   * Push completed `ChunkMesh` to `readyToUploadMeshes` concurrent queue.
3. **Main Thread GPU Upload (`World.Update`)**:
   * Dequeues up to `maxUploadsPerFrame` (6) meshes.
   * Creates/updates single interleaved VBO and VAO on the active OpenGL context.
   * Disposes `item.mesh`, returning rented arrays to `ArrayPool`.
   * Frees GPU memory for distant chunks beyond view radius.

---

## 4. Main Loop: Update Flow (`OnUpdateFrame`)

Executed once per frame prior to rendering:

```
OnUpdateFrame(FrameEventArgs e)
 ├── 1. Process Window / Input State
 │    ├── Handle ESC (Toggle Pause Menu)
 │    ├── Hotbar slot selection (1-9 or Scroll Wheel)
 │    └── Mouse capture & camera orientation
 ├── 2. Living Ecosystem Simulations:
 │    ├── Fluid Simulation (FluidSystem.Update): propagate water and lava flow (limit: 8 blocks)
 │    ├── Crop Growth (Wheat stages 0..7 advance based on random tick)
 │    ├── Dirt-to-Grass Spread: countdown timers convert eligible dirt to grass after 1-2 in-game days
 │    └── Nocturnal Cobweb Spawning: trees in loaded chunks spawn atmospheric cobwebs under leaf canopies at night
 ├── 3. Player Update (Player.Update)
 │    ├── Swept AABB Physics (Move along X, Y, Z with step-assist)
 │    ├── Directional Block & Door Placement (Calculates facing from Camera.Forward)
 │    ├── Minecart riding & rail tracking logic
 │    ├── DDA Raycast for block highlight, placement, and breaking
 │    └── Survival Controller:
 │         ├── Calculate fall distance & apply damage if fall >= 3 blocks
 │         ├── Check head submersion for oxygen depletion & drowning
 │         ├── Check body contact with lava for fire damage
 │         └── Passive health regeneration (after 10s without damage)
 └── 4. World Update (World.Update)
      ├── Upload ready chunk meshes to GPU & dispose pooled arrays
      ├── Stream surrounding chunks based on camera position
      └── Update dynamic entities (minecarts)
```

---

## 5. Main Loop: Render Flow (`OnRenderFrame`)

The render pipeline uses a multi-pass approach to ensure correct lighting and transparency sorting:

```mermaid
flowchart TD
    Clear["Clear Color & Depth Buffers"] --> Sky["Pass 0: Render Dynamic Skybox\n(Sun, Moon, Moving Clouds, Day/Night Tint)"]
    Sky --> Opaque["Pass 1: Render Opaque Terrain\n(DepthMask = ON, Blend = OFF, Dynamic Fog)"]
    Opaque --> Water["Pass 2: Render Translucent Water & Portals\n(DepthMask = OFF, Blend = ON)"]
    Water --> Entities["Pass 3: Render Entities & Minecarts"]
    Entities --> UI["Pass 4: Render 2D UI & HUD via UIRenderer\n(Health & Oxygen, Hotbar, Crosshair, Menus)"]
    UI --> Present["Swap Buffers (Present to Screen)"]
```

### Detailed Render Passes:
1. **Pass 0: Skybox (`SkyRenderer.Render`)**:
   * Renders celestial hemisphere with dynamic sun, moon, stars, and procedural moving clouds.
   * Updates atmospheric sky tint and distance fog parameters according to the current render distance.
2. **Pass 1: Opaque Terrain (`World.Render` - Pass 1)**:
   * Sets `isTranslucentPass = 0`, `DepthMask = true`, `Blend = false`.
   * Frustum-culls chunks outside camera field of view.
   * Draws opaque geometry (stone, dirt, wood, leaves, directional doors, etc.).
   * Solid surfaces under water are written to depth buffer first.
3. **Pass 2: Translucent Surfaces (`World.Render` - Pass 2)**:
   * Sets `isTranslucentPass = 1`, `DepthMask = false`, `Blend = true` (`GL.BlendFunc(SrcAlpha, OneMinusSrcAlpha)`).
   * Renders water and portal quads smoothly blended over opaque geometry without clipping.
4. **Pass 3: Entities (`MinecartRenderer.Render`)**:
   * Renders active minecart models with matching lighting and texture atlas.
5. **Pass 4: User Interface (`UIManager.Render` & `UIRenderer`)**:
   * Renders custom OpenGL 2D overlays:
     * Interactive Health, Oxygen, and Hotbar status indicators.
     * Reticle crosshair and hand-held item rendering.
     * Full interactive screens: Inventory, Crafting Table (3x3), Furnace (smelting), Chest / Double-Chest, Pause Menu with persistent sliders, and Victory Screen.

---

## 6. Shutdown & Disposal Flow

When the window closes (`OnUnload` / `Dispose`):
1. Background worker threads receive cancellation signals and are cleanly joined.
2. `World.Destroy()` unloads and deletes all OpenGL VBOs and VAOs from GPU memory.
3. Disposes any pending chunk meshes left in `readyToUploadMeshes` to return rented arrays to `ArrayPool`.
4. Texture atlas and shader programs are deleted via `GL.DeleteProgram` and `GL.DeleteTexture`.
5. `AudioManager.Dispose()` frees OpenAL sources, cached buffers, context, and audio device.
6. `UIRenderer.Delete()` frees UI shader program, font textures, and 2D quad buffers.
