using OpenTK.Mathematics;
using VelmoraCraft.src.Entities;

namespace VelmoraCraft.src.World;

public enum BiomeType : byte
{
    Plains,
    Forest,
    Desert,
    Mountains
}

public class WorldGenerator
{
    public const int SEA_LEVEL = 36;
    public const int LAVA_LEVEL = 10;
    private readonly FastNoiseLite caveNoiseA;
    private readonly FastNoiseLite caveNoiseB;
    private readonly FastNoiseLite continentalNoise;
    private readonly FastNoiseLite elevationNoise;
    private readonly FastNoiseLite moistureNoise;
    private readonly int seed;
    private readonly FastNoiseLite temperatureNoise;

    public WorldGenerator(int seed)
    {
        this.seed = seed;

        elevationNoise = new FastNoiseLite(seed);
        elevationNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        elevationNoise.SetFrequency(0.012f);
        elevationNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        elevationNoise.SetFractalOctaves(3);

        continentalNoise = new FastNoiseLite(seed + 101);
        continentalNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        continentalNoise.SetFrequency(0.005f);

        caveNoiseA = new FastNoiseLite(seed + 202);
        caveNoiseA.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        caveNoiseA.SetFrequency(0.035f);

        caveNoiseB = new FastNoiseLite(seed + 303);
        caveNoiseB.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        caveNoiseB.SetFrequency(0.035f);

        temperatureNoise = new FastNoiseLite(seed + 404);
        temperatureNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        temperatureNoise.SetFrequency(0.004f);

        moistureNoise = new FastNoiseLite(seed + 505);
        moistureNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        moistureNoise.SetFrequency(0.004f);
    }

    public BiomeType GetBiome(int wx, int wz)
    {
        var temp = temperatureNoise.GetNoise(wx, wz);
        var moisture = moistureNoise.GetNoise(wx, wz);

        if (temp > 0.3f && moisture < -0.1f) return BiomeType.Desert;
        if (temp < -0.2f) return BiomeType.Mountains;
        if (moisture > 0.1f) return BiomeType.Forest;

        return BiomeType.Plains;
    }

    public void GenerateChunkTerrain(Chunk chunk)
    {
        var originX = chunk.ChunkCoord.X * Chunk.SIZE_X;
        var originZ = chunk.ChunkCoord.Y * Chunk.SIZE_Z;

        for (var lx = 0; lx < Chunk.SIZE_X; lx++)
        for (var lz = 0; lz < Chunk.SIZE_Z; lz++)
        {
            var wx = originX + lx;
            var wz = originZ + lz;

            var biome = GetBiome(wx, wz);
            var baseElev = elevationNoise.GetNoise(wx, wz);
            var cont = continentalNoise.GetNoise(wx, wz);

            var targetHeight = biome switch
            {
                BiomeType.Mountains => 45f + cont * 30f + baseElev * 20f,
                BiomeType.Desert => 35f + baseElev * 8f,
                BiomeType.Forest => 38f + baseElev * 14f,
                _ => 36f + baseElev * 12f
            };

            var surfaceHeight = Math.Clamp((int)targetHeight, 15, Chunk.SIZE_Y - 10);

            for (var y = 0; y < Chunk.SIZE_Y; y++)
            {
                if (y == 0)
                {
                    chunk.SetBlock(lx, y, lz, BlockType.Bedrock);
                    continue;
                }

                if (y > surfaceHeight)
                {
                    chunk.SetBlock(lx, y, lz, y <= SEA_LEVEL ? BlockType.Water : BlockType.Empty);
                    continue;
                }

                if (y > 3 && y < surfaceHeight - 2)
                {
                    var cA = caveNoiseA.GetNoise(wx, y * 1.5f, wz);
                    var cB = caveNoiseB.GetNoise(wx, y * 1.5f, wz);

                    if (MathF.Abs(cA) < 0.12f && MathF.Abs(cB) < 0.12f)
                    {
                        chunk.SetBlock(lx, y, lz, y <= LAVA_LEVEL ? BlockType.Lava : BlockType.Empty);
                        continue;
                    }
                }

                if (y == surfaceHeight)
                {
                    if (surfaceHeight < SEA_LEVEL + 2 && biome != BiomeType.Mountains)
                        chunk.SetBlock(lx, y, lz, BlockType.Sand);
                    else if (biome == BiomeType.Desert)
                        chunk.SetBlock(lx, y, lz, BlockType.Sand);
                    else if (biome == BiomeType.Mountains && surfaceHeight >= 50)
                        chunk.SetBlock(lx, y, lz, BlockType.SnowyGrass);
                    else
                        chunk.SetBlock(lx, y, lz, BlockType.Grass);
                }
                else if (y > surfaceHeight - 4)
                {
                    if (biome == BiomeType.Desert)
                        chunk.SetBlock(lx, y, lz, BlockType.Sandstone);
                    else if (surfaceHeight < SEA_LEVEL + 2 && biome != BiomeType.Mountains)
                        chunk.SetBlock(lx, y, lz,
                            HashCoordinates(wx, y, wz, seed) % 5 == 0 ? BlockType.Clay : BlockType.Sand);
                    else if (biome == BiomeType.Mountains && surfaceHeight >= 50)
                        chunk.SetBlock(lx, y, lz, BlockType.SnowBlock);
                    else
                        chunk.SetBlock(lx, y, lz, BlockType.Dirt);
                }
                else
                {
                    var hash = HashCoordinates(wx, y, wz, seed);
                    var oreRoll = hash % 1000;
                    var rockType = BlockType.Stone;

                    if (y <= 12 && oreRoll < 12) rockType = BlockType.Obsidian;
                    else if (y <= 14 && oreRoll < 22) rockType = BlockType.DiamondOre;
                    else if (y <= 18 && oreRoll < 42) rockType = BlockType.RedstoneOre;
                    else if (y <= 32 && oreRoll < 24) rockType = BlockType.GoldOre;
                    else if (y >= 10 && y <= 34 && oreRoll >= 42 && oreRoll < 58) rockType = BlockType.LapisOre;
                    else if (y <= 50 && oreRoll < 85) rockType = BlockType.IronOre;
                    else if (y <= 75 && oreRoll < 140) rockType = BlockType.CoalOre;
                    else if (oreRoll >= 880 && oreRoll < 915) rockType = BlockType.Gravel;
                    else if (oreRoll >= 915 && oreRoll < 935) rockType = BlockType.Dirt;
                    else if (biome == BiomeType.Desert && y > surfaceHeight - 8) rockType = BlockType.Sandstone;

                    chunk.SetBlock(lx, y, lz, rockType);
                }
            }
        }
    }

    public void DecorateChunk(World world, Chunk chunk)
    {
        var originX = chunk.ChunkCoord.X * Chunk.SIZE_X;
        var originZ = chunk.ChunkCoord.Y * Chunk.SIZE_Z;

        for (var lx = 2; lx < Chunk.SIZE_X - 2; lx++)
        for (var lz = 2; lz < Chunk.SIZE_Z - 2; lz++)
        {
            var wx = originX + lx;
            var wz = originZ + lz;

            var biome = GetBiome(wx, wz);

            if (biome == BiomeType.Desert)
            {
                var cactusHash = HashCoordinates(wx, 55, wz, seed + 111);
                if (cactusHash % 90 == 0)
                {
                    var surfaceY = world.GetSurfaceHeight(wx, wz);
                    if (surfaceY > SEA_LEVEL && world.GetBlockWorld(wx, surfaceY, wz) == (byte)BlockType.Sand)
                    {
                        var cactusHeight = 2 + cactusHash % 2;
                        var canPlace = !IsLiquid(world.GetBlockWorld(wx + 1, surfaceY, wz)) &&
                                       !IsLiquid(world.GetBlockWorld(wx - 1, surfaceY, wz)) &&
                                       !IsLiquid(world.GetBlockWorld(wx, surfaceY, wz + 1)) &&
                                       !IsLiquid(world.GetBlockWorld(wx, surfaceY, wz - 1));

                        if (canPlace)
                            for (var cy = 1; cy <= cactusHeight; cy++)
                            {
                                var py = surfaceY + cy;
                                if (py >= Chunk.SIZE_Y || world.GetBlockWorld(wx, py, wz) != (byte)BlockType.Empty)
                                {
                                    canPlace = false;
                                    break;
                                }

                                if (IsInvalidCactusNeighbor(world.GetBlockWorld(wx + 1, py, wz)) ||
                                    IsInvalidCactusNeighbor(world.GetBlockWorld(wx - 1, py, wz)) ||
                                    IsInvalidCactusNeighbor(world.GetBlockWorld(wx, py, wz + 1)) ||
                                    IsInvalidCactusNeighbor(world.GetBlockWorld(wx, py, wz - 1)))
                                {
                                    canPlace = false;
                                    break;
                                }
                            }

                        if (canPlace)
                            for (var cy = 1; cy <= cactusHeight; cy++)
                                world.SetBlockWorld(wx, surfaceY + cy, wz, BlockType.Cactus);
                    }
                }

                continue;
            }

            var treeChance = biome == BiomeType.Forest ? 35 : 160;
            var treeHash = HashCoordinates(wx, 77, wz, seed + 888);

            if (treeHash % treeChance == 0)
            {
                for (var y = Chunk.SIZE_Y - 10; y >= SEA_LEVEL; y--)
                {
                    var block = chunk.GetBlock(lx, y, lz);
                    if (block is (byte)BlockType.Grass or (byte)BlockType.SnowyGrass)
                    {
                        if (biome == BiomeType.Mountains)
                            GrowSpruceTree(world, wx, y + 1, wz);
                        else if (biome == BiomeType.Forest && treeHash % 3 == 0)
                            GrowBirchTree(world, wx, y + 1, wz);
                        else
                            GrowTree(world, wx, y + 1, wz);
                        break;
                    }
                }

                continue;
            }

            var floraHash = HashCoordinates(wx, 99, wz, seed + 999);
            var surface = world.GetSurfaceHeight(wx, wz);
            var groundBlock = world.GetBlockWorld(wx, surface, wz);

            if (groundBlock == (byte)BlockType.Grass &&
                world.GetBlockWorld(wx, surface + 1, wz) == (byte)BlockType.Empty)
            {
                if (floraHash % 7 == 0)
                    world.SetBlockWorld(wx, surface + 1, wz, BlockType.TallGrass);
                else if (floraHash % 45 == 0)
                    world.SetBlockWorld(wx, surface + 1, wz, floraHash % 2 == 0 ? BlockType.Rose : BlockType.Dandelion);
                else if (floraHash % 48 == 0)
                    world.SetBlockWorld(wx, surface + 1, wz,
                        floraHash % 2 == 0 ? BlockType.RedMushroom : BlockType.BrownMushroom);
                else if (floraHash % 350 == 0) world.SetBlockWorld(wx, surface + 1, wz, BlockType.Pumpkin);
            }
            else if (groundBlock == (byte)BlockType.Dirt &&
                     world.GetBlockWorld(wx, surface + 1, wz) == (byte)BlockType.Empty && floraHash % 30 == 0)
            {
                world.SetBlockWorld(wx, surface + 1, wz,
                    floraHash % 2 == 0 ? BlockType.RedMushroom : BlockType.BrownMushroom);
            }
            else if ((groundBlock == (byte)BlockType.Sand || groundBlock == (byte)BlockType.Dirt) &&
                     surface <= SEA_LEVEL + 1)
            {
                if (floraHash % 70 == 0 && world.GetBlockWorld(wx, surface + 1, wz) == (byte)BlockType.Empty)
                {
                    var caneHeight = 2 + floraHash % 2;
                    for (var cy = 1; cy <= caneHeight; cy++)
                        world.SetBlockWorld(wx, surface + cy, wz, BlockType.SugarCane);
                }
            }

            var dungeonHash = HashCoordinates(wx, 33, wz, seed + 777);
            if (dungeonHash % 900 == 0)
            {
                var dungeonY = 12 + dungeonHash % 12;
                if (world.GetBlockWorld(wx, dungeonY, wz) == (byte)BlockType.Stone)
                    GenerateDungeonRoom(world, wx, dungeonY, wz);
            }
        }

        DecorateChunkStructures(world, chunk);
    }

    private void GenerateDungeonRoom(World world, int cx, int cy, int cz)
    {
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        for (var dy = 0; dy <= 3; dy++)
        {
            var x = cx + dx;
            var y = cy + dy;
            var z = cz + dz;

            var isFloor = dy == 0;
            var isWall = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
            var isCeiling = dy == 3;

            if (isFloor || isWall || isCeiling)
            {
                var cobble = (dx + dz + dy) % 3 == 0 ? BlockType.MossyCobblestone : BlockType.Cobblestone;
                world.SetBlockWorld(x, y, z, cobble);
            }
            else
            {
                world.SetBlockWorld(x, y, z, BlockType.Empty);
            }
        }

        world.SetBlockWorld(cx, cy + 1, cz, BlockType.MonsterSpawner);
        world.SetBlockWorld(cx - 1, cy + 2, cz - 1, BlockType.Cobweb);
        world.SetBlockWorld(cx + 1, cy + 2, cz + 1, BlockType.Cobweb);
        world.SetBlockWorld(cx - 1, cy + 2, cz + 1, BlockType.Cobweb);
        world.SetBlockWorld(cx + 1, cy + 2, cz - 1, BlockType.Cobweb);
    }

    public void GrowBirchTree(World world, int wx, int trunkBaseY, int wz)
    {
        var trunkHeight = 6;
        for (var y = 0; y < trunkHeight; y++) world.SetBlockWorld(wx, trunkBaseY + y, wz, BlockType.BirchLog);

        var leafBase = trunkBaseY + trunkHeight - 2;
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        for (var dy = 0; dy <= 2; dy++)
        {
            if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2 && dy == 2) continue;

            var leafX = wx + dx;
            var leafY = leafBase + dy;
            var leafZ = wz + dz;

            if (world.GetBlockWorld(leafX, leafY, leafZ) == (byte)BlockType.Empty)
                world.SetBlockWorld(leafX, leafY, leafZ, BlockType.Leaves);
        }

        world.SetBlockWorld(wx, trunkBaseY + trunkHeight + 1, wz, BlockType.Leaves);
    }

    public void GrowSpruceTree(World world, int wx, int trunkBaseY, int wz)
    {
        var trunkHeight = 7;
        for (var y = 0; y < trunkHeight; y++) world.SetBlockWorld(wx, trunkBaseY + y, wz, BlockType.SpruceLog);

        for (var y = trunkHeight - 4; y <= trunkHeight; y++)
        {
            var radius = y >= trunkHeight - 1 ? 1 : 2;
            for (var dx = -radius; dx <= radius; dx++)
            for (var dz = -radius; dz <= radius; dz++)
            {
                if (Math.Abs(dx) == radius && Math.Abs(dz) == radius && y == trunkHeight - 4) continue;

                var leafX = wx + dx;
                var leafY = trunkBaseY + y;
                var leafZ = wz + dz;

                if (world.GetBlockWorld(leafX, leafY, leafZ) == (byte)BlockType.Empty)
                    world.SetBlockWorld(leafX, leafY, leafZ, BlockType.Leaves);
            }
        }

        world.SetBlockWorld(wx, trunkBaseY + trunkHeight + 1, wz, BlockType.Leaves);
    }

    public void GrowTree(World world, int wx, int trunkBaseY, int wz)
    {
        var trunkHeight = 5;
        for (var y = 0; y < trunkHeight; y++) world.SetBlockWorld(wx, trunkBaseY + y, wz, BlockType.Log);

        var leafBase = trunkBaseY + trunkHeight - 2;
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        for (var dy = 0; dy <= 2; dy++)
        {
            if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2 && dy == 2) continue;

            var leafX = wx + dx;
            var leafY = leafBase + dy;
            var leafZ = wz + dz;

            if (world.GetBlockWorld(leafX, leafY, leafZ) == (byte)BlockType.Empty)
                world.SetBlockWorld(leafX, leafY, leafZ, BlockType.Leaves);
        }

        world.SetBlockWorld(wx, trunkBaseY + trunkHeight + 1, wz, BlockType.Leaves);

        if (Random.Shared.Next(100) < 15)
        {
            var cwx = wx + (Random.Shared.Next(2) == 0 ? -1 : 1);
            var cwz = wz + (Random.Shared.Next(2) == 0 ? -1 : 1);
            var cwy = leafBase - 1;
            if (cwy > 0 && world.GetBlockWorld(cwx, cwy, cwz) == (byte)BlockType.Empty)
                world.SetBlockWorld(cwx, cwy, cwz, BlockType.Cobweb);
        }
    }

    private static int FloorDiv(int a, int b)
    {
        var rem = a % b;
        return a < 0 && rem != 0 ? a / b - 1 : a / b;
    }

    private bool IsRegionCandidateChunk(int chunkX, int chunkZ, int regionSize, int saltX, int saltZ, int saltSeed)
    {
        var rx = FloorDiv(chunkX, regionSize);
        var rz = FloorDiv(chunkZ, regionSize);
        var candX = rx * regionSize + HashCoordinates(rx, saltX, rz, seed + saltSeed) % regionSize;
        var candZ = rz * regionSize + HashCoordinates(rx, saltZ, rz, seed + saltSeed + 99) % regionSize;
        return chunkX == candX && chunkZ == candZ;
    }

    private void DecorateChunkStructures(World world, Chunk chunk)
    {
        var chunkX = chunk.ChunkCoord.X;
        var chunkZ = chunk.ChunkCoord.Y;
        var originX = chunkX * Chunk.SIZE_X;
        var originZ = chunkZ * Chunk.SIZE_Z;
        var cx = originX + 8;
        var cz = originZ + 8;

        if (!World.IsInsideWorld(cx, cz)) return;

        var chunkHash = HashCoordinates(chunkX, 42, chunkZ, seed + 12345);
        var centerBiome = GetBiome(cx, cz);
        var surfaceY = world.GetSurfaceHeight(cx, cz);

        if (IsRegionCandidateChunk(chunkX, chunkZ, 13, 31, 32, 3003) &&
            centerBiome is BiomeType.Plains or BiomeType.Forest &&
            surfaceY > SEA_LEVEL + 1)
            GenerateVillage(world, cx, surfaceY, cz, chunkHash);
        else if (IsRegionCandidateChunk(chunkX, chunkZ, 11, 41, 42, 4004) &&
                 centerBiome != BiomeType.Desert &&
                 surfaceY > SEA_LEVEL + 2)
            GenerateSurfaceRailwayAndMineshaft(world, cx, surfaceY, cz, chunkHash);
        else if (IsRegionCandidateChunk(chunkX, chunkZ, 10, 51, 52, 5005) &&
                 surfaceY > SEA_LEVEL)
            GenerateAncientRuins(world, cx, surfaceY, cz, chunkHash);
        else if (IsRegionCandidateChunk(chunkX, chunkZ, 8, 11, 12, 1001) &&
                 centerBiome == BiomeType.Desert &&
                 surfaceY > SEA_LEVEL)
            GenerateDesertWell(world, cx, surfaceY, cz);
        else if (IsRegionCandidateChunk(chunkX, chunkZ, 8, 21, 22, 2002) &&
                 centerBiome == BiomeType.Mountains &&
                 surfaceY >= 50)
            GenerateMountainCamp(world, cx, surfaceY, cz, chunkHash);

        if (IsRegionCandidateChunk(chunkX, chunkZ, 8, 61, 62, 6006))
        {
            var undergroundHash = HashCoordinates(chunkX, 91, chunkZ, seed + 67890);
            var cy = 18 + undergroundHash % 14;
            GenerateUndergroundMineshaft(world, cx, cy, cz, undergroundHash);
        }
    }

    private static void PlaceChestWithLoot(World world, int x, int y, int z, params (BlockType item, int count)[] items)
    {
        world.SetBlockWorld(x, y, z, BlockType.Chest);
        if (world.ChestManager != null)
        {
            var inv = world.ChestManager.GetOrCreateChest(new Vector3i(x, y, z));
            foreach (var (item, count) in items) inv.AddItem(item, count);
        }
    }

    private void GenerateDesertWell(World world, int cx, int surfaceY, int cz)
    {
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        {
            var x = cx + dx;
            var z = cz + dz;

            world.SetBlockWorld(x, surfaceY - 2, z, BlockType.Sandstone);

            var isBorder = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
            var isCorner = Math.Abs(dx) == 2 && Math.Abs(dz) == 2;

            if (isBorder)
            {
                world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Sandstone);
                world.SetBlockWorld(x, surfaceY, z, BlockType.Sandstone);

                if (isCorner)
                    for (var py = 1; py <= 3; py++)
                        world.SetBlockWorld(x, surfaceY + py, z, BlockType.Bricks);
                else
                    world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Sandstone);
            }
            else
            {
                world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Water);
                world.SetBlockWorld(x, surfaceY, z, BlockType.Water);
                world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Empty);
                world.SetBlockWorld(x, surfaceY + 2, z, BlockType.Empty);
                world.SetBlockWorld(x, surfaceY + 3, z, BlockType.Empty);
            }

            var roofBlock = isBorder ? BlockType.Bricks : BlockType.Sandstone;
            world.SetBlockWorld(x, surfaceY + 4, z, roofBlock);
        }

        world.SetBlockWorld(cx, surfaceY + 5, cz, BlockType.Sandstone);
        world.SetBlockWorld(cx, surfaceY + 3, cz, BlockType.Torch);

        PlaceChestWithLoot(world, cx + 2, surfaceY + 1, cz + 1,
            (BlockType.Bread, 3),
            (BlockType.GoldOre, 4),
            (BlockType.Sandstone, 8),
            (BlockType.Torch, 4));
    }

    private void GenerateMountainCamp(World world, int cx, int surfaceY, int cz, int seed)
    {
        var tentWool = seed % 3 == 0 ? BlockType.RedWool : seed % 3 == 1 ? BlockType.BlueWool : BlockType.WhiteWool;

        for (var dx = -3; dx <= 3; dx++)
        for (var dz = -3; dz <= 3; dz++)
        {
            var x = cx + dx;
            var z = cz + dz;
            world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);
            for (var dy = 1; dy <= 4; dy++) world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
        }

        for (var dz = -2; dz <= 2; dz++)
        {
            var z = cz + dz;
            for (var dx = -1; dx <= 1; dx++) world.SetBlockWorld(cx + dx, surfaceY, z, BlockType.Planks);

            world.SetBlockWorld(cx - 2, surfaceY + 1, z, tentWool);
            world.SetBlockWorld(cx + 2, surfaceY + 1, z, tentWool);
            world.SetBlockWorld(cx - 1, surfaceY + 2, z, tentWool);
            world.SetBlockWorld(cx + 1, surfaceY + 2, z, tentWool);
            world.SetBlockWorld(cx, surfaceY + 3, z, tentWool);
        }

        world.SetBlockWorld(cx - 1, surfaceY + 1, cz - 2, tentWool);
        world.SetBlockWorld(cx, surfaceY + 1, cz - 2, tentWool);
        world.SetBlockWorld(cx + 1, surfaceY + 1, cz - 2, tentWool);
        world.SetBlockWorld(cx, surfaceY + 2, cz - 2, tentWool);

        PlaceChestWithLoot(world, cx - 1, surfaceY + 1, cz - 1,
            (BlockType.Bread, 4),
            (BlockType.Cake, 1),
            (BlockType.Torch, 8),
            (BlockType.Coal, 6),
            (BlockType.IronIngot, 3),
            (BlockType.WheatSeeds, 4));

        world.SetBlockWorld(cx, surfaceY + 2, cz, BlockType.Torch);

        world.SetBlockWorld(cx + 3, surfaceY + 1, cz, BlockType.CraftingTable);
        world.SetBlockWorld(cx + 3, surfaceY + 1, cz + 1, BlockType.Furnace);

        world.SetBlockWorld(cx + 1, surfaceY + 1, cz + 2, BlockType.Cobblestone);
        world.SetBlockWorld(cx + 3, surfaceY + 1, cz + 2, BlockType.Cobblestone);
        world.SetBlockWorld(cx + 2, surfaceY + 1, cz + 3, BlockType.Cobblestone);
        world.SetBlockWorld(cx + 2, surfaceY + 1, cz + 1, BlockType.Cobblestone);
        world.SetBlockWorld(cx + 2, surfaceY + 1, cz + 2, BlockType.Torch);

        world.SetBlockWorld(cx - 3, surfaceY + 1, cz + 3, BlockType.Cobblestone);
        world.SetBlockWorld(cx - 3, surfaceY + 2, cz + 3, BlockType.Torch);

        world.SetBlockWorld(cx + 3, surfaceY + 1, cz - 3, BlockType.Cobblestone);
        world.SetBlockWorld(cx + 3, surfaceY + 2, cz - 3, BlockType.Torch);
    }

    private void GenerateUndergroundMineshaft(World world, int cx, int cy, int cz, int seed)
    {
        var alongX = seed % 2 == 0;
        var length = 14;

        for (var step = 0; step < length; step++)
        {
            var sx = alongX ? cx + step : cx;
            var sz = alongX ? cz : cz + step;

            for (var w = -1; w <= 1; w++)
            {
                var px = alongX ? sx : sx + w;
                var pz = alongX ? sz + w : sz;

                var floorBlock = world.GetBlockWorld(px, cy - 1, pz);
                if (floorBlock is (byte)BlockType.Empty or (byte)BlockType.Water or (byte)BlockType.Lava)
                    world.SetBlockWorld(px, cy - 1, pz, BlockType.Planks);

                for (var dy = 0; dy < 3; dy++) world.SetBlockWorld(px, cy + dy, pz, BlockType.Empty);

                var ceilBlock = world.GetBlockWorld(px, cy + 3, pz);
                if (ceilBlock == (byte)BlockType.Empty) world.SetBlockWorld(px, cy + 3, pz, BlockType.Planks);
            }

            world.SetBlockWorld(sx, cy, sz, BlockType.Rail);

            if (step % 4 == 0)
            {
                var leftX = alongX ? sx : sx - 1;
                var leftZ = alongX ? sz - 1 : sz;
                var rightX = alongX ? sx : sx + 1;
                var rightZ = alongX ? sz + 1 : sz;

                world.SetBlockWorld(leftX, cy, leftZ, BlockType.Log);
                world.SetBlockWorld(leftX, cy + 1, leftZ, BlockType.Log);
                world.SetBlockWorld(leftX, cy + 2, leftZ, BlockType.Log);

                world.SetBlockWorld(rightX, cy, rightZ, BlockType.Log);
                world.SetBlockWorld(rightX, cy + 1, rightZ, BlockType.Log);
                world.SetBlockWorld(rightX, cy + 2, rightZ, BlockType.Log);

                world.SetBlockWorld(leftX, cy + 2, leftZ, BlockType.Planks);
                world.SetBlockWorld(sx, cy + 2, sz, BlockType.Planks);
                world.SetBlockWorld(rightX, cy + 2, rightZ, BlockType.Planks);

                world.SetBlockWorld(leftX, cy + 1, leftZ, BlockType.Torch);
            }
            else if ((step + seed) % 3 == 0)
            {
                var cobX = alongX ? sx : sx + (step % 2 == 0 ? -1 : 1);
                var cobZ = alongX ? sz + (step % 2 == 0 ? -1 : 1) : sz;
                world.SetBlockWorld(cobX, cy + 2, cobZ, BlockType.Cobweb);
            }
        }

        var chestX = alongX ? cx + length - 2 : cx + 1;
        var chestZ = alongX ? cz + 1 : cz + length - 2;
        PlaceChestWithLoot(world, chestX, cy, chestZ,
            (BlockType.Coal, 8),
            (BlockType.IronOre, 5),
            (BlockType.GoldOre, 2),
            (BlockType.RedstoneOre, 4),
            (BlockType.Rail, 12),
            (BlockType.PoweredRail, 2),
            (BlockType.Torch, 6),
            (BlockType.FlintAndSteel, 1));
    }

    private void GenerateSurfaceRailwayAndMineshaft(World world, int cx, int surfaceY, int cz, int seed)
    {
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        {
            var x = cx + dx;
            var z = cz + dz;

            world.SetBlockWorld(x, surfaceY, z, BlockType.Planks);
            for (var dy = 1; dy <= 4; dy++) world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
        }

        for (var dy = 1; dy <= 3; dy++)
        {
            world.SetBlockWorld(cx - 2, surfaceY + dy, cz - 2, BlockType.Log);
            world.SetBlockWorld(cx + 2, surfaceY + dy, cz - 2, BlockType.Log);
            world.SetBlockWorld(cx - 2, surfaceY + dy, cz + 2, BlockType.Log);
            world.SetBlockWorld(cx + 2, surfaceY + dy, cz + 2, BlockType.Log);
        }

        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
            world.SetBlockWorld(cx + dx, surfaceY + 4, cz + dz, BlockType.Planks);

        world.SetBlockWorld(cx - 1, surfaceY + 1, cz - 2, BlockType.CraftingTable);
        world.SetBlockWorld(cx + 1, surfaceY + 1, cz - 2, BlockType.Furnace);

        world.SetBlockWorld(cx - 2, surfaceY + 2, cz - 1, BlockType.Torch);
        world.SetBlockWorld(cx + 2, surfaceY + 2, cz - 1, BlockType.Torch);

        PlaceChestWithLoot(world, cx - 1, surfaceY + 1, cz + 2,
            (BlockType.MinecartItem, 2),
            (BlockType.Rail, 16),
            (BlockType.PoweredRail, 6),
            (BlockType.Coal, 10),
            (BlockType.IronIngot, 6),
            (BlockType.Torch, 8),
            (BlockType.Bread, 3));

        var trackLength = 24;
        var trackZ = cz;
        var trackY = surfaceY + 1;

        for (var i = 0; i < trackLength; i++)
        {
            var tx = cx + i;

            for (var by = trackY - 1; by >= SEA_LEVEL - 5; by--)
            {
                var b = world.GetBlockWorld(tx, by, trackZ);
                if (b != (byte)BlockType.Empty && b != (byte)BlockType.Water) break;
                world.SetBlockWorld(tx, by, trackZ, by == trackY - 1 ? BlockType.Gravel : BlockType.Planks);
            }

            world.SetBlockWorld(tx, trackY, trackZ, BlockType.Empty);
            world.SetBlockWorld(tx, trackY + 1, trackZ, BlockType.Empty);

            if (i > 0 && i % 8 == 0)
            {
                world.SetBlockWorld(tx, trackY, trackZ, BlockType.PoweredRailOn);
                world.SetBlockWorld(tx, trackY, trackZ + 1, BlockType.RedstoneTorch);
            }
            else
            {
                world.SetBlockWorld(tx, trackY, trackZ, BlockType.Rail);
            }
        }

        world.SetBlockWorld(cx + trackLength, trackY, trackZ, BlockType.Cobblestone);
        world.SetBlockWorld(cx + trackLength, trackY + 1, trackZ, BlockType.Torch);

        world.SpawnMinecart(new Minecart(new Vector3(cx + 1.5f, trackY + 0.1f, trackZ + 0.5f)));
    }

    private void GenerateVillage(World world, int cx, int surfaceY, int cz, int seed)
    {
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        {
            var x = cx + dx;
            var z = cz + dz;
            var isBorder = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
            var isCorner = Math.Abs(dx) == 2 && Math.Abs(dz) == 2;

            world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Cobblestone);

            if (isBorder)
            {
                world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);
                world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Cobblestone);

                if (isCorner)
                    for (var py = 1; py <= 3; py++)
                        world.SetBlockWorld(x, surfaceY + py, z, BlockType.Bricks);
            }
            else
            {
                world.SetBlockWorld(x, surfaceY - 2, z, BlockType.Cobblestone);
                world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Water);
                world.SetBlockWorld(x, surfaceY, z, BlockType.Water);
                for (var dy = 1; dy <= 3; dy++) world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
            }

            world.SetBlockWorld(x, surfaceY + 4, z, isBorder ? BlockType.Cobblestone : BlockType.Bricks);
        }

        world.SetBlockWorld(cx, surfaceY + 3, cz, BlockType.Torch);

        var hx = cx - 8;
        var hz = cz - 2;
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 3; dz++)
        {
            var x = hx + dx;
            var z = hz + dz;

            world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);
            var isWall = Math.Abs(dx) == 2 || dz == -2 || dz == 3;

            for (var dy = 1; dy <= 3; dy++)
                if (isWall)
                {
                    var isWindow = dy == 2 && ((Math.Abs(dx) == 2 && dz == 0) || (dz == -2 && dx == 0));
                    var isDoorway = dz == 3 && dx == 0 && (dy == 1 || dy == 2);

                    if (isWindow)
                        world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Glass);
                    else if (isDoorway)
                        world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                    else
                        world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Bricks);
                }
                else
                {
                    world.SetBlockWorld(x, surfaceY, z, BlockType.Planks);
                    world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                }

            world.SetBlockWorld(x, surfaceY + 4, z, BlockType.SmoothStone);
        }

        world.SetBlockWorld(hx, surfaceY + 1, hz + 3, BlockType.DoorLower);
        world.SetBlockWorld(hx, surfaceY + 2, hz + 3, BlockType.DoorUpper);

        world.SetBlockWorld(hx - 1, surfaceY + 1, hz - 1, BlockType.Bookshelf);
        world.SetBlockWorld(hx + 1, surfaceY + 1, hz - 1, BlockType.CraftingTable);
        world.SetBlockWorld(hx, surfaceY + 2, hz - 1, BlockType.Torch);

        PlaceChestWithLoot(world, hx - 1, surfaceY + 1, hz + 1,
            (BlockType.Bread, 5),
            (BlockType.Cake, 1),
            (BlockType.Wheat, 8),
            (BlockType.Torch, 6),
            (BlockType.Bookshelf, 2));

        var bx = cx + 8;
        var bz = cz - 2;
        for (var dx = -2; dx <= 3; dx++)
        for (var dz = -2; dz <= 2; dz++)
        {
            var x = bx + dx;
            var z = bz + dz;

            world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);

            var isBackOrSideWall = dz == -2 || dx == 3 || dz == 2;
            var isOpenFront = dx == -2;

            for (var dy = 1; dy <= 3; dy++)
                if (isOpenFront)
                {
                    var isPillar = dz == -2 || dz == 2;
                    world.SetBlockWorld(x, surfaceY + dy, z, isPillar ? BlockType.Stone : BlockType.Empty);
                }
                else if (isBackOrSideWall)
                {
                    world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Cobblestone);
                }
                else
                {
                    world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                }

            world.SetBlockWorld(x, surfaceY + 4, z, BlockType.Stone);
        }

        world.SetBlockWorld(bx + 1, surfaceY + 1, bz - 1, BlockType.LitFurnace);
        world.SetBlockWorld(bx + 2, surfaceY + 1, bz - 1, BlockType.Furnace);
        world.SetBlockWorld(bx + 1, surfaceY + 2, bz - 1, BlockType.Bricks);
        world.SetBlockWorld(bx + 1, surfaceY + 3, bz - 1, BlockType.Bricks);
        world.SetBlockWorld(bx + 1, surfaceY + 4, bz - 1, BlockType.Bricks);
        world.SetBlockWorld(bx + 1, surfaceY + 5, bz - 1, BlockType.Bricks);

        world.SetBlockWorld(bx, surfaceY + 1, bz, BlockType.IronBlock);
        world.SetBlockWorld(bx + 2, surfaceY + 1, bz + 1, BlockType.Water);
        world.SetBlockWorld(bx + 1, surfaceY + 2, bz + 1, BlockType.Torch);

        PlaceChestWithLoot(world, bx + 1, surfaceY + 1, bz,
            (BlockType.IronIngot, 8),
            (BlockType.IronOre, 6),
            (BlockType.Coal, 12),
            (BlockType.GoldOre, 3),
            (BlockType.Obsidian, 4),
            (BlockType.FlintAndSteel, 1),
            (BlockType.Bread, 3));

        var fx = cx - 2;
        var fz = cz + 7;
        for (var dx = -2; dx <= 2; dx++)
        for (var dz = -2; dz <= 2; dz++)
        {
            var x = fx + dx;
            var z = fz + dz;

            var isBorder = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
            var isCenterWater = dx == 0 && dz == 0;

            if (isBorder)
            {
                world.SetBlockWorld(x, surfaceY, z, BlockType.Log);
                world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Empty);
            }
            else if (isCenterWater)
            {
                world.SetBlockWorld(x, surfaceY, z, BlockType.Water);
                world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Empty);
            }
            else
            {
                world.SetBlockWorld(x, surfaceY, z, BlockType.Farmland);
                world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Wheat);
            }
        }

        for (var x = Math.Min(hx, cx); x <= Math.Max(bx, cx); x++)
        {
            world.SetBlockWorld(x, surfaceY, cz, BlockType.Cobblestone);
            world.SetBlockWorld(x, surfaceY + 1, cz, BlockType.Empty);
        }

        for (var z = Math.Min(hz, cz); z <= Math.Max(fz, cz); z++)
        {
            world.SetBlockWorld(cx, surfaceY, z, BlockType.Cobblestone);
            world.SetBlockWorld(cx, surfaceY + 1, z, BlockType.Empty);
        }
    }

    private void GenerateAncientRuins(World world, int cx, int surfaceY, int cz, int seed)
    {
        for (var dx = -3; dx <= 3; dx++)
        for (var dz = -3; dz <= 3; dz++)
        {
            var x = cx + dx;
            var z = cz + dz;

            var blockSeed = HashCoordinates(x, surfaceY, z, seed);
            var floorType = blockSeed % 3 == 0 ? BlockType.MossyCobblestone :
                blockSeed % 3 == 1 ? BlockType.Stone : BlockType.Cobblestone;
            world.SetBlockWorld(x, surfaceY, z, floorType);

            for (var dy = 1; dy <= 5; dy++) world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
        }

        int[] pillarOffsetsX = { -3, 3, -3, 3 };
        int[] pillarOffsetsZ = { -3, -3, 3, 3 };
        int[] pillarHeights = { 2 + seed % 3, 4, 1 + (seed >> 2) % 3, 3 };

        for (var p = 0; p < 4; p++)
        {
            var px = cx + pillarOffsetsX[p];
            var pz = cz + pillarOffsetsZ[p];
            var h = pillarHeights[p];

            for (var dy = 1; dy <= h; dy++)
            {
                var pillarBlock = dy % 2 == 0 ? BlockType.Bricks : BlockType.MossyCobblestone;
                world.SetBlockWorld(px, surfaceY + dy, pz, pillarBlock);
            }

            if (h >= 4)
            {
                world.SetBlockWorld(px, surfaceY + h, pz, BlockType.Cobblestone);
                world.SetBlockWorld(px, surfaceY + h + 1, pz, BlockType.Torch);
            }
        }

        world.SetBlockWorld(cx, surfaceY + 1, cz, BlockType.Obsidian);
        PlaceChestWithLoot(world, cx, surfaceY + 2, cz,
            (BlockType.DiamondOre, 3),
            (BlockType.GoldBlock, 1),
            (BlockType.Obsidian, 6),
            (BlockType.Bookshelf, 3),
            (BlockType.Cake, 1),
            (BlockType.FlintAndSteel, 1),
            (BlockType.Torch, 8));

        world.SetBlockWorld(cx - 1, surfaceY + 2, cz, BlockType.Cobweb);
        world.SetBlockWorld(cx + 1, surfaceY + 2, cz, BlockType.Cobweb);
        world.SetBlockWorld(cx, surfaceY + 1, cz - 2, BlockType.RedstoneTorch);
        world.SetBlockWorld(cx, surfaceY + 1, cz + 2, BlockType.RedstoneTorch);
    }

    private static bool IsLiquid(byte block)
    {
        return block is (byte)BlockType.Water or (byte)BlockType.Lava;
    }

    private static bool IsInvalidCactusNeighbor(byte block)
    {
        return block is (byte)BlockType.Water or (byte)BlockType.Lava || BlockRegistry.IsSolid(block);
    }

    public static int HashCoordinates(int x, int y, int z, int seed)
    {
        unchecked
        {
            var h = seed;
            h = (h * 397) ^ x;
            h = (h * 397) ^ y;
            h = (h * 397) ^ z;
            h ^= h >> 16;
            h *= 0x45d9f3b;
            h ^= h >> 16;
            return Math.Abs(h);
        }
    }
}