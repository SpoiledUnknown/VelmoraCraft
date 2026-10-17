using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using VoxelRenderEngine.src.Entities;
using VoxelRenderEngine.src.Graphics;

namespace VoxelRenderEngine.src.World
{
    public class World
    {
        public static int ActiveWorldChunksX { get; private set; } = 64;
        public static int ActiveWorldChunksZ { get; private set; } = 64;
        public static int ActiveWorldSizeX { get; private set; } = 64 * Chunk.SIZE_X;
        public static int ActiveWorldSizeZ { get; private set; } = 64 * Chunk.SIZE_Z;

        public static int WORLD_CHUNKS_X => ActiveWorldChunksX;
        public static int WORLD_CHUNKS_Z => ActiveWorldChunksZ;
        public static int WORLD_SIZE_X => ActiveWorldSizeX;
        public static int WORLD_SIZE_Z => ActiveWorldSizeZ;

        public int WorldChunksX { get; }
        public int WorldChunksZ { get; }
        public int WorldChunks => WorldChunksX;
        public int WorldSizeX => WorldChunksX * Chunk.SIZE_X;
        public int WorldSizeZ => WorldChunksZ * Chunk.SIZE_Z;

        public int Seed { get; }
        public int ViewDistanceChunks { get; set; } = 8; // 8 chunks radius = 17x17 = 289 chunks active

        private readonly WorldGenerator generator;
        private readonly ConcurrentDictionary<Vector2i, Chunk> chunks = new ConcurrentDictionary<Vector2i, Chunk>();
        public readonly List<Minecart> Minecarts = new List<Minecart>();
        private readonly ConcurrentDictionary<Vector3i, byte> lightSources = new ConcurrentDictionary<Vector3i, byte>();
        public readonly ConcurrentDictionary<Vector3i, BlockType> ModifiedBlocks = new ConcurrentDictionary<Vector3i, BlockType>();
        public readonly ConcurrentDictionary<Vector3i, byte> BlockMetadataMap = new ConcurrentDictionary<Vector3i, byte>();
        public readonly FluidSystem FluidSystem = new FluidSystem();

        // Thread-safe queues for multithreaded terrain generation and meshing
        private readonly ConcurrentQueue<Vector2i> workQueue = new ConcurrentQueue<Vector2i>();
        private readonly ConcurrentQueue<(Vector2i coord, ChunkMesh mesh)> readyToUploadMeshes = new ConcurrentQueue<(Vector2i, ChunkMesh)>();
        private readonly HashSet<Vector2i> queuedChunks = new HashSet<Vector2i>();
        private readonly object queueLock = new object();

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Thread[] workerThreads;
        private readonly AutoResetEvent workAvailable = new AutoResetEvent(false);

        // Cached reusable collections to eliminate per-frame allocations
        private readonly List<Chunk> visibleChunks = new List<Chunk>(512);
        private readonly List<Chunk> visibleOpaqueChunks = new List<Chunk>(256);
        private readonly List<Chunk> visibleWaterChunks = new List<Chunk>(64);
        private Frustum cameraFrustum = new Frustum();
        public int TranslucentPassUniformLoc { get; set; } = -1;
        private Vector3 lastLightQueryPos = new Vector3(-9999f, -9999f, -9999f);
        private float lightQueryTimer = 0f;

        private readonly List<(Vector2i coord, int distSq)> toEnqueueChunks = new List<(Vector2i, int)>(128);
        private readonly List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> activePointLights = new List<(Vector3, Vector3, float, float)>(16);
        private readonly List<(Vector3i Pos, float DistSq)> lightCandidates = new List<(Vector3i Pos, float DistSq)>(64);
        private bool hasTrimmedStartupMemory = false;

        private float streamTimer = 0f;
        private Vector2i lastStreamedChunk = new Vector2i(-9999, -9999);

        // Farming and crop simulation
        public readonly HashSet<Vector3i> GrowingCrops = new HashSet<Vector3i>();
        private float cropGrowthTimer = 0f;

        public World(int seed = 1337, int worldChunks = 64, Vector2i? initialSpawnChunk = null)
        {
            Seed = seed;
            WorldChunksX = Math.Clamp(worldChunks, 8, 256);
            WorldChunksZ = Math.Clamp(worldChunks, 8, 256);
            ActiveWorldChunksX = WorldChunksX;
            ActiveWorldChunksZ = WorldChunksZ;
            ActiveWorldSizeX = WorldSizeX;
            ActiveWorldSizeZ = WorldSizeZ;

            generator = new WorldGenerator(seed);

            Vector2i spawnChunk = initialSpawnChunk ?? new Vector2i(WorldChunksX / 2, WorldChunksZ / 2);

            // 1. Immediately generate immediate 3x3 spawn chunks synchronously for instant ground collision
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    var coord = new Vector2i(spawnChunk.X + dx, spawnChunk.Y + dz);
                    if (IsChunkInsideWorld(coord))
                    {
                        var chunk = GetOrCreateChunk(coord);
                        if (!chunk.IsTerrainGenerated)
                        {
                            generator.GenerateChunkTerrain(chunk);
                            chunk.IsTerrainGenerated = true;
                        }
                    }
                }
            }

            // Decorate center spawn chunk
            var centerChunk = GetOrCreateChunk(spawnChunk);
            if (!centerChunk.IsDecorated)
            {
                generator.DecorateChunk(this, centerChunk);
                centerChunk.IsDecorated = true;
            }

            // 2. Start background worker threads for generation and meshing
            int threadCount = Math.Max(2, Environment.ProcessorCount - 1);
            workerThreads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                workerThreads[i] = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"ChunkWorker-{i}"
                };
                workerThreads[i].Start();
            }

            // Enqueue initial spawn area for meshing
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    var coord = new Vector2i(spawnChunk.X + dx, spawnChunk.Y + dz);
                    if (IsChunkInsideWorld(coord))
                    {
                        EnqueueChunk(coord);
                    }
                }
            }
        }

        public Chunk GetOrCreateChunk(Vector2i coord)
        {
            return chunks.GetOrAdd(coord, c => new Chunk(c));
        }

        public Chunk? GetChunk(Vector2i coord)
        {
            chunks.TryGetValue(coord, out var chunk);
            return chunk;
        }

        public static bool IsChunkInsideWorld(Vector2i coord)
        {
            return coord.X >= 0 && coord.X < WORLD_CHUNKS_X && coord.Y >= 0 && coord.Y < WORLD_CHUNKS_Z;
        }

        public static bool IsInsideWorld(int wx, int wz)
        {
            return wx >= 0 && wx < WORLD_SIZE_X && wz >= 0 && wz < WORLD_SIZE_Z;
        }

        public int GetSurfaceHeight(int wx, int wz)
        {
            if (!IsInsideWorld(wx, wz)) return 64;

            int cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
            int cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);
            var chunk = GetOrCreateChunk(new Vector2i(cx, cz));
            if (!chunk.IsTerrainGenerated)
            {
                generator.GenerateChunkTerrain(chunk);
                chunk.IsTerrainGenerated = true;
            }

            int lx = wx - (cx * Chunk.SIZE_X);
            int lz = wz - (cz * Chunk.SIZE_Z);

            for (int y = Chunk.SIZE_Y - 2; y >= 1; y--)
            {
                byte b = chunk.GetBlock(lx, y, lz);
                if (b != (byte)BlockType.Empty && b != (byte)BlockType.Water)
                {
                    return y;
                }
            }
            return 60;
        }

        public byte GetBlockWorld(int wx, int wy, int wz)
        {
            if (wy < 0 || wy >= Chunk.SIZE_Y) return (byte)BlockType.Empty;

            int cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
            int cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);

            if (chunks.TryGetValue(new Vector2i(cx, cz), out var chunk))
            {
                int lx = wx - (cx * Chunk.SIZE_X);
                int lz = wz - (cz * Chunk.SIZE_Z);
                return chunk.GetBlock(lx, wy, lz);
            }

            return (byte)BlockType.Empty;
        }

        public void SetBlockWorld(int wx, int wy, int wz, BlockType type, byte metadata = 0)
        {
            if (wy < 0 || wy >= Chunk.SIZE_Y) return;

            int cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
            int cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);
            var coord = new Vector2i(cx, cz);

            if (IsChunkInsideWorld(coord))
            {
                var chunk = GetOrCreateChunk(coord);
                int lx = wx - (cx * Chunk.SIZE_X);
                int lz = wz - (cz * Chunk.SIZE_Z);

                byte oldBlock = chunk.GetBlock(lx, wy, lz);
                var pos = new Vector3i(wx, wy, wz);
                if (BlockRegistry.Get(oldBlock).IsLightSource)
                {
                    lightSources.TryRemove(pos, out _);
                    chunk.LightPositions.Remove(pos);
                }

                chunk.SetBlock(lx, wy, lz, type);
                chunk.SetMetadata(lx, wy, lz, metadata);

                if (type == BlockType.Empty)
                {
                    ModifiedBlocks[pos] = type;
                    BlockMetadataMap.TryRemove(pos, out _);
                }
                else
                {
                    ModifiedBlocks[pos] = type;
                    if (metadata > 0)
                    {
                        BlockMetadataMap[pos] = metadata;
                    }
                    else
                    {
                        BlockMetadataMap.TryRemove(pos, out _);
                    }
                }

                if (BlockRegistry.Get(type).IsLightSource)
                {
                    lightSources.TryAdd(pos, 0);
                    if (!chunk.LightPositions.Contains(pos))
                    {
                        chunk.LightPositions.Add(pos);
                    }
                }

                EnqueueChunk(coord);

                // If block is on the edge of the chunk, also re-mesh adjacent chunk
                if (lx == 0 && cx > 0) EnqueueChunk(new Vector2i(cx - 1, cz));
                if (lx == Chunk.SIZE_X - 1 && cx < WORLD_CHUNKS_X - 1) EnqueueChunk(new Vector2i(cx + 1, cz));
                if (lz == 0 && cz > 0) EnqueueChunk(new Vector2i(cx, cz - 1));
                if (lz == Chunk.SIZE_Z - 1 && cz < WORLD_CHUNKS_Z - 1) EnqueueChunk(new Vector2i(cx, cz + 1));
            }
        }

        public byte GetBlockMetadata(int wx, int wy, int wz)
        {
            if (wy < 0 || wy >= Chunk.SIZE_Y) return 0;

            int cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
            int cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);

            if (chunks.TryGetValue(new Vector2i(cx, cz), out var chunk))
            {
                int lx = wx - (cx * Chunk.SIZE_X);
                int lz = wz - (cz * Chunk.SIZE_Z);
                return chunk.GetMetadata(lx, wy, lz);
            }

            return 0;
        }

        public void SetBlockMetadata(int wx, int wy, int wz, byte metadata)
        {
            if (wy < 0 || wy >= Chunk.SIZE_Y) return;

            int cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
            int cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);
            var coord = new Vector2i(cx, cz);

            if (IsChunkInsideWorld(coord))
            {
                var chunk = GetOrCreateChunk(coord);
                int lx = wx - (cx * Chunk.SIZE_X);
                int lz = wz - (cz * Chunk.SIZE_Z);

                chunk.SetMetadata(lx, wy, lz, metadata);
                var pos = new Vector3i(wx, wy, wz);
                if (metadata > 0)
                {
                    BlockMetadataMap[pos] = metadata;
                }
                else
                {
                    BlockMetadataMap.TryRemove(pos, out _);
                }

                EnqueueChunk(coord);

                if (lx == 0 && cx > 0) EnqueueChunk(new Vector2i(cx - 1, cz));
                if (lx == Chunk.SIZE_X - 1 && cx < WORLD_CHUNKS_X - 1) EnqueueChunk(new Vector2i(cx + 1, cz));
                if (lz == 0 && cz > 0) EnqueueChunk(new Vector2i(cx, cz - 1));
                if (lz == Chunk.SIZE_Z - 1 && cz < WORLD_CHUNKS_Z - 1) EnqueueChunk(new Vector2i(cx, cz + 1));
            }
        }

        public void EnqueueChunk(Vector2i coord)
        {
            if (!IsChunkInsideWorld(coord)) return;

            lock (queueLock)
            {
                if (queuedChunks.Add(coord))
                {
                    var chunk = GetOrCreateChunk(coord);
                    chunk.IsGeneratingMesh = true;
                    workQueue.Enqueue(coord);
                    workAvailable.Set();
                }
            }
        }

        private void WorkerLoop()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                // Throttling: If the upload queue has a backlog, wait briefly
                // so background workers don't run miles ahead and hoard memory
                if (readyToUploadMeshes.Count >= 24)
                {
                    Thread.Sleep(5);
                    continue;
                }

                if (workQueue.TryDequeue(out var coord))
                {
                    if (!IsChunkInsideWorld(coord))
                    {
                        lock (queueLock)
                        {
                            queuedChunks.Remove(coord);
                        }
                        continue;
                    }

                    var chunk = GetOrCreateChunk(coord);

                    // 1. Generate terrain if needed
                    if (!chunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(chunk);
                        chunk.IsTerrainGenerated = true;
                    }

                    // 2. Ensure 4 neighbor chunks exist and have terrain generated
                    Vector2i leftCoord = new Vector2i(coord.X - 1, coord.Y);
                    Vector2i rightCoord = new Vector2i(coord.X + 1, coord.Y);
                    Vector2i frontCoord = new Vector2i(coord.X, coord.Y + 1);
                    Vector2i backCoord = new Vector2i(coord.X, coord.Y - 1);

                    Chunk? leftChunk = IsChunkInsideWorld(leftCoord) ? GetOrCreateChunk(leftCoord) : null;
                    Chunk? rightChunk = IsChunkInsideWorld(rightCoord) ? GetOrCreateChunk(rightCoord) : null;
                    Chunk? frontChunk = IsChunkInsideWorld(frontCoord) ? GetOrCreateChunk(frontCoord) : null;
                    Chunk? backChunk = IsChunkInsideWorld(backCoord) ? GetOrCreateChunk(backCoord) : null;

                    if (leftChunk != null && !leftChunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(leftChunk);
                        leftChunk.IsTerrainGenerated = true;
                    }
                    if (rightChunk != null && !rightChunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(rightChunk);
                        rightChunk.IsTerrainGenerated = true;
                    }
                    if (frontChunk != null && !frontChunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(frontChunk);
                        frontChunk.IsTerrainGenerated = true;
                    }
                    if (backChunk != null && !backChunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(backChunk);
                        backChunk.IsTerrainGenerated = true;
                    }

                    // 3. Decorate chunk if needed
                    if (!chunk.IsDecorated)
                    {
                        generator.DecorateChunk(this, chunk);
                        chunk.IsDecorated = true;

                        // Scan chunk for placed light sources (lava, torches)
                        chunk.LightPositions.Clear();
                        for (int x = 0; x < Chunk.SIZE_X; x++)
                        {
                            for (int y = 0; y < Chunk.SIZE_Y; y++)
                            {
                                for (int z = 0; z < Chunk.SIZE_Z; z++)
                                {
                                    byte b = chunk.GetBlock(x, y, z);
                                    if (BlockRegistry.Get(b).IsLightSource)
                                    {
                                        int wx = (int)chunk.Position.X + x;
                                        int wz = (int)chunk.Position.Z + z;
                                        var lpos = new Vector3i(wx, y, wz);
                                        lightSources.TryAdd(lpos, 0);
                                        chunk.LightPositions.Add(lpos);
                                    }
                                }
                            }
                        }
                    }

                    // 4. Build mesh with boundary edge culling
                    ChunkMesh mesh = ChunkMesher.BuildMesh(chunk, leftChunk, rightChunk, frontChunk, backChunk);
                    readyToUploadMeshes.Enqueue((coord, mesh));
                }
                else
                {
                    workAvailable.WaitOne(40);
                }
            }
        }

        // Called each frame on the main OpenGL thread to upload meshes to GPU and stream around view position
        public void Update(Vector3 viewPosition, float deltaTime = 0f, int maxUploadsPerFrame = 6)
        {
            // 1. Upload ready meshes to GPU
            int uploaded = 0;
            while (uploaded < maxUploadsPerFrame && readyToUploadMeshes.TryDequeue(out var item))
            {
                if (chunks.TryGetValue(item.coord, out var chunk))
                {
                    chunk.UploadMesh(item.mesh);
                    chunk.IsGeneratingMesh = false;
                    uploaded++;
                }
                lock (queueLock)
                {
                    queuedChunks.Remove(item.coord);
                }
                item.mesh.Dispose();
            }

            // 2. Real-time chunk streaming around view position
            streamTimer += deltaTime;
            int viewCX = (int)Math.Floor(viewPosition.X / Chunk.SIZE_X);
            int viewCZ = (int)Math.Floor(viewPosition.Z / Chunk.SIZE_Z);
            var currentViewChunk = new Vector2i(viewCX, viewCZ);

            if (streamTimer > 0.15f || currentViewChunk != lastStreamedChunk)
            {
                streamTimer = 0f;
                lastStreamedChunk = currentViewChunk;
                StreamChunksAround(viewCX, viewCZ);
            }

            // 3. Update Minecart entities
            if (deltaTime > 0f)
            {
                for (int i = Minecarts.Count - 1; i >= 0; i--)
                {
                    var cart = Minecarts[i];
                    if (!cart.IsAlive)
                    {
                        Minecarts.RemoveAt(i);
                        continue;
                    }
                    cart.Update(this, deltaTime);
                }
            }

            // 4. Update fluid expansion simulation (water and lava)
            if (deltaTime > 0f)
            {
                FluidSystem.Update(this, deltaTime);

                // 4b. Crop growth simulation (wheat grows through 8 stages)
                cropGrowthTimer += deltaTime;
                if (cropGrowthTimer >= 3.5f)
                {
                    cropGrowthTimer = 0f;
                    if (GrowingCrops.Count > 0)
                    {
                        var crops = new List<Vector3i>(GrowingCrops);
                        foreach (var cPos in crops)
                        {
                            byte b = GetBlockWorld(cPos.X, cPos.Y, cPos.Z);
                            if (b == (byte)BlockType.Wheat)
                            {
                                byte stage = GetBlockMetadata(cPos.X, cPos.Y, cPos.Z);
                                if (stage < 7)
                                {
                                    if (Random.Shared.Next(100) < 55)
                                    {
                                        SetBlockMetadata(cPos.X, cPos.Y, cPos.Z, (byte)(stage + 1));
                                    }
                                }
                            }
                            else
                            {
                                GrowingCrops.Remove(cPos);
                            }
                        }
                    }
                }
            }

            // 5. Memory trim once the initial world generation burst settles
            if (!hasTrimmedStartupMemory && workQueue.IsEmpty && readyToUploadMeshes.IsEmpty && chunks.Count >= 25)
            {
                hasTrimmedStartupMemory = true;
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            }
        }

        // Legacy / simple overload
        public void Update(float deltaTime = 0f, int maxUploadsPerFrame = 6)
        {
            Update(new Vector3(WORLD_SIZE_X * 0.5f, 64f, WORLD_SIZE_Z * 0.5f), deltaTime, maxUploadsPerFrame);
        }

        private void StreamChunksAround(int centerCX, int centerCZ)
        {
            int r = ViewDistanceChunks;
            toEnqueueChunks.Clear();

            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    int distSq = dx * dx + dz * dz;
                    if (distSq <= r * r)
                    {
                        int cx = centerCX + dx;
                        int cz = centerCZ + dz;
                        var coord = new Vector2i(cx, cz);

                        if (IsChunkInsideWorld(coord))
                        {
                            bool alreadyQueued;
                            lock (queueLock)
                            {
                                alreadyQueued = queuedChunks.Contains(coord);
                            }
                            if (alreadyQueued) continue;

                            if (!chunks.TryGetValue(coord, out var chunk) || (!chunk.HasMesh && !chunk.IsGeneratingMesh))
                            {
                                toEnqueueChunks.Add((coord, distSq));
                            }
                        }
                    }
                }
            }

            // Sort so closest chunks to player/camera are generated and meshed first!
            if (toEnqueueChunks.Count > 1)
            {
                toEnqueueChunks.Sort((a, b) => a.distSq.CompareTo(b.distSq));
            }

            int budget = 32; // Limit enqueue batch size to avoid overwhelming queue
            for (int i = 0; i < Math.Min(budget, toEnqueueChunks.Count); i++)
            {
                EnqueueChunk(toEnqueueChunks[i].coord);
            }

            // Release GPU buffers for far chunks beyond view distance + 4
            int unloadDistSq = (r + 4) * (r + 4);
            foreach (var chunk in chunks.Values)
            {
                if (chunk.HasMesh)
                {
                    int dx = chunk.ChunkCoord.X - centerCX;
                    int dz = chunk.ChunkCoord.Y - centerCZ;
                    if (dx * dx + dz * dz > unloadDistSq)
                    {
                        chunk.DeleteGLBuffers();
                    }
                }
            }
        }

        // Render visible chunks with distance and true 3D frustum culling
        public void Render(ShaderProgram shaderProgram, Camera? camera = null)
        {
            shaderProgram.Bind();

            visibleOpaqueChunks.Clear();
            visibleWaterChunks.Clear();

            if (camera != null)
            {
                Matrix4 view = camera.GetViewMatrix();
                Matrix4 proj = camera.GetProjectionMatrix();
                Matrix4 vp = view * proj;
                cameraFrustum.Update(vp);

                float renderDistSq = MathF.Pow((ViewDistanceChunks + 1) * Chunk.SIZE_X, 2);
                Vector3 camPos = camera.Position;

                foreach (var chunk in chunks.Values)
                {
                    if (!chunk.HasMesh) continue;

                    float chunkCenterX = chunk.Position.X + Chunk.SIZE_X * 0.5f;
                    float chunkCenterZ = chunk.Position.Z + Chunk.SIZE_Z * 0.5f;

                    float dx = chunkCenterX - camPos.X;
                    float dz = chunkCenterZ - camPos.Z;
                    float distSq = dx * dx + dz * dz;

                    // Skip chunks beyond render distance
                    if (distSq > renderDistSq) continue;

                    // True 3D Frustum culling (chunks within 24 blocks of player are always rendered)
                    if (distSq > 24f * 24f)
                    {
                        Vector3 min = chunk.Position - new Vector3(2f, 0f, 2f);
                        Vector3 max = new Vector3(chunk.Position.X + Chunk.SIZE_X + 2f, Chunk.SIZE_Y, chunk.Position.Z + Chunk.SIZE_Z + 2f);
                        if (!cameraFrustum.IsBoxVisible(min, max))
                        {
                            continue; // Culled! Outside 3D view frustum
                        }
                    }

                    if (chunk.HasOpaqueMesh) visibleOpaqueChunks.Add(chunk);
                    if (chunk.HasWaterMesh) visibleWaterChunks.Add(chunk);
                }
            }
            else
            {
                foreach (var chunk in chunks.Values)
                {
                    if (chunk.HasOpaqueMesh) visibleOpaqueChunks.Add(chunk);
                    if (chunk.HasWaterMesh) visibleWaterChunks.Add(chunk);
                }
            }

            int transLoc = TranslucentPassUniformLoc >= 0
                ? TranslucentPassUniformLoc
                : GL.GetUniformLocation(shaderProgram.ID, "isTranslucentPass");

            // PASS 1: Render all Opaque terrain blocks (depth write ON, blend OFF)
            GL.Uniform1i(transLoc, 0);
            GL.Disable(EnableCap.Blend);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);

            for (int i = 0; i < visibleOpaqueChunks.Count; i++)
            {
                visibleOpaqueChunks[i].RenderOpaque();
            }

            // PASS 2: Render Translucent Water & Portal surfaces (only if water chunks exist!)
            if (visibleWaterChunks.Count > 0)
            {
                GL.Uniform1i(transLoc, 1);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.DepthMask(false);
                GL.DepthFunc(DepthFunction.Lequal);

                for (int i = 0; i < visibleWaterChunks.Count; i++)
                {
                    visibleWaterChunks[i].RenderWater();
                }

                // Restore standard GL pipeline states
                GL.DepthMask(true);
                GL.Disable(EnableCap.Blend);
                GL.DepthFunc(DepthFunction.Less);
            }

            // Render minecarts with the same active shader & texture atlas
            if (Minecarts.Count > 0)
            {
                MinecartRenderer.Render(Minecarts, shaderProgram);
            }
        }

        public void Destroy()
        {
            cancellationTokenSource.Cancel();
            workAvailable.Set();

            foreach (var thread in workerThreads)
            {
                thread.Join(200);
            }

            foreach (var chunk in chunks.Values)
            {
                chunk.DeleteGLBuffers();
            }
            chunks.Clear();

            while (readyToUploadMeshes.TryDequeue(out var item))
            {
                item.mesh.Dispose();
            }

            MinecartRenderer.Cleanup();
            Minecarts.Clear();
            lightSources.Clear();
        }

        public List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> GetActivePointLights(Vector3 playerPos, int maxCount = 15, float deltaTime = 0.016f)
        {
            lightQueryTimer += deltaTime;
            float moveDistSq = Vector3.DistanceSquared(playerPos, lastLightQueryPos);

            // Re-use cached point lights if player hasn't moved noticeably and timer is small
            if (moveDistSq < 0.64f && lightQueryTimer < 0.05f && activePointLights.Count > 0)
            {
                return activePointLights;
            }

            lightQueryTimer = 0f;
            lastLightQueryPos = playerPos;
            activePointLights.Clear();
            lightCandidates.Clear();

            int centerCX = (int)Math.Floor(playerPos.X / Chunk.SIZE_X);
            int centerCZ = (int)Math.Floor(playerPos.Z / Chunk.SIZE_Z);

            // Fast spatial lookup: only inspect chunks within 2 chunks radius (max 32 blocks of player)
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    if (chunks.TryGetValue(new Vector2i(centerCX + dx, centerCZ + dz), out var chunk))
                    {
                        var lights = chunk.LightPositions;
                        for (int k = 0; k < lights.Count; k++)
                        {
                            var pos = lights[k];
                            float lx = pos.X + 0.5f - playerPos.X;
                            float ly = pos.Y + 0.5f - playerPos.Y;
                            float lz = pos.Z + 0.5f - playerPos.Z;
                            float distSq = lx * lx + ly * ly + lz * lz;

                            if (distSq < 625.0f) // Within 25 blocks radius
                            {
                                lightCandidates.Add((pos, distSq));
                            }
                        }
                    }
                }
            }

            if (lightCandidates.Count > 1)
            {
                lightCandidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
            }

            int count = Math.Min(maxCount, lightCandidates.Count);

            for (int i = 0; i < count; i++)
            {
                var pos = lightCandidates[i].Pos;
                byte blockId = GetBlockWorld(pos.X, pos.Y, pos.Z);

                Vector3 lightColor;
                float radius = 15.0f;
                float intensity = 1.4f;

                if (blockId == (byte)BlockType.Lava)
                {
                    lightColor = new Vector3(1.0f, 0.45f, 0.15f);
                    radius = 12.0f;
                }
                else if (blockId == (byte)BlockType.Portal)
                {
                    lightColor = new Vector3(0.75f, 0.20f, 0.95f);
                    radius = 14.0f;
                    intensity = 1.6f;
                }
                else if (blockId == (byte)BlockType.Glowstone)
                {
                    lightColor = new Vector3(1.0f, 0.92f, 0.60f);
                    radius = 15.0f;
                    intensity = 1.5f;
                }
                else if (blockId == (byte)BlockType.JackOLantern)
                {
                    lightColor = new Vector3(1.0f, 0.65f, 0.20f);
                    radius = 15.0f;
                    intensity = 1.5f;
                }
                else if (blockId == (byte)BlockType.LitFurnace)
                {
                    lightColor = new Vector3(1.0f, 0.55f, 0.20f);
                    radius = 13.0f;
                    intensity = 1.4f;
                }
                else if (blockId == (byte)BlockType.RedstoneTorch || blockId == (byte)BlockType.RedstoneOre || blockId == (byte)BlockType.RedstoneBlock)
                {
                    lightColor = new Vector3(1.0f, 0.15f, 0.10f);
                    radius = 11.0f;
                    intensity = 1.2f;
                }
                else
                {
                    lightColor = new Vector3(1.0f, 0.75f, 0.40f);
                }

                activePointLights.Add((new Vector3(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f), lightColor, radius, intensity));
            }

            return activePointLights;
        }
    }
}
