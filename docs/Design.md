# VelmoraCraft — Engine Architecture & Design Decisions

This document provides a comprehensive technical overview of the architecture and key design decisions powering
VelmoraCraft — a high-performance 3D voxel sandbox engine built in C# and .NET with OpenTK (OpenGL bindings). It details
why specific approaches were chosen, alternative designs considered, and the optimizations applied to achieve high frame
rates, low memory consumption, and rich gameplay systems.

---

## 1. Chunk Storage & Voxel Data Representation

* **Chosen Design**: Fixed-size chunks ($16 \times 128 \times 16$) backed by a flat 1D byte array
  (`byte[SIZE_X * SIZE_Y * SIZE_Z]`) indexed via `x + (z * SIZE_X) + (y * SIZE_X * SIZE_Z)` ($32\text{ KB}$ per chunk).
* **Why**:
    * Provides $O (1)$ random lookups with optimal cache line utilization.
    * Flat memory structures avoid object-header and multidimensional bounds-check overheads in .NET.
    * $32\text{ KB}$ fits cleanly within modern CPU L1/L2 data caches, making chunk iteration during meshing and physics
      extremely fast.
* **Alternative Approaches Considered**:
    * *3D Array (`byte[,,]`)*: Introduces multidimensional bounds-checking overheads and multiple reference dereferences
      in C#.
    * *Sparse Octrees / RLE*: Highly memory-efficient for vast empty air, but adds significant traversal overhead during
      meshing, raycasting, and frequent block manipulation.
* **Optimization**:
    * Chunks beyond the active view distance are unmeshed, keeping only raw byte data in memory, while their OpenGL GPU
      buffers (VAO, VBO, IBO) are promptly freed.

---

## 2. Rendering Pipeline & Interleaved Vertex Buffers

* **Chosen Design**: Single interleaved VBO per chunk utilizing a packed 44-byte vertex struct (`ChunkVertex`: 3 floats
  position, 2 floats UV, 3 floats tint color, 3 floats normal) bound to a single VAO.
* **Why**:
    * Reduces OpenGL buffer handles by **75%** (from 1,024 handles down to 256 for a 256-chunk world).
    * Dramatically reduces driver overhead and cache line misses by streaming contiguous vertex attributes directly to
      the GPU in a single `glBufferData` call.
* **Alternative Approaches Considered**:
    * *Multiple VBOs per chunk* (positions, UVs, colors, normals separately): Triples GPU API call count and fragments
      memory bandwidth.
    * *Bit-packed integer attributes* (e.g. 1 uint per vertex): Saves memory but adds shader unpacking instructions. The
      44-byte float vertex provides an ideal balance between simplicity, precision, and GPU alignment.
* **Optimization & Multi-Pass Execution**:
    * **Pass 1 (Opaque)**: All solid terrain blocks are drawn first (`DepthMask=true`, blending disabled,
      `DepthFunc=Less`). Solid geometry beneath water writes to the depth buffer first.
    * **Pass 2 (Translucent)**: Water, ice, and portal surfaces are drawn in a dedicated secondary pass
      (`DepthMask=false`, `GL.BlendFunc(SrcAlpha, OneMinusSrcAlpha)`, `DepthFunc=Lequal`), eliminating alpha clipping
      and depth sorting artifacts.
    * **Frustum & Distance Culling**: Chunks outside the camera's 3D viewing frustum or beyond the view distance are
      culled CPU-side before issuing draw calls.

---

## 3. Chunk Meshing & Zero-Allocation Buffer Pooling

* **Chosen Design**: Hidden-face culling checking adjacent neighbors within and across chunk boundaries, coupled with
  thread-local mesh builders (`ThreadLocal<ChunkMeshBuilder>`) and array pooling via `ArrayPool<T>.Shared`.
* **Why**:
    * In .NET, any object array larger than $85,000$ bytes ($\approx 1,931$ `ChunkVertex` elements) is immediately
      placed on the **Large Object Heap (LOH)**. LOH allocations trigger high-generation GC pauses, cause heap
      fragmentation, and are not freed back to the OS promptly.
    * Rented arrays from `ArrayPool<ChunkVertex>.Shared` and `ArrayPool<uint>.Shared` are returned immediately upon GPU
      upload via `ChunkMesh.Dispose()`.
    * Reusing mesh builder lists per thread prevents managed heap churn during active world exploration.
* **Alternative Approaches Considered**:
    * *Per-mesh heap allocations (`new List<T>()` or `Vertices.ToArray()`)*: Previously inflated memory to 500MB because
      arrays continuously landed in LOH.
    * *Greedy Meshing*: Combines adjacent identical faces into single large quads. While it produces fewer vertices, it
      complicates texture tiling, per-voxel lighting, and texture animation without significant performance gain for
      dense block varieties.
* **Optimization**:
    * Cross-chunk boundary neighbor lookups ensure chunk borders seamlessly connect without missing edge faces.

---

## 4. Multithreaded World Streaming & Worker Pipeline

* **Chosen Design**: Dedicated background worker threads consuming from a thread-safe `ConcurrentQueue<Vector2i>`,
  outputting complete meshes to a `readyToUploadMeshes` queue with upload throttling.
* **Why**:
    * Noise generation (FastNoiseLite 3D Simplex) and meshing are CPU-intensive. Offloading them to background threads
      guarantees a smooth, unblocked 60+ FPS main render thread.
* **Alternative Approaches Considered**:
    * *Synchronous Main Thread Meshing*: Causes noticeable hitching and frame drops during player movement.
    * *Task.Run / ThreadPool*: Unbounded task spawning can overwhelm the thread pool, starving audio and OS tasks. Fixed
      worker threads (`Math.Max(2, Environment.ProcessorCount - 1)`) provide predictable CPU scheduling.
* **Optimization**:
    * **Duplicate Enqueue Prevention**: `queuedChunks` set tracking and `chunk.IsGeneratingMesh = true` prevent
      redundant enqueues while chunks are being generated or waiting for GPU upload.
    * **Worker Throttling**: Workers pause if `readyToUploadMeshes.Count >= 24`, preventing background threads from
      building a backlog of unuploaded chunk meshes in memory.
    * **Priority Sorting**: Chunks closest to the camera are sorted and meshed first.
    * **Frame Budget Upload Limit**: Throttles uploads to 6 chunks per frame to prevent stuttering.

---

## 5. Procedural World Generation & Multi-Octave Noise

* **Chosen Design**: Multi-layered procedural generation driven by FastNoiseLite with deterministic world seeds:
    * **Base Elevation Noise**: OpenSimplex2 fractal FBm noise creates rolling terrain and realistic continental
      shelves.
    * **Continental & Mountain Noise**: Large elevation gradients produce dramatic mountain ranges with peaks reaching
      above Y=70.
    * **3D Cave System**: Dual-noise 3D OpenSimplex2 generates interconnected cavern networks, tunnels, and subterranean
      lava lakes.
    * **Dynamic Biomes**: Temperature and moisture noise fields classify terrain dynamically into:
        * **Plains**: Meadow grasses, tall grass, red roses, and yellow dandelions.
        * **Forest**: Dense oak and birch trees with shaded dirt undergrowth and mushroom patches.
        * **Desert**: Rolling sand dunes, sandstone bedrock, cacti, and sugar cane riverbanks.
        * **Mountains**: Rugged stone peaks, snowy grass, spruce pine forests, and high-altitude mountain camps.

---

## 6. Procedural World Structures & Region-Grid Rarity System

Structures use a deterministic **Region-Grid Partitioning Algorithm** (`IsRegionCandidateChunk`) combined with
biome/elevation filters. This guarantees:

* **No Clustering or Overlaps**: Structures of the same type can never generate in adjacent chunks. Each region has
  exactly one candidate chunk.
* **Rewarding Discovery**: Structures are rare enough that discovering one feels like a true achievement, yet common
  enough that an intrepid explorer is guaranteed to encounter multiple structures during a playthrough.

1. **Small Villages** (Plains & Forest Biomes):
    * **Rarity**: 1 candidate per 13x13 chunk region (~208m spacing, ~12–15 villages across an entire 64x64 world).
    * **Central Town Well**: Cobblestone rim, deep water basin, brick pillars, canopy, and illumination.
    * **Residential Cottage**: Cobblestone foundation, brick walls, glass windows, wooden door, plank flooring,
      bookshelves, crafting table, and village food chest.
    * **Blacksmith Workshop**: Cobblestone & stone structure with open front, burning lit furnace, standard furnace,
      brick chimney, iron block anvil, cooling water trough, and blacksmith chest (iron, coal, gold, obsidian, flint and
      steel).
    * **Farmland Garden**: Bordered plot with central irrigation canal, tilled farmland, and wheat crops.
    * **Cobblestone Pathways**: Natural cobblestone paths connecting the well, cottages, and farmland.
2. **Surface Railway & Mineshaft Depot** (Above Sea Level):
    * **Rarity**: 1 candidate per 11x11 chunk region (~176m spacing, ~16–20 across an entire world).
    * **Staging Platform**: Deck with log pillars, plank canopy, crafting table, and furnace.
    * **Depot Chest**: Stocked with minecarts, powered rails, rails, coal, and iron ingots.
    * **Scenic Railway**: 24-block railway system with trestle bridge supports over terrain dips, powered rail boosters,
      and redstone torch power.
    * **Interactive Entity**: Automatically spawns an interactive, rideable `Minecart` entity on the track.
3. **Ancient Ruins & Shrine**:
    * **Rarity**: 1 candidate per 10x10 chunk region (~160m spacing, ~20–25 across an entire world).
    * Sunken weathered stone floor (mossy cobblestone, stone, cobblestone).
    * Weathered corner pillars of varying heights and broken stone arches.
    * Obsidian altar pedestal holding an ancient reliquary chest with rare treasures (diamond ore, gold block, obsidian,
      bookshelves, cake, flint and steel).
4. **Desert Wells** (Desert Biome):
    * **Rarity**: 1 candidate per 8x8 chunk region (~128m spacing, ~6–8 wells across desert biomes in a world).
    * Sandstone and brick foundation with a central 3x3 water basin.
    * Four brick support pillars holding a sandstone & brick canopy roof.
    * Hidden explorer's chest containing gold ore, sandstone, bread, and torches.
5. **Mountain Tent Camps** (Mountains Biome, Y >= 50):
    * **Rarity**: 1 candidate per 8x8 chunk region on high mountain peaks (~4–6 camps on the highest summits in a
      world).
    * Leveled cobblestone mountain plateau.
    * Wool A-frame tent (colored red, blue, or white depending on seed) with wood plank interior.
    * Explorer's supply chest with bread, cake, coal, iron, and seeds.
    * Outdoor workstation with crafting table, furnace, campfire ring with torch flame, and mountain torches.
6. **Underground Mini-Mineshafts** (Subterranean, Y: 18–31):
    * **Rarity**: 1 candidate per 8x8 chunk region (~128m spacing, ~40–50 across the subterranean depths).
    * Carved 3x3 tunnel corridors with plank/cobblestone ballasts.
    * Timber support arches made of natural logs and overhead planks with mounted torches.
    * Central rail tracks, atmospheric hanging cobwebs, and miner loot chests (iron ore, gold ore, redstone ore, coal,
      rails, powered rails).

---

## 7. Botanical, Crop & Ecological Simulation

VelmoraCraft includes a dynamic real-time ecological simulation running on the game loop:

* **Leaf Decay System**:
    * When all logs supporting a tree are mined, connected/surrounding leaves detect the absence of supporting trunk
      logs within distance 4.
    * Orphaned leaves schedule an individual decay timer between **1 and 2 in-game minutes** (60 to 120 seconds).
    * As timers expire, leaves disappear naturally with a ~10% probability of dropping an `OakSapling`.
* **Tree Sapling Growth**:
    * When planted on dirt, grass, or snowy grass with vertical clearance, `OakSapling` blocks grow into full trees
      after **5 to 6 in-game minutes** (300 to 360 seconds).
    * The grown tree type dynamically adapts to the local biome (Spruce in Mountains, Birch or Oak in Forests/Plains).
* **Wheat Crop Growth**:
    * Wheat crops planted on tilled farmland advance smoothly across 7 growth stages to full golden maturity over **3 to
      4 in-game minutes** (180 to 240 seconds, ~26–34 seconds per stage).
    * Fully mature wheat yields harvested Wheat items plus 1–2 Wheat Seeds for replanting.
* **Dirt-to-Grass Spread**:
    * Placed dirt blocks with clear space above and neighboring grass blocks convert into grass blocks over **2 to 3
      in-game minutes** (120 to 180 seconds, fast-forwarded from initial multi-day cycles).
* **Nocturnal Cobweb Spawning**:
    * During Dusk and Night phases, trees in loaded chunks periodically spawn atmospheric cobwebs under leaf canopies
      with spatial density limits.

---

## 8. Coordinates & Navigation System

* **Compact HUD Coordinates Badge**:
    * Always displays real-time `XYZ` player block coordinates, cardinal facing direction (`North (-Z)`, `South (+Z)`,
      `East (+X)`, `West (-X)`), and active biome name in the top-left corner.
* **F3 Debug Screen**:
    * Toggleable via the `F3` key.
    * Displays full Minecraft-style diagnostics:
        * Engine version.
        * Exact sub-block float coordinates (`XYZ: X.XX / Y.YY / Z.ZZ`).
        * Block coordinates and chunk coordinates with sub-chunk indices `[Chunk X, Z in sub X, Z]`.
        * Facing direction with cardinal vector and exact camera Yaw/Pitch angles.
        * Active biome name.
        * Day/Time of day and lighting parameters.
        * Movement mode (Creative Flying vs. Survival Walking) and health/stamina.
        * Render distance and field of view.

---

## 9. Dynamic Lighting, Point Lights & Fluid Animation

* **Dynamic Day/Night Cycle**:
    * Real-time celestial calculations transition ambient light, directional sun/moon light, and sky colors smoothly
      from dawn to daytime, dusk, and night.
* **Dynamic Point Lights & Spatial Query**:
    * Local light sources (placed torches, burning furnaces, handheld torches, lava, glowing jack-o'-lanterns, and
      nether portals) are spatially queried within a 48-block radius.
    * Spatial deduplication clusters multi-block portals into single coherent emitters.
    * Up to 15 nearby point lights are uploaded to uniform arrays in `Default.frag` per frame.
* **Self-Luminous Shading**:
    * Luminous blocks (torches, portals, glowstone, lit furnaces) receive vertex tint markers (`r > 1.25`) that trigger
      full self-illumination in the fragment shader, remaining vivid in pitch-black darkness.
* **Fluid Animation (Water & Lava)**:
    * Water and lava blocks feature smooth UV texture cycling transitions in `Default.vert` and `Default.frag`.
    * Connected sinusoidal vertex wave displacement gives natural rolling waves to water bodies and lava pools.

---

## 10. Raycasting & Block Selection (DDA)

* **Chosen Design**: 3D Amanatides-Woo Fast Voxel Traversal (Digital Differential Analyzer / DDA).
* **Why**: Traverses voxel grid cells along a ray in exact geometric order without missing narrow edges or corners,
  running in $O (N)$ steps where $N$ is ray length.
* **Alternative Approaches Considered**:
    * *Raymarching with small fixed steps*: Inaccurate; can step through block corners or require high step counts that
      waste CPU cycles.
    * *Physics collider queries*: Heavy overhead for simple voxel grids.

---

## 11. Fluid Expansion System

* **Chosen Design**: Deterministic cellular expansion with a hard Manhattan/Chebyshev distance limit of 8 blocks from
  source.
* **Why**: Provides responsive, authentic water and lava flows that settle into static equilibrium, avoiding runaway
  simulation loops or infinite world flooding.
* **Alternative Approaches Considered**:
    * *Full Navier-Stokes / SPH*: Extreme computational cost inappropriate for a block-based voxel game.
    * *Dynamic Cellular Automata*: Ongoing tick-based updates across all loaded chunks can cause CPU bottlenecks as the
      world grows.
* **Optimization**: Reuses a static cardinal offset array and clears the visited set when the queue empties to eliminate
  runtime heap allocations.

---

## 12. Player Physics, Collision & Survival Mechanics

* **Chosen Design**: Swept Axis-Aligned Bounding Box (AABB) resolving velocity along $X$, $Y$, and $Z$ axes
  sequentially, combined with a survival state controller (fall damage, drowning, lava contact, and passive healing).
* **Why**: Axis-separated resolution prevents tunneling and allows smooth sliding along walls and up step-heights
  without physics library bloat.
* **Alternative Approaches Considered**:
    * *Full Rigidbody Physics (Bullet/PhysX)*: Unnecessary complexity and prone to voxel jitter or corner catching.
* **Optimization & Mechanics**:
    * **Memory**: Collision candidate box lists are preallocated and reused per frame instead of reallocated on every
      movement tick.
    * **Fall Damage**: Damage is calculated based on vertical fall distance, triggering only when falling $\ge 3$ blocks
      to prevent damage on normal jumps.
    * **Drowning & Lava**: Drowning checks head submersion; lava deals high-rate fire damage.
    * **Passive Healing**: Automatically recovers health after 10 seconds of taking no damage.

---

## 13. Minecart Entity Physics & Thread-Safe System

* **Chosen Design**: Dynamic `Minecart` entities with custom rail-following physics, speed boosting on powered rails,
  slope climbing, and player riding support.
* **Thread Safety**:
    * Background chunk generation enqueues new minecarts to a thread-safe `ConcurrentQueue<Minecart>`.
    * The main thread drains the queue in `World.Update` under `lock (Minecarts)`.
    * `MinecartRenderer.Render` utilizes `lock (minecarts)` with an index-based `for` loop to prevent concurrent
      modification exceptions during multithreaded world exploration.

---

## 14. Audio Subsystem & Fallbacks

* **Chosen Design**: Low-latency OpenAL Sound System with procedural dynamic synthesizers for footstep, block break,
  ambient, water, and minecart sounds.
* **Why**: Zero external asset dependencies, low memory footprint, and immunity to missing file paths or uncompressed
  disk streaming overhead.
* **Alternative Approaches Considered**:
    * *Compressed Audio Assets (OGG/MP3)*: Requires disk decoding, asset caching, and file I/O management.
* **Optimization**: Dynamic soft clamping and band-limited noise shaping prevent harsh audio artifacts and clipping.

---

## 15. Managed & Working Set Memory Optimization

* **Zero-Allocation Game Loop**:
    * Cached list buffers for `visibleChunks`, `toEnqueueChunks`, `activePointLights`, and `lightCandidates` prevent Gen
      0 GC churn at 60–120 FPS.
* **ArrayPool & LOH Elimination**:
    * Meshing uses `ArrayPool<T>.Shared` to pass geometry spans from background workers to OpenGL, completely bypassing
      the Large Object Heap.
* **One-Time Startup Memory Trim**:
    * Once initial generation settles,
      `GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce` and
      `GC.Collect(2, GCCollectionMode.Aggressive, true, true)` are invoked to compact memory and return uncommitted
      virtual memory pages back to the operating system.

---

## 16. Complete Atlas Block Registry & Multi-Face Mapping

* **Chosen Design**: Unified 64-entry block addition utilizing the $16 \times 16$ tile texture atlas
  (`Textures/atlas.png`), supporting Standard Cube, Cross-Quad flora (`MeshType.CrossQuad`), and transparent
  foliage/ice.
* **Why**:
    * Enables full visual parity with classic voxel survival worlds without additional texture assets or memory bloat.
    * Direct UV coordinate mapping (`GetUV(col, row)`) with normalized atlas coordinates keeps meshing overhead
      negligible.
    * Directional front/side/top/bottom face configurations allow realistic orientation for Furnaces, Dispensers,
      Pumpkins, Jack-o'-Lanterns, Cake, TNT, Farmland, and Bookshelves.

---

## 17. Stratified Ore & Biome Decoration Spawning

* **Chosen Design**: Height-band stratified ore distribution with 3D noise variation, coupled with biome-aware surface
  decoration.
* **Why**:
    * Realistic geological distribution: Coal spans wide depths ($y \in [5, 115]$), Iron mid-depths ($y \in [5, 75]$),
      Gold deeper ($y \in [5, 36]$), and Lapis/Redstone/Diamond clustered near bedrock ($y \in [1, 20]$).
    * Biomes feature distinctive foliage: Deserts spawn Sandstone strata and Cacti; snowy mountain peaks ($y > 90$)
      feature Snow Blocks and Spruce Pines; forests generate mixed Oak and Birch trees; shorelines spawn Sugar Cane and
      Clay beds; deep underground caves generate Mossy Cobblestone dungeon rooms with Monster Spawners.

---

## 18. Station-Based Crafting & Smelting Architecture

* **Chosen Design**: Separation of recipes by functional workstation (`RecipeStation.Personal`,
  `RecipeStation.CraftingTable`, `RecipeStation.Furnace`).
* **Why**:
    * Adheres to authentic survival progression:
        1. **Personal Inventory (`E`)**: Restricted to basic 2x2 survival recipes (Planks, Crafting Table, Torches,
           Buttons, Levers, Sandstone, Snow Blocks, White Wool).
        2. **Crafting Table (Interactive Block)**: Right-clicking a placed `CraftingTable` in the world opens the full
           3x3 Workbench station, unlocking architectural elements (Chests, Furnaces, Doors, Ladders, Bookshelves,
           Rails, TNT, Dispenser, Jukebox, Cake) and full 16-color wool dyeing.
        3. **Furnace (Interactive Block)**: Smelting recipes (Iron Ingot, Glass, Stone, Smooth Stone, Bricks, Charcoal,
           Glowstone) are strictly prohibited from personal inventory and require right-clicking a placed `Furnace` or
           `LitFurnace`, consuming 1 Coal fuel per operation.
* **Direct Mineral Compression**:
    * Minerals operate directly between ores/ingots and solid mineral blocks
      ($9\times\text{ Ore} \leftrightarrow 1\times\text{ Block}$) without needing loose intermediate dust items.

---

## 19. Custom Immediate-Mode UI & Font Renderer Engine

* **Chosen Design**: Dedicated `UIRenderer` and bitmap `FontRenderer` built entirely in OpenGL 3.3 without external
  dependencies (e.g. Dear ImGui).
* **Why**:
    * Eliminates third-party native wrapper DLLs and reduces distribution size.
    * Allows pixel-accurate retro aesthetics matching classic Minecraft Alpha UI.
    * Handles dynamic drag-and-drop item stacks, hotbar switching, multi-chest synchronization, furnace smelting
      progress, and custom tooltips.
* **Optimization**:
    * Batching of textured and untextured 2D quads with single-call VBO uploads per frame.
    * ASCII glyph atlas baked into texture memory with $O (1)$ UV lookups.

---

## 20. Directional Orientation System & Dynamic Door Meshing

* **Chosen Design**: 4-way cardinal metadata orientation (`BlockFacing`: South=0, North=1, West=2, East=3) stored per
  block and mapped directly into chunk metadata dictionaries.
* **Why**:
    * Supports authentic directional placement for Furnaces, Dispensers, Carved Pumpkins, Chests, Rails, Beds, and Doors
      based on player horizontal look vector (`Camera.Forward`).
* **Door Mechanics & Dynamic Meshing**:
    * Wooden and Iron doors occupy a 2-block vertical volume. When placed, both upper and lower blocks inherit the
      cardinal facing.
    * `AddDoor` in `ChunkMesher` generates dual-sided quad geometry for each facing:
        * **Closed**: Aligned with the doorframe, recessed slightly into the block along the facing axis.
        * **Open**: Rotated 90° inward against the adjoining wall frame.
    * Player interaction or Redstone signals (`RedstoneSystem.ToggleDoor`) flip the open/closed state while strictly
      preserving the door's facing metadata.
    * Shift + Right Click allows instant in-place rotation cycling through all 4 orientations.

---

## 21. World Persistence, Options & Instant Block Restoration (`SaveSystem`)

* **Chosen Design**: JSON-based dual-tier persistence: global `saves/options.json` for engine settings, and
  world-specific save files storing player stats, inventories, modified blocks, metadata, and active simulation timers.
* **Why**:
    * **Global Settings**: Options adjusted in the pause menu (Flight, Fullscreen, HUD, Render Distance slider, FOV
      slider) immediately write to `options.json` and persist across application restarts.
    * **Immediate Block Visibility**: `World` pre-populates modified blocks into chunk generation maps before meshing,
      and `ApplyLoadedData` enqueues all modified chunk coordinates so player modifications appear on screen instantly
      without requiring player interaction.
* **Optimization**:
    * Delta serialization: only user-modified or broken blocks are serialized, keeping save file sizes tiny
      ($< 100\text{ KB}$).
