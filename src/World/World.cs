using System.Collections.Concurrent;
using System.Runtime;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VelmoraCraft.src.Entities;
using VelmoraCraft.src.Graphics;
using VelmoraCraft.src.UI;

namespace VelmoraCraft.src.World;

public class World
{
    public static float GrassSpreadMinSeconds = 120.0f;
    public static float GrassSpreadMaxSeconds = 180.0f;
    public readonly ConcurrentDictionary<Vector3i, byte> BlockMetadataMap = new();
    public readonly ConcurrentDictionary<Vector3i, float> CropStageTimers = new();
    public readonly FluidSystem FluidSystem = new();

    public readonly HashSet<Vector3i> GrowingCrops = new();

    public readonly ConcurrentDictionary<Vector3i, float> GrowingSaplings = new();
    public readonly List<Minecart> Minecarts = new();
    public readonly ConcurrentDictionary<Vector3i, BlockType> ModifiedBlocks = new();

    public readonly ConcurrentDictionary<Vector3i, float> PendingDecayingLeaves = new();

    public readonly ConcurrentDictionary<Vector3i, float> PendingDirtGrassSpread = new();
    private readonly List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> activePointLights = new(16);
    private readonly Frustum cameraFrustum = new();

    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly ConcurrentDictionary<Vector2i, Chunk> chunks = new();

    private readonly WorldGenerator generator;
    private readonly List<(Vector3i Pos, float DistSq)> lightCandidates = new(64);

    private readonly ConcurrentDictionary<Vector3i, byte> lightSources = new();
    private readonly ConcurrentQueue<Minecart> pendingSpawnedMinecarts = new();
    private readonly object queueLock = new();
    private readonly HashSet<Vector2i> queuedChunks = new();
    private readonly ConcurrentQueue<(Vector2i coord, ChunkMesh mesh)> readyToUploadMeshes = new();

    private readonly List<(Vector2i coord, int distSq)> toEnqueueChunks = new(128);

    private readonly List<Chunk> visibleChunks = new(512);
    private readonly List<Chunk> visibleOpaqueChunks = new(256);
    private readonly List<Chunk> visibleWaterChunks = new(64);
    private readonly AutoResetEvent workAvailable = new(false);

    private readonly ConcurrentQueue<Vector2i> workQueue = new();
    private readonly Thread[] workerThreads;
    private float cobwebSpawnTimer;
    private float cropGrowthTimer;
    private float dirtSpreadTimer;
    private bool hasTrimmedStartupMemory;
    private Vector3 lastLightQueryPos = new(-9999f, -9999f, -9999f);
    private Vector2i lastStreamedChunk = new(-9999, -9999);
    private float leafDecayTimer;
    private float lightQueryTimer;
    private float saplingGrowthTimer;

    private float streamTimer;

    public World(int seed = 1337, int worldChunks = 64, Vector2i? initialSpawnChunk = null,
        IEnumerable<BlockChangeData>? initialModifiedBlocks = null)
    {
        Seed = seed;
        WorldChunksX = Math.Clamp(worldChunks, 8, 256);
        WorldChunksZ = Math.Clamp(worldChunks, 8, 256);
        ActiveWorldChunksX = WorldChunksX;
        ActiveWorldChunksZ = WorldChunksZ;
        ActiveWorldSizeX = WorldSizeX;
        ActiveWorldSizeZ = WorldSizeZ;

        if (initialModifiedBlocks != null)
            foreach (var b in initialModifiedBlocks)
            {
                var pos = new Vector3i(b.X, b.Y, b.Z);
                ModifiedBlocks[pos] = (BlockType)b.BlockType;
                if (b.Metadata > 0) BlockMetadataMap[pos] = b.Metadata;
            }

        generator = new WorldGenerator(seed);

        var spawnChunk = initialSpawnChunk ?? new Vector2i(WorldChunksX / 2, WorldChunksZ / 2);

        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
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

        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
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

        var threadCount = Math.Max(2, Environment.ProcessorCount - 1);
        workerThreads = new Thread[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            workerThreads[i] = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"ChunkWorker-{i}"
            };
            workerThreads[i].Start();
        }

        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        {
            var coord = new Vector2i(spawnChunk.X + dx, spawnChunk.Y + dz);
            if (IsChunkInsideWorld(coord)) EnqueueChunk(coord);
        }
    }

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
    public int ViewDistanceChunks { get; set; } = 8;
    public ChestManager? ChestManager { get; set; }
    public int TranslucentPassUniformLoc { get; set; } = -1;

    public Player? PlayerRef { get; set; }

    public DayNightCycle? DayNightCycle { get; set; }

    public void SpawnMinecart(Minecart cart)
    {
        pendingSpawnedMinecarts.Enqueue(cart);
    }

    public Chunk GetOrCreateChunk(Vector2i coord)
    {
        return chunks.GetOrAdd(coord, c => new Chunk(c));
    }

    public void ApplyModifiedBlocksToChunk(Chunk chunk)
    {
        if (ModifiedBlocks.IsEmpty) return;

        var minX = chunk.ChunkCoord.X * Chunk.SIZE_X;
        var maxX = minX + Chunk.SIZE_X;
        var minZ = chunk.ChunkCoord.Y * Chunk.SIZE_Z;
        var maxZ = minZ + Chunk.SIZE_Z;

        foreach (var kvp in ModifiedBlocks)
        {
            var pos = kvp.Key;
            if (pos.X >= minX && pos.X < maxX && pos.Z >= minZ && pos.Z < maxZ)
            {
                var lx = pos.X - minX;
                var lz = pos.Z - minZ;
                chunk.SetBlock(lx, pos.Y, lz, kvp.Value);
                chunk.SetMetadata(lx, pos.Y, lz, BlockMetadataMap.TryGetValue(pos, out var meta) ? meta : (byte)0);
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

        var cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
        var cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);
        var chunk = GetOrCreateChunk(new Vector2i(cx, cz));
        if (!chunk.IsTerrainGenerated)
        {
            generator.GenerateChunkTerrain(chunk);
            chunk.IsTerrainGenerated = true;
        }

        var lx = wx - cx * Chunk.SIZE_X;
        var lz = wz - cz * Chunk.SIZE_Z;

        for (var y = Chunk.SIZE_Y - 2; y >= 1; y--)
        {
            var b = chunk.GetBlock(lx, y, lz);
            if (b != (byte)BlockType.Empty && b != (byte)BlockType.Water) return y;
        }

        return 60;
    }

    public byte GetBlockWorld(int wx, int wy, int wz)
    {
        if (wy < 0 || wy >= Chunk.SIZE_Y) return (byte)BlockType.Empty;

        var cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
        var cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);

        if (chunks.TryGetValue(new Vector2i(cx, cz), out var chunk))
        {
            var lx = wx - cx * Chunk.SIZE_X;
            var lz = wz - cz * Chunk.SIZE_Z;
            return chunk.GetBlock(lx, wy, lz);
        }

        return (byte)BlockType.Empty;
    }

    public void SetBlockWorld(int wx, int wy, int wz, BlockType type, byte metadata = 0)
    {
        if (wy < 0 || wy >= Chunk.SIZE_Y) return;

        var cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
        var cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);
        var coord = new Vector2i(cx, cz);

        if (!IsChunkInsideWorld(coord)) return;

        var chunk = GetOrCreateChunk(coord);
        var lx = wx - cx * Chunk.SIZE_X;
        var lz = wz - cz * Chunk.SIZE_Z;

        var oldBlock = chunk.GetBlock(lx, wy, lz);
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

        ModifiedBlocks[pos] = type;
        if (type == BlockType.Empty || metadata == 0)
            BlockMetadataMap.TryRemove(pos, out _);
        else
            BlockMetadataMap[pos] = metadata;

        if (BlockRegistry.Get(type).IsLightSource)
        {
            lightSources.TryAdd(pos, 0);
            lock (chunk.LightPositions)
            {
                if (!chunk.LightPositions.Contains(pos)) chunk.LightPositions.Add(pos);
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
                    CheckAndRegisterDirtForGrassSpread(below);
            }
        }

        EnqueueChunk(coord);

        if (lx == 0 && cx > 0) EnqueueChunk(new Vector2i(cx - 1, cz));
        if (lx == Chunk.SIZE_X - 1 && cx < WORLD_CHUNKS_X - 1) EnqueueChunk(new Vector2i(cx + 1, cz));
        if (lz == 0 && cz > 0) EnqueueChunk(new Vector2i(cx, cz - 1));
        if (lz == Chunk.SIZE_Z - 1 && cz < WORLD_CHUNKS_Z - 1) EnqueueChunk(new Vector2i(cx, cz + 1));
    }

    public bool HasAdjacentGrass(int x, int y, int z)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
        {
            if (dx == 0 && dy == 0 && dz == 0) continue;
            if (GetBlockWorld(x + dx, y + dy, z + dz) == (byte)BlockType.Grass) return true;
        }

        return false;
    }

    public void CheckAndRegisterDirtForGrassSpread(Vector3i pos)
    {
        var above = GetBlockWorld(pos.X, pos.Y + 1, pos.Z);
        var isClear = !BlockRegistry.IsSolid(above) || BlockRegistry.Get(above).IsTransparent;
        if (!isClear) return;

        if (HasAdjacentGrass(pos.X, pos.Y, pos.Z) && !PendingDirtGrassSpread.ContainsKey(pos))
        {
            var durationSeconds = GrassSpreadMinSeconds +
                                  Random.Shared.NextSingle() * (GrassSpreadMaxSeconds - GrassSpreadMinSeconds);
            PendingDirtGrassSpread[pos] = durationSeconds;
        }
    }

    public void RegisterGrowingSapling(Vector3i pos)
    {
        var growthTime = 300.0f + Random.Shared.NextSingle() * 60.0f;
        GrowingSaplings[pos] = growthTime;
    }

    public bool HasLogWithin(Vector3i leafPos, int maxDist)
    {
        for (var dy = -maxDist; dy <= maxDist; dy++)
        for (var dx = -maxDist; dx <= maxDist; dx++)
        for (var dz = -maxDist; dz <= maxDist; dz++)
        {
            if (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz) > maxDist * 1.5f) continue;
            var b = GetBlockWorld(leafPos.X + dx, leafPos.Y + dy, leafPos.Z + dz);
            if (b is (byte)BlockType.Log or (byte)BlockType.BirchLog or (byte)BlockType.SpruceLog) return true;
        }

        return false;
    }

    public void TriggerTreeLeafDecay(Vector3i logPos)
    {
        for (var dy = -2; dy <= 8; dy++)
        for (var dx = -5; dx <= 5; dx++)
        for (var dz = -5; dz <= 5; dz++)
        {
            var leafPos = new Vector3i(logPos.X + dx, logPos.Y + dy, logPos.Z + dz);
            var b = GetBlockWorld(leafPos.X, leafPos.Y, leafPos.Z);
            if (b is (byte)BlockType.Leaves or (byte)BlockType.SpruceLeaves)
                if (!HasLogWithin(leafPos, 4) && !PendingDecayingLeaves.ContainsKey(leafPos))
                    PendingDecayingLeaves[leafPos] = 60.0f + Random.Shared.NextSingle() * 60.0f;
        }
    }

    public void CheckNeighborDirtForGrassSpread(Vector3i pos)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
        {
            if (dx == 0 && dy == 0 && dz == 0) continue;
            var nPos = new Vector3i(pos.X + dx, pos.Y + dy, pos.Z + dz);
            if (GetBlockWorld(nPos.X, nPos.Y, nPos.Z) == (byte)BlockType.Dirt) CheckAndRegisterDirtForGrassSpread(nPos);
        }
    }

    public byte GetBlockMetadata(int wx, int wy, int wz)
    {
        if (wy < 0 || wy >= Chunk.SIZE_Y) return 0;

        var cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
        var cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);

        return chunks.TryGetValue(new Vector2i(cx, cz), out var chunk)
            ? chunk.GetMetadata(wx - cx * Chunk.SIZE_X, wy, wz - cz * Chunk.SIZE_Z)
            : (byte)0;
    }

    public void SetBlockMetadata(int wx, int wy, int wz, byte metadata)
    {
        if (wy < 0 || wy >= Chunk.SIZE_Y) return;

        var cx = (int)Math.Floor((float)wx / Chunk.SIZE_X);
        var cz = (int)Math.Floor((float)wz / Chunk.SIZE_Z);
        var coord = new Vector2i(cx, cz);

        if (!IsChunkInsideWorld(coord)) return;

        var chunk = GetOrCreateChunk(coord);
        var lx = wx - cx * Chunk.SIZE_X;
        var lz = wz - cz * Chunk.SIZE_Z;

        chunk.SetMetadata(lx, wy, lz, metadata);
        var pos = new Vector3i(wx, wy, wz);
        if (metadata > 0)
            BlockMetadataMap[pos] = metadata;
        else
            BlockMetadataMap.TryRemove(pos, out _);

        EnqueueChunk(coord);

        if (lx == 0 && cx > 0) EnqueueChunk(new Vector2i(cx - 1, cz));
        if (lx == Chunk.SIZE_X - 1 && cx < WORLD_CHUNKS_X - 1) EnqueueChunk(new Vector2i(cx + 1, cz));
        if (lz == 0 && cz > 0) EnqueueChunk(new Vector2i(cx, cz - 1));
        if (lz == Chunk.SIZE_Z - 1 && cz < WORLD_CHUNKS_Z - 1) EnqueueChunk(new Vector2i(cx, cz + 1));
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
                chunk.NeedsRemesh = true;
            }
        }
    }

    private void WorkerLoop()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
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

                if (!chunk.IsTerrainGenerated)
                {
                    generator.GenerateChunkTerrain(chunk);
                    ApplyModifiedBlocksToChunk(chunk);
                    chunk.IsTerrainGenerated = true;
                }

                Vector2i leftCoord = new(coord.X - 1, coord.Y);
                Vector2i rightCoord = new(coord.X + 1, coord.Y);
                Vector2i frontCoord = new(coord.X, coord.Y + 1);
                Vector2i backCoord = new(coord.X, coord.Y - 1);

                var leftChunk = EnsureNeighborChunk(leftCoord);
                var rightChunk = EnsureNeighborChunk(rightCoord);
                var frontChunk = EnsureNeighborChunk(frontCoord);
                var backChunk = EnsureNeighborChunk(backCoord);

                if (!chunk.IsDecorated)
                {
                    generator.DecorateChunk(this, chunk);
                    ApplyModifiedBlocksToChunk(chunk);
                    chunk.IsDecorated = true;
                    ScanChunkLightSources(chunk);
                }

                var mesh = ChunkMesher.BuildMesh(chunk, leftChunk, rightChunk, frontChunk, backChunk);
                readyToUploadMeshes.Enqueue((coord, mesh));
            }
            else
            {
                workAvailable.WaitOne(40);
            }
        }
    }

    private Chunk? EnsureNeighborChunk(Vector2i coord)
    {
        if (!IsChunkInsideWorld(coord)) return null;

        var chunk = GetOrCreateChunk(coord);
        if (!chunk.IsTerrainGenerated)
        {
            generator.GenerateChunkTerrain(chunk);
            ApplyModifiedBlocksToChunk(chunk);
            chunk.IsTerrainGenerated = true;
        }

        return chunk;
    }

    public void Update(Vector3 viewPosition, float deltaTime = 0f, int maxUploadsPerFrame = 6)
    {
        var uploaded = 0;
        while (uploaded < maxUploadsPerFrame && readyToUploadMeshes.TryDequeue(out var item))
        {
            var needsRemesh = false;
            if (chunks.TryGetValue(item.coord, out var chunkObj))
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

        streamTimer += deltaTime;
        var viewCX = (int)Math.Floor(viewPosition.X / Chunk.SIZE_X);
        var viewCZ = (int)Math.Floor(viewPosition.Z / Chunk.SIZE_Z);
        var currentViewChunk = new Vector2i(viewCX, viewCZ);

        if (streamTimer > 0.15f || currentViewChunk != lastStreamedChunk)
        {
            streamTimer = 0f;
            lastStreamedChunk = currentViewChunk;
            StreamChunksAround(viewCX, viewCZ);
        }

        while (pendingSpawnedMinecarts.TryDequeue(out var pendingCart))
            lock (Minecarts)
            {
                Minecarts.Add(pendingCart);
            }

        if (deltaTime > 0f)
        {
            lock (Minecarts)
            {
                for (var i = Minecarts.Count - 1; i >= 0; i--)
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

            FluidSystem.Update(this, deltaTime);
            UpdateCropGrowth(deltaTime);
            UpdateSaplingGrowth(deltaTime);
            UpdateLeafDecay(deltaTime);
            UpdateDirtGrassSpread(deltaTime);
            UpdateNightSpidersAndWebs(viewPosition, deltaTime);
        }

        if (!hasTrimmedStartupMemory && workQueue.IsEmpty && readyToUploadMeshes.IsEmpty && chunks.Count >= 25)
        {
            hasTrimmedStartupMemory = true;
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        }
    }

    private void UpdateCropGrowth(float deltaTime)
    {
        if (GrowingCrops.Count == 0) return;

        cropGrowthTimer += deltaTime;
        if (cropGrowthTimer < 1.0f) return;

        var step = cropGrowthTimer;
        cropGrowthTimer = 0f;

        var crops = new List<Vector3i>(GrowingCrops);
        foreach (var cPos in crops)
        {
            var b = GetBlockWorld(cPos.X, cPos.Y, cPos.Z);
            if (b == (byte)BlockType.Wheat)
            {
                var stage = GetBlockMetadata(cPos.X, cPos.Y, cPos.Z);
                if (stage < 7)
                {
                    if (!CropStageTimers.TryGetValue(cPos, out var t)) t = 25.0f + Random.Shared.NextSingle() * 10.0f;
                    t -= step;
                    if (t <= 0f)
                    {
                        var newStage = (byte)(stage + 1);
                        SetBlockMetadata(cPos.X, cPos.Y, cPos.Z, newStage);
                        if (newStage < 7)
                            CropStageTimers[cPos] = 25.0f + Random.Shared.NextSingle() * 10.0f;
                        else
                            CropStageTimers.TryRemove(cPos, out _);
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

    private void UpdateSaplingGrowth(float deltaTime)
    {
        if (GrowingSaplings.Count == 0) return;

        saplingGrowthTimer += deltaTime;
        if (saplingGrowthTimer < 1.0f) return;

        var step = saplingGrowthTimer;
        saplingGrowthTimer = 0f;

        var saplingEntries = GrowingSaplings.ToArray();
        foreach (var kvp in saplingEntries)
        {
            var sPos = kvp.Key;
            var remaining = kvp.Value - step;

            if (remaining <= 0f)
            {
                GrowingSaplings.TryRemove(sPos, out _);

                var b = GetBlockWorld(sPos.X, sPos.Y, sPos.Z);
                if (b == (byte)BlockType.OakSapling)
                {
                    var hasClearance = true;
                    for (var cy = 1; cy <= 5; cy++)
                    {
                        var above = GetBlockWorld(sPos.X, sPos.Y + cy, sPos.Z);
                        if (above != (byte)BlockType.Empty && above != (byte)BlockType.Leaves)
                        {
                            hasClearance = false;
                            break;
                        }
                    }

                    if (hasClearance)
                    {
                        SetBlockWorld(sPos.X, sPos.Y, sPos.Z, BlockType.Empty);
                        var biome = GetBiome(sPos.X, sPos.Z);
                        if (biome == BiomeType.Mountains)
                            generator.GrowSpruceTree(this, sPos.X, sPos.Y, sPos.Z);
                        else if (biome == BiomeType.Forest &&
                                 WorldGenerator.HashCoordinates(sPos.X, 55, sPos.Z, Seed) % 3 == 0)
                            generator.GrowBirchTree(this, sPos.X, sPos.Y, sPos.Z);
                        else
                            generator.GrowTree(this, sPos.X, sPos.Y, sPos.Z);
                    }
                }
            }
            else
            {
                GrowingSaplings[sPos] = remaining;
            }
        }
    }

    private void UpdateLeafDecay(float deltaTime)
    {
        if (PendingDecayingLeaves.Count == 0) return;

        leafDecayTimer += deltaTime;
        if (leafDecayTimer < 1.0f) return;

        var step = leafDecayTimer;
        leafDecayTimer = 0f;

        var leafEntries = PendingDecayingLeaves.ToArray();
        foreach (var kvp in leafEntries)
        {
            var lPos = kvp.Key;
            var remaining = kvp.Value - step;

            if (remaining <= 0f)
            {
                PendingDecayingLeaves.TryRemove(lPos, out _);

                var b = GetBlockWorld(lPos.X, lPos.Y, lPos.Z);
                if (b is (byte)BlockType.Leaves or (byte)BlockType.SpruceLeaves)
                    if (!HasLogWithin(lPos, 4))
                    {
                        SetBlockWorld(lPos.X, lPos.Y, lPos.Z, BlockType.Empty);
                        if (Random.Shared.Next(100) < 10 && PlayerRef != null)
                            PlayerRef.DroppedItems.Add(new ItemDrop(BlockType.OakSapling,
                                new Vector3(lPos.X + 0.5f, lPos.Y + 0.5f, lPos.Z + 0.5f)));
                    }
            }
            else
            {
                PendingDecayingLeaves[lPos] = remaining;
            }
        }
    }

    private void UpdateDirtGrassSpread(float deltaTime)
    {
        if (PendingDirtGrassSpread.Count == 0) return;

        dirtSpreadTimer += deltaTime;
        if (dirtSpreadTimer < 1.0f) return;

        var step = dirtSpreadTimer;
        dirtSpreadTimer = 0f;

        var entries = PendingDirtGrassSpread.ToArray();
        foreach (var kvp in entries)
        {
            var dPos = kvp.Key;
            var remaining = kvp.Value - step;

            if (remaining <= 0f)
            {
                PendingDirtGrassSpread.TryRemove(dPos, out _);

                if (GetBlockWorld(dPos.X, dPos.Y, dPos.Z) == (byte)BlockType.Dirt)
                {
                    var above = GetBlockWorld(dPos.X, dPos.Y + 1, dPos.Z);
                    var clear = !BlockRegistry.IsSolid(above) || BlockRegistry.Get(above).IsTransparent;
                    if (clear && HasAdjacentGrass(dPos.X, dPos.Y, dPos.Z))
                        SetBlockWorld(dPos.X, dPos.Y, dPos.Z, BlockType.Grass);
                }
            }
            else
            {
                PendingDirtGrassSpread[dPos] = remaining;
            }
        }
    }

    private void UpdateNightSpidersAndWebs(Vector3 viewPosition, float deltaTime)
    {
        if (DayNightCycle == null || (DayNightCycle.CurrentPhase != DayNightPhase.Night &&
                                      DayNightCycle.CurrentPhase != DayNightPhase.Dusk)) return;

        cobwebSpawnTimer += deltaTime;
        if (cobwebSpawnTimer < 1.5f) return;

        cobwebSpawnTimer = 0f;
        for (var i = 0; i < 4; i++)
        {
            var rx = (int)viewPosition.X + Random.Shared.Next(-28, 29);
            var rz = (int)viewPosition.Z + Random.Shared.Next(-28, 29);
            if (!IsInsideWorld(rx, rz)) continue;

            var surfaceY = GetSurfaceHeight(rx, rz);
            for (var ry = surfaceY + 7; ry >= surfaceY + 1; ry--)
            {
                var b = GetBlockWorld(rx, ry, rz);
                if (b is (byte)BlockType.Leaves or (byte)BlockType.SpruceLeaves)
                {
                    var targetY = ry - 1;
                    if (targetY > 0 && GetBlockWorld(rx, targetY, rz) == (byte)BlockType.Empty)
                    {
                        var nearbyWebs = 0;
                        for (var dx = -2; dx <= 2 && nearbyWebs < 2; dx++)
                        for (var dz = -2; dz <= 2 && nearbyWebs < 2; dz++)
                            if (GetBlockWorld(rx + dx, targetY, rz + dz) == (byte)BlockType.Cobweb)
                                nearbyWebs++;

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

    public void Update(float deltaTime = 0f, int maxUploadsPerFrame = 6)
    {
        Update(new Vector3(WORLD_SIZE_X * 0.5f, 64f, WORLD_SIZE_Z * 0.5f), deltaTime, maxUploadsPerFrame);
    }

    private void StreamChunksAround(int centerCX, int centerCZ)
    {
        var r = ViewDistanceChunks;
        toEnqueueChunks.Clear();

        for (var dx = -r; dx <= r; dx++)
        for (var dz = -r; dz <= r; dz++)
        {
            var distSq = dx * dx + dz * dz;
            if (distSq <= r * r)
            {
                var cx = centerCX + dx;
                var cz = centerCZ + dz;
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
                        toEnqueueChunks.Add((coord, distSq));
                }
            }
        }

        if (toEnqueueChunks.Count > 1) toEnqueueChunks.Sort((a, b) => a.distSq.CompareTo(b.distSq));

        var budget = Math.Min(32, toEnqueueChunks.Count);
        for (var i = 0; i < budget; i++) EnqueueChunk(toEnqueueChunks[i].coord);

        var unloadDistSq = (r + 4) * (r + 4);
        foreach (var chunk in chunks.Values)
            if (chunk.HasMesh)
            {
                var dx = chunk.ChunkCoord.X - centerCX;
                var dz = chunk.ChunkCoord.Y - centerCZ;
                if (dx * dx + dz * dz > unloadDistSq) chunk.DeleteGLBuffers();
            }
    }

    public void Render(ShaderProgram shaderProgram, Camera? camera = null)
    {
        shaderProgram.Bind();

        visibleOpaqueChunks.Clear();
        visibleWaterChunks.Clear();

        if (camera != null)
        {
            var view = camera.GetViewMatrix();
            var proj = camera.GetProjectionMatrix();
            cameraFrustum.Update(view * proj);

            var renderDistSq = MathF.Pow((ViewDistanceChunks + 1) * Chunk.SIZE_X, 2);
            var camPos = camera.Position;

            foreach (var chunk in chunks.Values)
            {
                if (!chunk.HasMesh) continue;

                var chunkCenterX = chunk.Position.X + Chunk.SIZE_X * 0.5f;
                var chunkCenterZ = chunk.Position.Z + Chunk.SIZE_Z * 0.5f;

                var dx = chunkCenterX - camPos.X;
                var dz = chunkCenterZ - camPos.Z;
                var distSq = dx * dx + dz * dz;

                if (distSq > renderDistSq) continue;

                if (distSq > 24f * 24f)
                {
                    var min = chunk.Position - new Vector3(2f, 0f, 2f);
                    var max = new Vector3(chunk.Position.X + Chunk.SIZE_X + 2f, Chunk.SIZE_Y,
                        chunk.Position.Z + Chunk.SIZE_Z + 2f);
                    if (!cameraFrustum.IsBoxVisible(min, max)) continue;
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

        var transLoc = TranslucentPassUniformLoc >= 0
            ? TranslucentPassUniformLoc
            : GL.GetUniformLocation(shaderProgram.ID, "isTranslucentPass");

        GL.Uniform1i(transLoc, 0);
        GL.Disable(EnableCap.Blend);
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);

        for (var i = 0; i < visibleOpaqueChunks.Count; i++) visibleOpaqueChunks[i].RenderOpaque();

        if (visibleWaterChunks.Count > 0)
        {
            GL.Uniform1i(transLoc, 1);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);

            for (var i = 0; i < visibleWaterChunks.Count; i++) visibleWaterChunks[i].RenderWater();

            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.DepthFunc(DepthFunction.Less);
        }

        lock (Minecarts)
        {
            if (Minecarts.Count > 0) MinecartRenderer.Render(Minecarts, shaderProgram);
        }
    }

    public void Destroy()
    {
        cancellationTokenSource.Cancel();
        workAvailable.Set();

        foreach (var thread in workerThreads) thread.Join(200);

        foreach (var chunk in chunks.Values) chunk.DeleteGLBuffers();
        chunks.Clear();

        while (readyToUploadMeshes.TryDequeue(out var item)) item.mesh.Dispose();

        MinecartRenderer.Cleanup();
        lock (Minecarts)
        {
            Minecarts.Clear();
        }

        while (pendingSpawnedMinecarts.TryDequeue(out _))
        {
        }

        lightSources.Clear();
    }

    private void ScanChunkLightSources(Chunk chunk)
    {
        lock (chunk.LightPositions)
        {
            chunk.LightPositions.Clear();
            for (var x = 0; x < Chunk.SIZE_X; x++)
            for (var y = 0; y < Chunk.SIZE_Y; y++)
            for (var z = 0; z < Chunk.SIZE_Z; z++)
            {
                var b = chunk.GetBlock(x, y, z);
                if (BlockRegistry.Get(b).IsLightSource)
                {
                    var wx = (int)chunk.Position.X + x;
                    var wz = (int)chunk.Position.Z + z;
                    var lpos = new Vector3i(wx, y, wz);
                    lightSources.TryAdd(lpos, 0);
                    chunk.LightPositions.Add(lpos);
                }
            }
        }
    }

    public List<(Vector3 Position, Vector3 Color, float Radius, float Intensity)> GetActivePointLights(
        Vector3 playerPos, int maxCount = 15, float deltaTime = 0.016f)
    {
        lightQueryTimer += deltaTime;
        var moveDistSq = Vector3.DistanceSquared(playerPos, lastLightQueryPos);

        if (moveDistSq < 0.64f && lightQueryTimer < 0.05f) return activePointLights;

        lightQueryTimer = 0f;
        lastLightQueryPos = playerPos;
        activePointLights.Clear();
        lightCandidates.Clear();

        var centerCX = (int)Math.Floor(playerPos.X / Chunk.SIZE_X);
        var centerCZ = (int)Math.Floor(playerPos.Z / Chunk.SIZE_Z);

        for (var dx = -3; dx <= 3; dx++)
        for (var dz = -3; dz <= 3; dz++)
            if (chunks.TryGetValue(new Vector2i(centerCX + dx, centerCZ + dz), out var chunk))
                lock (chunk.LightPositions)
                {
                    var lights = chunk.LightPositions;
                    for (var k = 0; k < lights.Count; k++)
                    {
                        var pos = lights[k];
                        var lx = pos.X - playerPos.X;
                        var ly = pos.Y - playerPos.Y;
                        var lz = pos.Z - playerPos.Z;
                        var distSq = lx * lx + ly * ly + lz * lz;

                        if (distSq < 1600.0f) lightCandidates.Add((pos, distSq));
                    }
                }

        if (lightCandidates.Count > 1) lightCandidates.Sort((a, b) => a.DistSq.CompareTo(b.DistSq));

        for (var i = 0; i < lightCandidates.Count && activePointLights.Count < maxCount; i++)
        {
            var pos = lightCandidates[i].Pos;
            var blockId = GetBlockWorld(pos.X, pos.Y, pos.Z);

            if (blockId == (byte)BlockType.Empty || !BlockRegistry.Get(blockId).IsLightSource) continue;

            if (blockId == (byte)BlockType.Portal)
            {
                var duplicatePortal = false;
                for (var p = 0; p < activePointLights.Count; p++)
                {
                    var existing = activePointLights[p];
                    var pdx = existing.Position.X - pos.X;
                    var pdy = existing.Position.Y - pos.Y;
                    var pdz = existing.Position.Z - pos.Z;
                    if (pdx * pdx + pdy * pdy + pdz * pdz < 9.0f)
                    {
                        duplicatePortal = true;
                        break;
                    }
                }

                if (duplicatePortal) continue;
            }

            var (lightColor, radius, intensity, yOffset) = (BlockType)blockId switch
            {
                BlockType.Lava => (new Vector3(1.0f, 0.45f, 0.15f), 12.0f, 1.4f, 0f),
                BlockType.Portal => (new Vector3(0.85f, 0.25f, 1.0f), 16.0f, 1.7f, 0f),
                BlockType.Glowstone => (new Vector3(1.0f, 0.92f, 0.60f), 15.0f, 1.5f, 0f),
                BlockType.JackOLantern => (new Vector3(1.0f, 0.65f, 0.20f), 15.0f, 1.5f, 0f),
                BlockType.LitFurnace => (new Vector3(1.0f, 0.55f, 0.20f), 13.0f, 1.4f, 0f),
                BlockType.RedstoneTorch => (new Vector3(1.0f, 0.15f, 0.10f), 11.0f, 1.2f, 0.2f),
                _ => (new Vector3(1.0f, 0.74f, 0.40f), 15.0f, 1.4f, 0.2f)
            };

            Vector3 lightPos = new(pos.X, pos.Y + yOffset, pos.Z);
            activePointLights.Add((lightPos, lightColor, radius, intensity));
        }

        return activePointLights;
    }
}