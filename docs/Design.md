# Voxel Render Engine — Architecture & Design Decisions

This document outlines key technical decisions made in the Voxel Render Engine, why each approach was chosen, alternative architectures considered, and the optimizations applied to achieve high frame rates with low resource consumption.

---

## 1. Chunk Storage & Voxel Data Representation

* **Chosen Design**: Fixed-size chunks ($16 \times 128 \times 16$) backed by a flat 1D byte array (`byte[SIZE_X * SIZE_Y * SIZE_Z]`) indexed via `x + (z * SIZE_X) + (y * SIZE_X * SIZE_Z)` ($32\text{ KB}$ per chunk).
* **Why**: Provides $O(1)$ random lookups with optimal cache line utilization. Flat memory structures avoid object-header and bounds-check overheads in .NET.
* **Alternative Approaches**:
  * *3D Array (`byte[,,]`)*: Introduces multiple bounds-check overheads and reference dereferences in C#.
  * *Sparse Octrees / RLE*: Highly memory-efficient for sparse air, but adds significant traversal overhead during meshing and block manipulation.
* **Optimization**: Chunks beyond view distance are unmeshed, keeping only raw byte data in memory, while their OpenGL GPU buffers are freed.

---

## 2. Rendering Pipeline & Interleaved Vertex Buffers

* **Chosen Design**: Single interleaved VBO per chunk utilizing a packed 44-byte vertex struct (`ChunkVertex`: 3 floats position, 2 floats UV, 3 floats tint color, 3 floats normal) bound to a single VAO.
* **Why**:
  * Reduces OpenGL buffer handles by **75%** (from 1,024 handles down to 256 for a 256-chunk world).
  * Dramatically reduces driver overhead and cache line misses by streaming contiguous vertex attributes directly to the GPU in a single `glBufferData` call.
* **Alternative Approaches**:
  * *Multiple VBOs per chunk* (positions, UVs, colors, normals separately): Triples GPU API call count and fragments memory.
  * *Bit-packed integer attributes* (e.g. 1 uint per vertex): Saves memory but adds shader unpacking instructions. 44-byte float vertex provides the ideal balance between simplicity, precision, and GPU alignment.
* **Optimization**:
  * **Two-Pass Rendering**: Opaque blocks are rendered first (`DepthMask=true`, blending disabled), followed by water and translucent portal surfaces (`DepthMask=false`, `GL.BlendFunc`). This fixes transparency artifacts and renders blocks beneath water correctly.
  * **Horizontal Frustum & Distance Culling**: Chunks behind the camera or outside view distance are culled before issuing draw calls.

---

## 3. Chunk Meshing & Zero-Allocation Buffer Pooling

* **Chosen Design**: Hidden-face culling checking adjacent neighbors within and across chunk boundaries, coupled with thread-local mesh builders (`ThreadLocal<ChunkMeshBuilder>`) and array pooling via `ArrayPool<T>.Shared`.
* **Why**:
  * In .NET, any object array larger than $85,000$ bytes ($\approx 1,931$ `ChunkVertex` elements) is immediately placed on the **Large Object Heap (LOH)**. LOH allocations trigger high-generation GC pauses, cause heap fragmentation, and are not freed back to the OS promptly.
  * Rented arrays from `ArrayPool<ChunkVertex>.Shared` and `ArrayPool<uint>.Shared` are returned immediately upon GPU upload via `ChunkMesh.Dispose()`.
  * Reusing mesh builder lists per thread prevents managed heap churn during active world exploration.
* **Alternative Approaches**:
  * *Per-mesh heap allocations (`new List<T>()` or `Vertices.ToArray()`)*: Previously inflated memory to 500MB because arrays continuously landed in LOH.
  * *Greedy Meshing*: Combines adjacent identical faces into single large quads. While it produces fewer vertices, it complicates texture tiling and per-voxel lighting without significant performance gain for dense block varieties.
* **Optimization**: Cross-chunk boundary neighbor lookups ensure chunk borders seamlessly connect without missing edge faces.

---

## 4. Multithreaded World Streaming & Worker Pipeline

* **Chosen Design**: Dedicated background worker threads consuming from a thread-safe `ConcurrentQueue<Vector2i>`, outputting complete meshes to a `readyToUploadMeshes` queue with upload throttling.
* **Why**: Noise generation (FastNoiseLite 3D Simplex) and meshing are CPU-intensive. Offloading them to background threads guarantees a smooth, unblocked 60+ FPS main render thread.
* **Alternative Approaches**:
  * *Synchronous Main Thread Meshing*: Causes noticeable hitching and frame drops during player movement.
  * *Task.Run / ThreadPool*: Unbounded task spawning can overwhelm the thread pool, starving audio and OS tasks. Fixed worker threads (`Math.Max(2, Environment.ProcessorCount - 1)`) provide predictable CPU scheduling.
* **Optimization**:
  * **Duplicate Enqueue Prevention**: `queuedChunks` set tracking and `chunk.IsGeneratingMesh = true` prevent redundant enqueues while chunks are being generated or waiting for GPU upload.
  * **Worker Throttling**: Workers pause if `readyToUploadMeshes.Count >= 24`, preventing background threads from building a backlog of unuploaded chunk meshes in memory.
  * **Priority Sorting**: Chunks closest to the camera are sorted and meshed first.
  * **Frame Budget Upload Limit**: Throttles uploads to 6 chunks per frame to prevent stuttering.

---

## 5. Raycasting & Block Selection (DDA)

* **Chosen Design**: 3D Amanatides-Woo Fast Voxel Traversal (Digital Differential Analyzer / DDA).
* **Why**: Traverses voxel grid cells along a ray in exact geometric order without missing narrow edges or corners, running in $O(N)$ steps where $N$ is ray length.
* **Alternative Approaches**:
  * *Raymarching with small fixed steps*: Inaccurate; can step through block corners or require high step counts that waste CPU cycles.
  * *Physics collider queries*: Heavy overhead for simple voxel grids.

---

## 6. Fluid Expansion System

* **Chosen Design**: Deterministic cellular expansion with a hard Manhattan/Chebyshev distance limit of 8 blocks from source.
* **Why**: Provides responsive, authentic water and lava flows that settle into static equilibrium, avoiding runaway simulation loops or infinite world flooding.
* **Alternative Approaches**:
  * *Full Navier-Stokes / SPH*: Extreme computational cost inappropriate for a block-based voxel game.
  * *Dynamic Cellular Automata*: Ongoing tick-based updates across all loaded chunks can cause CPU bottlenecks as the world grows.
* **Optimization**: Reuses a static cardinal offset array and clears the visited set when the queue empties to eliminate runtime heap allocations.

---

## 7. Player Physics, Collision & Survival Mechanics

* **Chosen Design**: Swept Axis-Aligned Bounding Box (AABB) resolving velocity along $X$, $Y$, and $Z$ axes sequentially, combined with a survival state controller (fall damage, drowning, lava contact, and passive healing).
* **Why**: Axis-separated resolution prevents tunneling and allows smooth sliding along walls and up step-heights without physics library bloat.
* **Alternative Approaches**:
  * *Full Rigidbody Physics (Bullet/PhysX)*: Unnecessary complexity and prone to voxel jitter or corner catching.
* **Optimization & Mechanics**:
  * **Memory**: Collision candidate box lists are preallocated and reused per frame instead of reallocated on every movement tick.
  * **Fall Damage**: Damage is calculated based on vertical fall distance, triggering only when falling $\ge 3$ blocks to prevent damage on normal jumps.
  * **Drowning & Lava**: Drowning checks head submersion; lava deals high-rate fire damage.
  * **Passive Healing**: Automatically recovers health after 10 seconds of taking no damage.

---

## 8. Audio Subsystem & Fallbacks

* **Chosen Design**: Low-latency OpenAL Sound System with procedural dynamic synthesizers for footstep, block break, ambient, water, and minecart sounds.
* **Why**: Zero external asset dependencies, low memory footprint, and immunity to missing file paths or uncompressed disk streaming overhead.
* **Alternative Approaches**:
  * *Compressed Audio Assets (OGG/MP3)*: Requires disk decoding, asset caching, and file I/O management.
* **Optimization**: Dynamic soft clamping and band-limited noise shaping prevent harsh audio artifacts and clipping.

---

## 9. Managed & Working Set Memory Optimization

* **Zero-Allocation Game Loop**:
  * Cached list buffers for `visibleChunks`, `toEnqueueChunks`, `activePointLights`, and `lightCandidates` prevent Gen 0 GC churn at 60–120 FPS.
* **ArrayPool & LOH Elimination**:
  * Meshing uses `ArrayPool<T>.Shared` to pass geometry spans from background workers to OpenGL, completely bypassing the Large Object Heap.
* **One-Time Startup Memory Trim**:
  * Once initial generation settles, `GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce` and `GC.Collect(2, GCCollectionMode.Aggressive, true, true)` are invoked to compact memory and return uncommitted virtual memory pages back to the operating system.

---

## 10. Complete Atlas Block Registry & Multi-Face Mapping

* **Chosen Design**: Unified 64-entry block addition utilizing the $16 \times 16$ tile texture atlas (`Textures/atlas.png`), supporting Standard Cube, Cross-Quad flora (`MeshType.CrossQuad`), and transparent foliage/ice.
* **Why**:
  * Enables full visual parity with classic voxel survival worlds without additional texture assets or memory bloat.
  * Direct UV coordinate mapping (`GetUV(col, row)`) with normalized atlas coordinates keeps meshing overhead negligible.
  * Directional front/side/top/bottom face configurations allow realistic orientation for Furnaces, Dispensers, Pumpkins, Jack-o'-Lanterns, Cake, TNT, Farmland, and Bookshelves.
* **Lighting Integration**:
  * Blocks marked `IsLightSource = true` (Lava, Portal, Glowstone, Jack-o'-Lantern, Lit Furnace, Redstone Torch, Redstone Ore) automatically feed into the dynamic point-light system with custom hues and attenuation radii.

---

## 11. Stratified Ore & Biome Decoration Spawning

* **Chosen Design**: Height-band stratified ore distribution with 3D noise variation, coupled with biome-aware surface decoration.
* **Why**:
  * Realistic geological distribution: Coal spans wide depths ($y \in [5, 115]$), Iron mid-depths ($y \in [5, 75]$), Gold deeper ($y \in [5, 36]$), and Lapis/Redstone/Diamond clustered near bedrock ($y \in [1, 20]$).
  * Biomes feature distinctive foliage: Deserts spawn Sandstone strata and Cacti; snowy mountain peaks ($y > 90$) feature Snow Blocks and Spruce Pines; forests generate mixed Oak and Birch trees; shorelines spawn Sugar Cane and Clay beds; deep underground caves generate Mossy Cobblestone dungeon rooms with Monster Spawners.

---

## 12. Station-Based Crafting & Smelting Architecture

* **Chosen Design**: Separation of recipes by functional workstation (`RecipeStation.Personal`, `RecipeStation.CraftingTable`, `RecipeStation.Furnace`).
* **Why**:
  * Adheres to authentic survival progression:
    1. **Personal Inventory (`E`)**: Restricted to basic 2x2 survival recipes (Planks, Crafting Table, Torches, Buttons, Levers, Sandstone, Snow Blocks, White Wool).
    2. **Crafting Table (Interactive Block)**: Right-clicking a placed `CraftingTable` in the world opens the full 3x3 Workbench station, unlocking architectural elements (Chests, Furnaces, Doors, Ladders, Bookshelves, Rails, TNT, Dispenser, Jukebox, Cake) and full 16-color wool dyeing.
    3. **Furnace (Interactive Block)**: Smelting recipes (Iron Ingot, Glass, Stone, Smooth Stone, Bricks, Charcoal, Glowstone) are strictly prohibited from personal inventory and require right-clicking a placed `Furnace` or `LitFurnace`, consuming 1 Coal fuel per operation.
* **Direct Mineral Compression**:
  * Minerals operate directly between ores/ingots and solid mineral blocks ($9\times\text{ Ore} \leftrightarrow 1\times\text{ Block}$) without needing loose intermediate dust items.


