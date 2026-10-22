using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using VelmoraCraft.src.Entities;
using VelmoraCraft.src.Graphics;

namespace VelmoraCraft.src.World
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
        private readonly ConcurrentQueue<Minecart> pendingSpawnedMinecarts = new ConcurrentQueue<Minecart>();

        public void SpawnMinecart(Minecart cart)
        {
            pendingSpawnedMinecarts.Enqueue(cart);
        }
        private readonly ConcurrentDictionary<Vector3i, byte> lightSources = new ConcurrentDictionary<Vector3i, byte>();
        public readonly ConcurrentDictionary<Vector3i, BlockType> ModifiedBlocks = new ConcurrentDictionary<Vector3i, BlockType>();
        public readonly ConcurrentDictionary<Vector3i, byte> BlockMetadataMap = new ConcurrentDictionary<Vector3i, byte>();
        public readonly FluidSystem FluidSystem = new FluidSystem();
        public UI.ChestManager? ChestManager { get; set; }

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

        // Player reference for dropped items and interactions
        public Entities.Player? PlayerRef { get; set; }

        // Farming and crop simulation (takes 3 to 4 in-game minutes from seed to mature wheat)
        public readonly HashSet<Vector3i> GrowingCrops = new HashSet<Vector3i>();
        public readonly ConcurrentDictionary<Vector3i, float> CropStageTimers = new ConcurrentDictionary<Vector3i, float>();
        private float cropGrowthTimer = 0f;

        // Tree sapling growth simulation (takes 5 to 6 in-game minutes)
        public readonly ConcurrentDictionary<Vector3i, float> GrowingSaplings = new ConcurrentDictionary<Vector3i, float>();
        private float saplingGrowthTimer = 0f;

        // Leaf decay simulation (leaves disappear after 1 to 2 in-game minutes when tree logs are mined)
        public readonly ConcurrentDictionary<Vector3i, float> PendingDecayingLeaves = new ConcurrentDictionary<Vector3i, float>();
        private float leafDecayTimer = 0f;

        // Configurable grass spread parameters (dirt turns to grass after 2-3 in-game minutes when near grass)
        public static float GrassSpreadMinSeconds = 120.0f; // 2 minutes
        public static float GrassSpreadMaxSeconds = 180.0f; // 3 minutes

        public readonly ConcurrentDictionary<Vector3i, float> PendingDirtGrassSpread = new ConcurrentDictionary<Vector3i, float>();
        private float dirtSpreadTimer = 0f;

        public DayNightCycle? DayNightCycle { get; set; }
        private float cobwebSpawnTimer = 0f;

        public World(int seed = 1337, int worldChunks = 64, Vector2i? initialSpawnChunk = null, IEnumerable<BlockChangeData>? initialModifiedBlocks = null)
        {
            Seed = seed;
            WorldChunksX = Math.Clamp(worldChunks, 8, 256);
            WorldChunksZ = Math.Clamp(worldChunks, 8, 256);
            ActiveWorldChunksX = WorldChunksX;
            ActiveWorldChunksZ = WorldChunksZ;
            ActiveWorldSizeX = WorldSizeX;
            ActiveWorldSizeZ = WorldSizeZ;

            // Pre-populate modified blocks so synchronous spawn generation and early meshing include player blocks
            if (initialModifiedBlocks != null)
            {
                foreach (var b in initialModifiedBlocks)
                {
                    var pos = new Vector3i(b.X, b.Y, b.Z);
                    ModifiedBlocks[pos] = (BlockType)b.BlockType;
                    if (b.Metadata > 0)
                    {
                        BlockMetadataMap[pos] = b.Metadata;
                    }
                }
            }

            generator = new WorldGenerator(seed);

            Vector2i spawnChunk = initialSpawnChunk ?? new Vector2i(WorldChunksX / 2, WorldChunksZ / 2);

            // 1. Immediately generate 5x5 spawn chunks synchronously for instant ground collision and fluid/block safety
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    var coord = new Vector2i(spawnChunk.X + dx, spawnChunk.Y + dz);
                    if (IsChunkInsideWorld(coord))
                    {
                        var chunk = GetOrCreateChunk(coord);
                        if (!chunk.IsTerrainGenerated)
                        {
                            generator.GenerateChunkTerrain(chunk);
                            ApplyModifiedBlocksToChunk(chunk);
                            chunk.IsTerrainGenerated = true;
                        }
                    }
                }
            }

            // Decorate 3x3 chunks around spawn synchronously
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    var coord = new Vector2i(spawnChunk.X + dx, spawnChunk.Y + dz);
                    if (IsChunkInsideWorld(coord))
                    {
                        var chunk = GetOrCreateChunk(coord);
                        if (!chunk.IsDecorated)
                        {
                            generator.DecorateChunk(this, chunk);
                            ApplyModifiedBlocksToChunk(chunk);
                            chunk.IsDecorated = true;
                            ScanChunkLightSources(chunk);
                        }
                    }
                }
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

        public void ApplyModifiedBlocksToChunk(Chunk chunk)
        {
            if (ModifiedBlocks.IsEmpty) return;

            int minX = chunk.ChunkCoord.X * Chunk.SIZE_X;
            int maxX = minX + Chunk.SIZE_X;
            int minZ = chunk.ChunkCoord.Y * Chunk.SIZE_Z;
            int maxZ = minZ + Chunk.SIZE_Z;

            foreach (var kvp in ModifiedBlocks)
            {
                var pos = kvp.Key;
                if (pos.X >= minX && pos.X < maxX && pos.Z >= minZ && pos.Z < maxZ)
                {
                    int lx = pos.X - minX;
                    int lz = pos.Z - minZ;
                    chunk.SetBlock(lx, pos.Y, lz, kvp.Value);
                    if (BlockMetadataMap.TryGetValue(pos, out byte meta))
                    {
                        chunk.SetMetadata(lx, pos.Y, lz, meta);
                    }
                    else
                    {
                        chunk.SetMetadata(lx, pos.Y, lz, 0);
                    }
                }
            }
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

        public BiomeType GetBiome(int wx, int wz)
        {
            return generator.GetBiome(wx, wz);
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
                    lock (chunk.LightPositions)
                    {
                        chunk.LightPositions.Remove(pos);
                    }
                    lightQueryTimer = 999f;
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
                    lock (chunk.LightPositions)
                    {
                        if (!chunk.LightPositions.Contains(pos))
                        {
                            chunk.LightPositions.Add(pos);
                        }
                    }
                    lightQueryTimer = 999f;
                }

                if (type == BlockType.Dirt)
                {
                    CheckAndRegisterDirtForGrassSpread(pos);
                }
                else
                {
                    PendingDirtGrassSpread.TryRemove(pos, out _);
                    if (type == BlockType.Grass)
                    {
                        CheckNeighborDirtForGrassSpread(pos);
                    }
                    else if (type == BlockType.Empty)
                    {
                        var below = new Vector3i(pos.X, pos.Y - 1, pos.Z);
                        if (GetBlockWorld(below.X, below.Y, below.Z) == (byte)BlockType.Dirt)
                        {
                            CheckAndRegisterDirtForGrassSpread(below);
                        }
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

        public bool HasAdjacentGrass(int x, int y, int z)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        if (GetBlockWorld(x + dx, y + dy, z + dz) == (byte)BlockType.Grass)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void CheckAndRegisterDirtForGrassSpread(Vector3i pos)
        {
            byte above = GetBlockWorld(pos.X, pos.Y + 1, pos.Z);
            bool isClear = !BlockRegistry.IsSolid(above) || BlockRegistry.Get(above).IsTransparent;
            if (!isClear) return;

            if (HasAdjacentGrass(pos.X, pos.Y, pos.Z))
            {
                if (!PendingDirtGrassSpread.ContainsKey(pos))
                {
                    // Fast-forwarded: 2 to 3 in-game minutes (120 to 180 seconds)
                    float durationSeconds = GrassSpreadMinSeconds + Random.Shared.NextSingle() * (GrassSpreadMaxSeconds - GrassSpreadMinSeconds);
                    PendingDirtGrassSpread[pos] = durationSeconds;
                }
            }
        }

        public void RegisterGrowingSapling(Vector3i pos)
        {
            // Trees take 5 to 6 in-game minutes to grow (300 to 360 seconds)
            float growthTime = 300.0f + Random.Shared.NextSingle() * 60.0f;
            GrowingSaplings[pos] = growthTime;
        }

        public bool HasLogWithin(Vector3i leafPos, int maxDist)
        {
            for (int dy = -maxDist; dy <= maxDist; dy++)
            {
                for (int dx = -maxDist; dx <= maxDist; dx++)
                {
                    for (int dz = -maxDist; dz <= maxDist; dz++)
                    {
                        if (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz) > maxDist * 1.5f) continue;
                        byte b = GetBlockWorld(leafPos.X + dx, leafPos.Y + dy, leafPos.Z + dz);
                        if (b == (byte)BlockType.Log || b == (byte)BlockType.BirchLog || b == (byte)BlockType.SpruceLog)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void TriggerTreeLeafDecay(Vector3i logPos)
        {
            // When logs are mined, scan surrounding canopy (up to 5 blocks horizontal, -2 to +8 vertical)
            for (int dy = -2; dy <= 8; dy++)
            {
                for (int dx = -5; dx <= 5; dx++)
                {
                    for (int dz = -5; dz <= 5; dz++)
                    {
                        var leafPos = new Vector3i(logPos.X + dx, logPos.Y + dy, logPos.Z + dz);
                        byte b = GetBlockWorld(leafPos.X, leafPos.Y, leafPos.Z);
                        if (b == (byte)BlockType.Leaves || b == (byte)BlockType.SpruceLeaves)
                        {
                            if (!HasLogWithin(leafPos, 4))
                            {
                                if (!PendingDecayingLeaves.ContainsKey(leafPos))
                                {
                                    // Leaves disappear after 1 to 2 in-game minutes (60 to 120 seconds)
                                    float decayDelay = 60.0f + Random.Shared.NextSingle() * 60.0f;
                                    PendingDecayingLeaves[leafPos] = decayDelay;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void CheckNeighborDirtForGrassSpread(Vector3i pos)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        var nPos = new Vector3i(pos.X + dx, pos.Y + dy, pos.Z + dz);
                        if (GetBlockWorld(nPos.X, nPos.Y, nPos.Z) == (byte)BlockType.Dirt)
                        {
                            CheckAndRegisterDirtForGrassSpread(nPos);
                        }
                    }
                }
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
                var chunk = GetOrCreateChunk(coord);
                if (queuedChunks.Add(coord))
                {
                    chunk.IsGeneratingMesh = true;
                    chunk.NeedsRemesh = false;
                    workQueue.Enqueue(coord);
                    workAvailable.Set();
                }
                else
                {
                    // Chunk is currently queued or being meshed; flag it so it immediately re-meshes with latest blocks
                    chunk.NeedsRemesh = true;
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
                        ApplyModifiedBlocksToChunk(chunk);
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
                        ApplyModifiedBlocksToChunk(leftChunk);
                        leftChunk.IsTerrainGenerated = true;
                    }
                    if (rightChunk != null && !rightChunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(rightChunk);
                        ApplyModifiedBlocksToChunk(rightChunk);
                        rightChunk.IsTerrainGenerated = true;
                    }
                    if (frontChunk != null && !frontChunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(frontChunk);
                        ApplyModifiedBlocksToChunk(frontChunk);
                        frontChunk.IsTerrainGenerated = true;
                    }
                    if (backChunk != null && !backChunk.IsTerrainGenerated)
                    {
                        generator.GenerateChunkTerrain(backChunk);
                        ApplyModifiedBlocksToChunk(backChunk);
                        backChunk.IsTerrainGenerated = true;
                    }

                    // 3. Decorate chunk if needed
                    if (!chunk.IsDecorated)
                    {
                        generator.DecorateChunk(this, chunk);
                        ApplyModifiedBlocksToChunk(chunk);
                        chunk.IsDecorated = true;

                        ScanChunkLightSources(chunk);
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
                bool needsRemesh = false;
                Chunk? chunkObj = null;
                if (chunks.TryGetValue(item.coord, out chunkObj))
                {
                    chunkObj.UploadMesh(item.mesh);
                    chunkObj.IsGeneratingMesh = false;
                    needsRemesh = chunkObj.NeedsRemesh;
                    chunkObj.NeedsRemesh = false;
                    uploaded++;
                }
                lock (queueLock)
                {
                    queuedChunks.Remove(item.coord);
                    if (needsRemesh)
                    {
                        queuedChunks.Add(item.coord);
                        if (chunkObj != null) chunkObj.IsGeneratingMesh = true;
                        workQueue.Enqueue(item.coord);
                        workAvailable.Set();
                    }
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
            while (pendingSpawnedMinecarts.TryDequeue(out var pendingCart))
            {
                lock (Minecarts)
                {
                    Minecarts.Add(pendingCart);
                }
            }

            if (deltaTime > 0f)
            {
                lock (Minecarts)
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

            // 4. Update fluid expansion simulation (water and lava)
            if (deltaTime > 0f)
            {
                FluidSystem.Update(this, deltaTime);

                // 4b. Crop growth simulation (wheat grows through 7 stages to maturity in 3 to 4 in-game minutes)
                if (GrowingCrops.Count > 0)
                {
                    cropGrowthTimer += deltaTime;
                    if (cropGrowthTimer >= 1.0f) // Tick once per second
                    {
                        float step = cropGrowthTimer;
                        cropGrowthTimer = 0f;

                        var crops = new List<Vector3i>(GrowingCrops);
                        foreach (var cPos in crops)
                        {
                            byte b = GetBlockWorld(cPos.X, cPos.Y, cPos.Z);
                            if (b == (byte)BlockType.Wheat)
                            {
                                byte stage = GetBlockMetadata(cPos.X, cPos.Y, cPos.Z);
                                if (stage < 7)
                                {
                                    if (!CropStageTimers.TryGetValue(cPos, out float t))
                                    {
                                        t = 25.0f + Random.Shared.NextSingle() * 10.0f; // ~30s per stage (3-4 mins total)
                                    }
                                    t -= step;
                                    if (t <= 0f)
                                    {
                                        byte newStage = (byte)(stage + 1);
                                        SetBlockMetadata(cPos.X, cPos.Y, cPos.Z, newStage);
                                        if (newStage < 7)
                                        {
                                            CropStageTimers[cPos] = 25.0f + Random.Shared.NextSingle() * 10.0f;
                                        }
                                        else
                                        {
                                            CropStageTimers.TryRemove(cPos, out _);
                                        }
                                    }
                                    else
                                    {
                                        CropStageTimers[cPos] = t;
                                    }
                                }
                                else
                                {
                                    CropStageTimers.TryRemove(cPos, out _);
                                }
                            }
                            else
                            {
                                GrowingCrops.Remove(cPos);
                                CropStageTimers.TryRemove(cPos, out _);
                            }
                        }
                    }
                }

                // 4c. Tree Sapling growth simulation (saplings grow into trees in 5 to 6 in-game minutes)
                if (GrowingSaplings.Count > 0)
                {
                    saplingGrowthTimer += deltaTime;
                    if (saplingGrowthTimer >= 1.0f)
                    {
                        float step = saplingGrowthTimer;
                        saplingGrowthTimer = 0f;

                        var saplingEntries = GrowingSaplings.ToArray();
                        foreach (var kvp in saplingEntries)
                        {
                            Vector3i sPos = kvp.Key;
                            float remaining = kvp.Value - step;

                            if (remaining <= 0f)
                            {
                                GrowingSaplings.TryRemove(sPos, out _);

                                byte b = GetBlockWorld(sPos.X, sPos.Y, sPos.Z);
                                if (b == (byte)BlockType.OakSapling)
                                {
                                    // Space check: ensure clearance above sapling
                                    bool hasClearance = true;
                                    for (int cy = 1; cy <= 5; cy++)
                                    {
                                        byte above = GetBlockWorld(sPos.X, sPos.Y + cy, sPos.Z);
                                        if (above != (byte)BlockType.Empty && above != (byte)BlockType.Leaves)
                                        {
                                            hasClearance = false;
                                            break;
                                        }
                                    }

                                    if (hasClearance)
                                    {
                                        // Replace sapling with tree trunk base and grow
                                        SetBlockWorld(sPos.X, sPos.Y, sPos.Z, BlockType.Empty);
                                        var biome = GetBiome(sPos.X, sPos.Z);
                                        if (biome == BiomeType.Mountains)
                                        {
                                            generator.GrowSpruceTree(this, sPos.X, sPos.Y, sPos.Z);
                                        }
                                        else if (biome == BiomeType.Forest && (WorldGenerator.HashCoordinates(sPos.X, 55, sPos.Z, Seed) % 3 == 0))
                                        {
                                            generator.GrowBirchTree(this, sPos.X, sPos.Y, sPos.Z);
                                        }
                                        else
                                        {
                                            generator.GrowTree(this, sPos.X, sPos.Y, sPos.Z);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                GrowingSaplings[sPos] = remaining;
                            }
                        }
                    }
                }

                // 4d. Leaf Decay simulation (leaves with no trunk logs disappear after 1 to 2 in-game minutes)
                if (PendingDecayingLeaves.Count > 0)
                {
                    leafDecayTimer += deltaTime;
                    if (leafDecayTimer >= 1.0f)
                    {
                        float step = leafDecayTimer;
                        leafDecayTimer = 0f;

                        var leafEntries = PendingDecayingLeaves.ToArray();
                        foreach (var kvp in leafEntries)
                        {
                            Vector3i lPos = kvp.Key;
                            float remaining = kvp.Value - step;

                            if (remaining <= 0f)
                            {
                                PendingDecayingLeaves.TryRemove(lPos, out _);

                                byte b = GetBlockWorld(lPos.X, lPos.Y, lPos.Z);
                                if (b == (byte)BlockType.Leaves || b == (byte)BlockType.SpruceLeaves)
                                {
                                    if (!HasLogWithin(lPos, 4))
                                    {
                                        SetBlockWorld(lPos.X, lPos.Y, lPos.Z, BlockType.Empty);

                                        // ~10% chance to drop Oak Sapling
                                        if (Random.Shared.Next(100) < 10 && PlayerRef != null)
                                        {
                                            PlayerRef.DroppedItems.Add(new ItemDrop(BlockType.OakSapling, new Vector3(lPos.X + 0.5f, lPos.Y + 0.5f, lPos.Z + 0.5f)));
                                        }
                                    }
                                }
                            }
                            else
                            {
                                PendingDecayingLeaves[lPos] = remaining;
                            }
                        }
                    }
                }

                // 4c. Dirt-to-grass spread simulation (dirt near grass turns into grass after 1-2 in-game days)
                if (PendingDirtGrassSpread.Count > 0)
                {
                    dirtSpreadTimer += deltaTime;
                    if (dirtSpreadTimer >= 1.0f) // Checked once per second
                    {
                        float step = dirtSpreadTimer;
                        dirtSpreadTimer = 0f;

                        var entries = PendingDirtGrassSpread.ToArray();
                        foreach (var kvp in entries)
                        {
                            Vector3i dPos = kvp.Key;
                            float remaining = kvp.Value - step;

                            if (remaining <= 0f)
                            {
                                PendingDirtGrassSpread.TryRemove(dPos, out _);

                                // Verify block is still dirt and eligible to convert
                                if (GetBlockWorld(dPos.X, dPos.Y, dPos.Z) == (byte)BlockType.Dirt)
                                {
                                    byte above = GetBlockWorld(dPos.X, dPos.Y + 1, dPos.Z);
                                    bool clear = !BlockRegistry.IsSolid(above) || BlockRegistry.Get(above).IsTransparent;
                                    if (clear && HasAdjacentGrass(dPos.X, dPos.Y, dPos.Z))
                                    {
                                        SetBlockWorld(dPos.X, dPos.Y, dPos.Z, BlockType.Grass);
                                    }
                                }
                            }
                            else
                            {
                                PendingDirtGrassSpread[dPos] = remaining;
                            }
                        }
                    }
                }

                // 4d. Cobweb spawning on trees at night
                if (DayNightCycle != null && (DayNightCycle.CurrentPhase == DayNightPhase.Night || DayNightCycle.CurrentPhase == DayNightPhase.Dusk))
                {
                    cobwebSpawnTimer += deltaTime;
                    if (cobwebSpawnTimer >= 1.5f) // Check every 1.5 seconds at night
                    {
                        cobwebSpawnTimer = 0f;
                        int attempts = 4;
                        for (int i = 0; i < attempts; i++)
                        {
                            int rx = (int)viewPosition.X + Random.Shared.Next(-28, 29);
                            int rz = (int)viewPosition.Z + Random.Shared.Next(-28, 29);
                            if (!IsInsideWorld(rx, rz)) continue;

                            int surfaceY = GetSurfaceHeight(rx, rz);
                            for (int ry = surfaceY + 7; ry >= surfaceY + 1; ry--)
                            {
                                byte b = GetBlockWorld(rx, ry, rz);
                                if (b == (byte)BlockType.Leaves || b == (byte)BlockType.SpruceLeaves)
                                {
                                    int targetY = ry - 1;
                                    if (targetY > 0 && GetBlockWorld(rx, targetY, rz) == (byte)BlockType.Empty)
                                    {
                                        int nearbyWebs = 0;
                                        for (int dx = -2; dx <= 2 && nearbyWebs < 2; dx++)
                                        {
                                            for (int dz = -2; dz <= 2 && nearbyWebs < 2; dz++)
                                            {
                                                if (GetBlockWorld(rx + dx, targetY, rz + dz) == (byte)BlockType.Cobweb)
                                                    nearbyWebs++;
                                            }
                                        }

                                        if (nearbyWebs < 2)
                                        {
                                            SetBlockWorld(rx, targetY, rz, BlockType.Cobweb);
                                            break;
                                        }
                                    }
                                }
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
            lock (Minecarts)
            {
                if (Minecarts.Count > 0)
                {
                    MinecartRenderer.Render(Minecarts, shaderProgram);
                }
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
            lock (Minecarts)
            {
                Minecarts.Clear();
            }
            while (pendingSpawnedMinecarts.TryDequeue(out _)) { }
            lightSources.Clear();
        }

        private void ScanChunkLightSources(Chunk chunk)
        {
            lock (chunk.LightPositions)
            {
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
        }

        public List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> GetActivePointLights(Vector3 playerPos, int maxCount = 15, float deltaTime = 0.016f)
        {
            lightQueryTimer += deltaTime;
            float moveDistSq = Vector3.DistanceSquared(playerPos, lastLightQueryPos);

            // Re-use cached point lights if player hasn't moved noticeably and timer is small
            if (moveDistSq < 0.64f && lightQueryTimer < 0.05f)
            {
                return activePointLights;
            }

            lightQueryTimer = 0f;
            lastLightQueryPos = playerPos;
            activePointLights.Clear();
            lightCandidates.Clear();

            int centerCX = (int)Math.Floor(playerPos.X / Chunk.SIZE_X);
            int centerCZ = (int)Math.Floor(playerPos.Z / Chunk.SIZE_Z);

            // Spatial lookup: inspect chunks within 3 chunks radius (up to 48 blocks of player)
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (chunks.TryGetValue(new Vector2i(centerCX + dx, centerCZ + dz), out var chunk))
                    {
                        lock (chunk.LightPositions)
                        {
                            var lights = chunk.LightPositions;
                            for (int k = 0; k < lights.Count; k++)
                            {
                                var pos = lights[k];
                                float lx = pos.X - playerPos.X;
                                float ly = pos.Y - playerPos.Y;
                                float lz = pos.Z - playerPos.Z;
                                float distSq = lx * lx + ly * ly + lz * lz;

                                if (distSq < 1600.0f) // Within 40 blocks radius
                                {
                                    lightCandidates.Add((pos, distSq));
                                }
                            }
                        }
                    }
                }
            }

            if (lightCandidates.Count > 1)
            {
                lightCandidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));
            }

            for (int i = 0; i < lightCandidates.Count && activePointLights.Count < maxCount; i++)
            {
                var pos = lightCandidates[i].Pos;
                byte blockId = GetBlockWorld(pos.X, pos.Y, pos.Z);

                if (blockId == (byte)BlockType.Empty || !BlockRegistry.Get(blockId).IsLightSource)
                {
                    continue;
                }

                if (blockId == (byte)BlockType.Portal)
                {
                    // Deduplicate adjacent portal blocks so a 2x3 portal frame produces a clean, focused emitter
                    bool duplicatePortal = false;
                    for (int p = 0; p < activePointLights.Count; p++)
                    {
                        var existing = activePointLights[p];
                        float pdx = existing.Position.X - pos.X;
                        float pdy = existing.Position.Y - pos.Y;
                        float pdz = existing.Position.Z - pos.Z;
                        if (pdx * pdx + pdy * pdy + pdz * pdz < 9.0f)
                        {
                            duplicatePortal = true;
                            break;
                        }
                    }
                    if (duplicatePortal) continue;
                }

                Vector3 lightColor;
                float radius = 15.0f;
                float intensity = 1.4f;
                Vector3 lightPos = new Vector3(pos.X, pos.Y, pos.Z);

                if (blockId == (byte)BlockType.Lava)
                {
                    lightColor = new Vector3(1.0f, 0.45f, 0.15f);
                    radius = 12.0f;
                }
                else if (blockId == (byte)BlockType.Portal)
                {
                    lightColor = new Vector3(0.85f, 0.25f, 1.0f);
                    radius = 16.0f;
                    intensity = 1.7f;
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
                else if (blockId == (byte)BlockType.RedstoneTorch)
                {
                    lightColor = new Vector3(1.0f, 0.15f, 0.10f);
                    radius = 11.0f;
                    intensity = 1.2f;
                    lightPos = new Vector3(pos.X, pos.Y + 0.20f, pos.Z);
                }
                else // BlockType.Torch
                {
                    lightColor = new Vector3(1.0f, 0.74f, 0.40f);
                    radius = 15.0f;
                    intensity = 1.4f;
                    lightPos = new Vector3(pos.X, pos.Y + 0.20f, pos.Z);
                }

                activePointLights.Add((lightPos, lightColor, radius, intensity));
            }

            return activePointLights;
        }
    }
}
