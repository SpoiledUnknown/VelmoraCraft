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

        // Thread-safe queues for multithreaded terrain generation and meshing
        private readonly ConcurrentQueue<Vector2i> workQueue = new ConcurrentQueue<Vector2i>();
        private readonly ConcurrentQueue<(Vector2i coord, ChunkMesh mesh)> readyToUploadMeshes = new ConcurrentQueue<(Vector2i, ChunkMesh)>();
        private readonly HashSet<Vector2i> queuedChunks = new HashSet<Vector2i>();
        private readonly object queueLock = new object();

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Thread[] workerThreads;
        private readonly AutoResetEvent workAvailable = new AutoResetEvent(false);

        private float streamTimer = 0f;
        private Vector2i lastStreamedChunk = new Vector2i(-9999, -9999);

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

        public void SetBlockWorld(int wx, int wy, int wz, BlockType type)
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
                if (BlockRegistry.Get(oldBlock).IsLightSource)
                {
                    lightSources.TryRemove(new Vector3i(wx, wy, wz), out _);
                }

                chunk.SetBlock(lx, wy, lz, type);
                ModifiedBlocks[new Vector3i(wx, wy, wz)] = type;

                if (BlockRegistry.Get(type).IsLightSource)
                {
                    lightSources.TryAdd(new Vector3i(wx, wy, wz), 0);
                }

                EnqueueChunk(coord);

                // If block is on the edge of the chunk, also re-mesh adjacent chunk
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
                    workQueue.Enqueue(coord);
                    workAvailable.Set();
                }
            }
        }

        private void WorkerLoop()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (workQueue.TryDequeue(out var coord))
                {
                    lock (queueLock)
                    {
                        queuedChunks.Remove(coord);
                    }

                    if (!IsChunkInsideWorld(coord)) continue;

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
                                        lightSources.TryAdd(new Vector3i(wx, y, wz), 0);
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
                    uploaded++;
                }
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
        }

        // Legacy / simple overload
        public void Update(float deltaTime = 0f, int maxUploadsPerFrame = 6)
        {
            Update(new Vector3(WORLD_SIZE_X * 0.5f, 64f, WORLD_SIZE_Z * 0.5f), deltaTime, maxUploadsPerFrame);
        }

        private void StreamChunksAround(int centerCX, int centerCZ)
        {
            int r = ViewDistanceChunks;
            var toEnqueue = new List<(Vector2i coord, int distSq)>();

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
                            if (!chunks.TryGetValue(coord, out var chunk) || (!chunk.HasMesh && !chunk.IsGeneratingMesh))
                            {
                                toEnqueue.Add((coord, distSq));
                            }
                        }
                    }
                }
            }

            // Sort so closest chunks to player/camera are generated and meshed first!
            toEnqueue.Sort((a, b) => a.distSq.CompareTo(b.distSq));

            int budget = 32; // Limit enqueue batch size to avoid overwhelming queue
            for (int i = 0; i < Math.Min(budget, toEnqueue.Count); i++)
            {
                EnqueueChunk(toEnqueue[i].coord);
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

        // Render visible chunks with distance and frustum culling
        public void Render(ShaderProgram shaderProgram, Camera? camera = null)
        {
            shaderProgram.Bind();

            if (camera != null)
            {
                float renderDistSq = MathF.Pow((ViewDistanceChunks + 1) * Chunk.SIZE_X, 2);
                Vector3 camPos = camera.Position;
                Vector3 camForward = camera.Forward;

                float forwardHorizLen = MathF.Sqrt(camForward.X * camForward.X + camForward.Z * camForward.Z);
                float fwdX = (forwardHorizLen > 0.05f) ? camForward.X / forwardHorizLen : 0f;
                float fwdZ = (forwardHorizLen > 0.05f) ? camForward.Z / forwardHorizLen : 1f;

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

                    // Chunks within 32 blocks are always rendered (covers player feet and looking straight down)
                    if (distSq > 32f * 32f && forwardHorizLen > 0.05f)
                    {
                        // Directional frustum culling: Dot product between horizontal chunk direction and camera forward
                        float invDist = 1.0f / MathF.Sqrt(distSq);
                        float dirX = dx * invDist;
                        float dirZ = dz * invDist;

                        // Dot product < 0.15f corresponds to ~80 degrees off-axis (>160-degree horizontal field of view)
                        if (dirX * fwdX + dirZ * fwdZ < 0.15f)
                        {
                            continue; // Culled! Behind or outside camera view
                        }
                    }

                    chunk.Render();
                }
            }
            else
            {
                foreach (var chunk in chunks.Values)
                {
                    if (chunk.HasMesh)
                    {
                        chunk.Render();
                    }
                }
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

            MinecartRenderer.Cleanup();
            Minecarts.Clear();
            lightSources.Clear();
        }

        public List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> GetActivePointLights(Vector3 playerPos, int maxCount = 15)
        {
            var result = new List<(Vector3, Vector3, float, float)>(maxCount);
            if (lightSources.IsEmpty) return result;

            var candidates = new List<(Vector3i Pos, float DistSq)>();
            foreach (var pos in lightSources.Keys)
            {
                float dx = pos.X + 0.5f - playerPos.X;
                float dy = pos.Y + 0.5f - playerPos.Y;
                float dz = pos.Z + 0.5f - playerPos.Z;
                float distSq = dx * dx + dy * dy + dz * dz;

                if (distSq < 625.0f) // Within 25 blocks radius
                {
                    candidates.Add((pos, distSq));
                }
            }

            candidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
            int count = Math.Min(maxCount, candidates.Count);

            for (int i = 0; i < count; i++)
            {
                var pos = candidates[i].Pos;
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
                else
                {
                    lightColor = new Vector3(1.0f, 0.75f, 0.40f);
                }

                result.Add((new Vector3(pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f), lightColor, radius, intensity));
            }

            return result;
        }
    }
}
