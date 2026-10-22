using OpenTK.Mathematics;
using VelmoraCraft.src.Entities;

namespace VelmoraCraft.src.World
{
    public enum BiomeType : byte
    {
        Plains,
        Forest,
        Desert,
        Mountains
    }

    public class WorldGenerator
    {
        private readonly int seed;
        private readonly FastNoiseLite elevationNoise;
        private readonly FastNoiseLite continentalNoise;
        private readonly FastNoiseLite caveNoiseA;
        private readonly FastNoiseLite caveNoiseB;
        private readonly FastNoiseLite temperatureNoise;
        private readonly FastNoiseLite moistureNoise;

        public const int SEA_LEVEL = 36;
        public const int LAVA_LEVEL = 10;

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
            float temp = temperatureNoise.GetNoise(wx, wz);
            float moisture = moistureNoise.GetNoise(wx, wz);

            if (temp > 0.3f && moisture < -0.1f)
            {
                return BiomeType.Desert;
            }
            if (temp < -0.2f)
            {
                return BiomeType.Mountains;
            }
            if (moisture > 0.1f)
            {
                return BiomeType.Forest;
            }

            return BiomeType.Plains;
        }

        public void GenerateChunkTerrain(Chunk chunk)
        {
            int originX = chunk.ChunkCoord.X * Chunk.SIZE_X;
            int originZ = chunk.ChunkCoord.Y * Chunk.SIZE_Z;

            for (int lx = 0; lx < Chunk.SIZE_X; lx++)
            {
                for (int lz = 0; lz < Chunk.SIZE_Z; lz++)
                {
                    int wx = originX + lx;
                    int wz = originZ + lz;

                    BiomeType biome = GetBiome(wx, wz);

                    // Compute surface height
                    float baseElev = elevationNoise.GetNoise(wx, wz); // [-1, 1]
                    float cont = continentalNoise.GetNoise(wx, wz);   // [-1, 1]

                    float targetHeight;
                    switch (biome)
                    {
                        case BiomeType.Mountains:
                            targetHeight = 45f + (cont * 30f) + (baseElev * 20f);
                            break;
                        case BiomeType.Desert:
                            targetHeight = 35f + (baseElev * 8f);
                            break;
                        case BiomeType.Forest:
                            targetHeight = 38f + (baseElev * 14f);
                            break;
                        default: // Plains
                            targetHeight = 36f + (baseElev * 12f);
                            break;
                    }

                    int surfaceHeight = Math.Clamp((int)targetHeight, 15, Chunk.SIZE_Y - 10);

                    // Fill vertical column
                    for (int y = 0; y < Chunk.SIZE_Y; y++)
                    {
                        if (y == 0)
                        {
                            chunk.SetBlock(lx, y, lz, BlockType.Bedrock);
                            continue;
                        }

                        if (y > surfaceHeight)
                        {
                            // Static Water below Sea Level
                            if (y <= SEA_LEVEL)
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Water);
                            }
                            else
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Empty);
                            }
                            continue;
                        }

                        if (y > 3 && y < surfaceHeight - 2)
                        {
                            float cA = caveNoiseA.GetNoise(wx, y * 1.5f, wz);
                            float cB = caveNoiseB.GetNoise(wx, y * 1.5f, wz);

                            if (MathF.Abs(cA) < 0.12f && MathF.Abs(cB) < 0.12f)
                            {
                                // If cave is deep down, fill with static lava
                                if (y <= LAVA_LEVEL)
                                {
                                    chunk.SetBlock(lx, y, lz, BlockType.Lava);
                                }
                                else
                                {
                                    chunk.SetBlock(lx, y, lz, BlockType.Empty);
                                }
                                continue;
                            }
                        }

                        if (y == surfaceHeight)
                        {
                            if (surfaceHeight < SEA_LEVEL + 2 && biome != BiomeType.Mountains)
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Sand);
                            }
                            else if (biome == BiomeType.Desert)
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Sand);
                            }
                            else if (biome == BiomeType.Mountains && surfaceHeight >= 50)
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.SnowyGrass);
                            }
                            else
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Grass);
                            }
                        }
                        else if (y > surfaceHeight - 4)
                        {
                            if (biome == BiomeType.Desert)
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Sandstone);
                            }
                            else if (surfaceHeight < SEA_LEVEL + 2 && biome != BiomeType.Mountains)
                            {
                                // Clay and sand along beaches
                                chunk.SetBlock(lx, y, lz, (HashCoordinates(wx, y, wz, seed) % 5 == 0) ? BlockType.Clay : BlockType.Sand);
                            }
                            else if (biome == BiomeType.Mountains && surfaceHeight >= 50)
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.SnowBlock);
                            }
                            else
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Dirt);
                            }
                        }
                        else
                        {
                            // Deep stone layer with ore veins
                            BlockType rockType = BlockType.Stone;

                            // Deterministic pseudo-random ore distribution
                            int hash = HashCoordinates(wx, y, wz, seed);
                            int oreRoll = hash % 1000;

                            if (y <= 12 && oreRoll < 12)
                            {
                                rockType = BlockType.Obsidian;
                            }
                            else if (y <= 14 && oreRoll < 22)
                            {
                                rockType = BlockType.DiamondOre; // Diamond deep underground
                            }
                            else if (y <= 18 && oreRoll < 42)
                            {
                                rockType = BlockType.RedstoneOre; // Redstone near bedrock
                            }
                            else if (y <= 32 && oreRoll < 24)
                            {
                                rockType = BlockType.GoldOre; // Gold in mid-depths
                            }
                            else if (y >= 10 && y <= 34 && oreRoll >= 42 && oreRoll < 58)
                            {
                                rockType = BlockType.LapisOre; // Lapis lazuli band
                            }
                            else if (y <= 50 && oreRoll < 85)
                            {
                                rockType = BlockType.IronOre; // Iron ore
                            }
                            else if (y <= 75 && oreRoll < 140)
                            {
                                rockType = BlockType.CoalOre; // Coal ore
                            }
                            else if (oreRoll >= 880 && oreRoll < 915)
                            {
                                rockType = BlockType.Gravel; // Gravel pockets
                            }
                            else if (oreRoll >= 915 && oreRoll < 935)
                            {
                                rockType = BlockType.Dirt; // Dirt pockets
                            }
                            else if (biome == BiomeType.Desert && y > surfaceHeight - 8)
                            {
                                rockType = BlockType.Sandstone;
                            }

                            chunk.SetBlock(lx, y, lz, rockType);
                        }
                    }
                }
            }
        }

        public void DecorateChunk(World world, Chunk chunk)
        {
            int originX = chunk.ChunkCoord.X * Chunk.SIZE_X;
            int originZ = chunk.ChunkCoord.Y * Chunk.SIZE_Z;

            for (int lx = 2; lx < Chunk.SIZE_X - 2; lx++)
            {
                for (int lz = 2; lz < Chunk.SIZE_Z - 2; lz++)
                {
                    int wx = originX + lx;
                    int wz = originZ + lz;

                    BiomeType biome = GetBiome(wx, wz);

                    if (biome == BiomeType.Desert)
                    {
                        int cactusHash = HashCoordinates(wx, 55, wz, seed + 111);
                        if (cactusHash % 90 == 0)
                        {
                            int surfaceY = world.GetSurfaceHeight(wx, wz);
                            // Must be on dry ground above sea level
                            if (surfaceY > SEA_LEVEL && world.GetBlockWorld(wx, surfaceY, wz) == (byte)BlockType.Sand)
                            {
                                int cactusHeight = 2 + (cactusHash % 2);
                                bool canPlace = true;

                                // Base sand block cannot be directly touching liquid
                                if (IsLiquid(world.GetBlockWorld(wx + 1, surfaceY, wz)) ||
                                    IsLiquid(world.GetBlockWorld(wx - 1, surfaceY, wz)) ||
                                    IsLiquid(world.GetBlockWorld(wx, surfaceY, wz + 1)) ||
                                    IsLiquid(world.GetBlockWorld(wx, surfaceY, wz - 1)))
                                {
                                    canPlace = false;
                                }

                                if (canPlace)
                                {
                                    for (int cy = 1; cy <= cactusHeight; cy++)
                                    {
                                        int py = surfaceY + cy;
                                        if (py >= Chunk.SIZE_Y || world.GetBlockWorld(wx, py, wz) != (byte)BlockType.Empty)
                                        {
                                            canPlace = false;
                                            break;
                                        }

                                        // Cacti cannot grow in or adjacent to water, lava, or solid blocks
                                        if (IsInvalidCactusNeighbor(world.GetBlockWorld(wx + 1, py, wz)) ||
                                            IsInvalidCactusNeighbor(world.GetBlockWorld(wx - 1, py, wz)) ||
                                            IsInvalidCactusNeighbor(world.GetBlockWorld(wx, py, wz + 1)) ||
                                            IsInvalidCactusNeighbor(world.GetBlockWorld(wx, py, wz - 1)))
                                        {
                                            canPlace = false;
                                            break;
                                        }
                                    }
                                }

                                if (canPlace)
                                {
                                    for (int cy = 1; cy <= cactusHeight; cy++)
                                    {
                                        world.SetBlockWorld(wx, surfaceY + cy, wz, BlockType.Cactus);
                                    }
                                }
                            }
                        }
                        continue;
                    }

                    int treeChance = (biome == BiomeType.Forest) ? 35 : 160;
                    int treeHash = HashCoordinates(wx, 77, wz, seed + 888);

                    if (treeHash % treeChance == 0)
                    {
                        for (int y = Chunk.SIZE_Y - 10; y >= SEA_LEVEL; y--)
                        {
                            byte block = chunk.GetBlock(lx, y, lz);
                            if (block == (byte)BlockType.Grass || block == (byte)BlockType.SnowyGrass)
                            {
                                if (biome == BiomeType.Mountains)
                                {
                                    GrowSpruceTree(world, wx, y + 1, wz);
                                }
                                else if (biome == BiomeType.Forest && (treeHash % 3 == 0))
                                {
                                    GrowBirchTree(world, wx, y + 1, wz);
                                }
                                else
                                {
                                    GrowTree(world, wx, y + 1, wz);
                                }
                                break;
                            }
                        }
                        continue;
                    }

                    int floraHash = HashCoordinates(wx, 99, wz, seed + 999);
                    int surface = world.GetSurfaceHeight(wx, wz);
                    byte groundBlock = world.GetBlockWorld(wx, surface, wz);

                    if (groundBlock == (byte)BlockType.Grass && world.GetBlockWorld(wx, surface + 1, wz) == (byte)BlockType.Empty)
                    {
                        if (floraHash % 7 == 0)
                        {
                            // Tall grass patches across meadows and hills
                            world.SetBlockWorld(wx, surface + 1, wz, BlockType.TallGrass);
                        }
                        else if (floraHash % 45 == 0)
                        {
                            // Red Rose or Yellow Dandelion
                            BlockType flower = (floraHash % 2 == 0) ? BlockType.Rose : BlockType.Dandelion;
                            world.SetBlockWorld(wx, surface + 1, wz, flower);
                        }
                        else if (floraHash % 48 == 0)
                        {
                            // Red Mushroom (12,1) or Brown Mushroom (13,1) randomly spawned in the world similar to flowers
                            BlockType mushroom = (floraHash % 2 == 0) ? BlockType.RedMushroom : BlockType.BrownMushroom;
                            world.SetBlockWorld(wx, surface + 1, wz, mushroom);
                        }
                        else if (floraHash % 350 == 0)
                        {
                            // Pumpkin patch
                            world.SetBlockWorld(wx, surface + 1, wz, BlockType.Pumpkin);
                        }
                    }
                    else if (groundBlock == (byte)BlockType.Dirt && world.GetBlockWorld(wx, surface + 1, wz) == (byte)BlockType.Empty && floraHash % 30 == 0)
                    {
                        // Mushrooms thriving on forest dirt floor and shaded terrain
                        BlockType mushroom = (floraHash % 2 == 0) ? BlockType.RedMushroom : BlockType.BrownMushroom;
                        world.SetBlockWorld(wx, surface + 1, wz, mushroom);
                    }
                    else if ((groundBlock == (byte)BlockType.Sand || groundBlock == (byte)BlockType.Dirt) && surface <= SEA_LEVEL + 1)
                    {
                        // Sugar cane along water shores
                        if (floraHash % 70 == 0 && world.GetBlockWorld(wx, surface + 1, wz) == (byte)BlockType.Empty)
                        {
                            int caneHeight = 2 + (floraHash % 2);
                            for (int cy = 1; cy <= caneHeight; cy++)
                            {
                                world.SetBlockWorld(wx, surface + cy, wz, BlockType.SugarCane);
                            }
                        }
                    }

                    int dungeonHash = HashCoordinates(wx, 33, wz, seed + 777);
                    if (dungeonHash % 900 == 0)
                    {
                        int dungeonY = 12 + (dungeonHash % 12); // Deep caves
                        if (world.GetBlockWorld(wx, dungeonY, wz) == (byte)BlockType.Stone)
                        {
                            GenerateDungeonRoom(world, wx, dungeonY, wz);
                        }
                    }
                }
            }

            DecorateChunkStructures(world, chunk);
        }

        private void GenerateDungeonRoom(World world, int cx, int cy, int cz)
        {
            // 5x5 cobblestone & mossy cobblestone dungeon with center monster spawner
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    for (int dy = 0; dy <= 3; dy++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        int z = cz + dz;

                        bool isFloor = (dy == 0);
                        bool isWall = (Math.Abs(dx) == 2 || Math.Abs(dz) == 2);
                        bool isCeiling = (dy == 3);

                        if (isFloor || isWall || isCeiling)
                        {
                            BlockType cobble = ((dx + dz + dy) % 3 == 0) ? BlockType.MossyCobblestone : BlockType.Cobblestone;
                            world.SetBlockWorld(x, y, z, cobble);
                        }
                        else
                        {
                            world.SetBlockWorld(x, y, z, BlockType.Empty);
                        }
                    }
                }
            }

            // Monster spawner in center
            world.SetBlockWorld(cx, cy + 1, cz, BlockType.MonsterSpawner);

            // Natural cobwebs in dungeon corners
            world.SetBlockWorld(cx - 1, cy + 2, cz - 1, BlockType.Cobweb);
            world.SetBlockWorld(cx + 1, cy + 2, cz + 1, BlockType.Cobweb);
            world.SetBlockWorld(cx - 1, cy + 2, cz + 1, BlockType.Cobweb);
            world.SetBlockWorld(cx + 1, cy + 2, cz - 1, BlockType.Cobweb);
        }

        public void GrowBirchTree(World world, int wx, int trunkBaseY, int wz)
        {
            int trunkHeight = 6;
            for (int y = 0; y < trunkHeight; y++)
            {
                world.SetBlockWorld(wx, trunkBaseY + y, wz, BlockType.BirchLog);
            }

            int leafBase = trunkBaseY + trunkHeight - 2;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    for (int dy = 0; dy <= 2; dy++)
                    {
                        if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2 && dy == 2) continue;

                        int leafX = wx + dx;
                        int leafY = leafBase + dy;
                        int leafZ = wz + dz;

                        if (world.GetBlockWorld(leafX, leafY, leafZ) == (byte)BlockType.Empty)
                        {
                            world.SetBlockWorld(leafX, leafY, leafZ, BlockType.Leaves);
                        }
                    }
                }
            }
            world.SetBlockWorld(wx, trunkBaseY + trunkHeight + 1, wz, BlockType.Leaves);
        }

        public void GrowSpruceTree(World world, int wx, int trunkBaseY, int wz)
        {
            int trunkHeight = 7;
            for (int y = 0; y < trunkHeight; y++)
            {
                world.SetBlockWorld(wx, trunkBaseY + y, wz, BlockType.SpruceLog);
            }

            // Conical pine canopy
            for (int y = trunkHeight - 4; y <= trunkHeight; y++)
            {
                int radius = (y >= trunkHeight - 1) ? 1 : 2;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Math.Abs(dx) == radius && Math.Abs(dz) == radius && y == trunkHeight - 4) continue;

                        int leafX = wx + dx;
                        int leafY = trunkBaseY + y;
                        int leafZ = wz + dz;

                        if (world.GetBlockWorld(leafX, leafY, leafZ) == (byte)BlockType.Empty)
                        {
                            world.SetBlockWorld(leafX, leafY, leafZ, BlockType.Leaves);
                        }
                    }
                }
            }
            world.SetBlockWorld(wx, trunkBaseY + trunkHeight + 1, wz, BlockType.Leaves);
        }

        public void GrowTree(World world, int wx, int trunkBaseY, int wz)
        {
            int trunkHeight = 5;

            // Wood trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                world.SetBlockWorld(wx, trunkBaseY + y, wz, BlockType.Log);
            }

            // Leaves canopy (spherical/cuboid canopy)
            int leafBase = trunkBaseY + trunkHeight - 2;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    for (int dy = 0; dy <= 2; dy++)
                    {
                        if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2 && dy == 2) continue; // Round the corners

                        int leafX = wx + dx;
                        int leafY = leafBase + dy;
                        int leafZ = wz + dz;

                        // Only place leaves in empty air
                        if (world.GetBlockWorld(leafX, leafY, leafZ) == (byte)BlockType.Empty)
                        {
                            world.SetBlockWorld(leafX, leafY, leafZ, BlockType.Leaves);
                        }
                    }
                }
            }

            // Top leaf crown
            world.SetBlockWorld(wx, trunkBaseY + trunkHeight + 1, wz, BlockType.Leaves);

            // ~15% chance to generate an atmospheric cobweb under an outer leaf
            if (Random.Shared.Next(100) < 15)
            {
                int cwx = wx + (Random.Shared.Next(2) == 0 ? -1 : 1);
                int cwz = wz + (Random.Shared.Next(2) == 0 ? -1 : 1);
                int cwy = leafBase - 1;
                if (cwy > 0 && world.GetBlockWorld(cwx, cwy, cwz) == (byte)BlockType.Empty)
                {
                    world.SetBlockWorld(cwx, cwy, cwz, BlockType.Cobweb);
                }
            }
        }

        private static int FloorDiv(int a, int b)
        {
            int rem = a % b;
            return (a < 0 && rem != 0) ? (a / b - 1) : (a / b);
        }

        private bool IsRegionCandidateChunk(int chunkX, int chunkZ, int regionSize, int saltX, int saltZ, int saltSeed)
        {
            int rx = FloorDiv(chunkX, regionSize);
            int rz = FloorDiv(chunkZ, regionSize);
            int candX = rx * regionSize + (HashCoordinates(rx, saltX, rz, seed + saltSeed) % regionSize);
            int candZ = rz * regionSize + (HashCoordinates(rx, saltZ, rz, seed + saltSeed + 99) % regionSize);
            return chunkX == candX && chunkZ == candZ;
        }

        private void DecorateChunkStructures(World world, Chunk chunk)
        {
            int chunkX = chunk.ChunkCoord.X;
            int chunkZ = chunk.ChunkCoord.Y;

            int originX = chunkX * Chunk.SIZE_X;
            int originZ = chunkZ * Chunk.SIZE_Z;
            int cx = originX + 8;
            int cz = originZ + 8;

            if (!World.IsInsideWorld(cx, cz)) return;

            int chunkHash = HashCoordinates(chunkX, 42, chunkZ, seed + 12345);
            BiomeType centerBiome = GetBiome(cx, cz);
            int surfaceY = world.GetSurfaceHeight(cx, cz);

            if (IsRegionCandidateChunk(chunkX, chunkZ, 13, 31, 32, 3003) &&
                (centerBiome == BiomeType.Plains || centerBiome == BiomeType.Forest) &&
                surfaceY > SEA_LEVEL + 1)
            {
                GenerateVillage(world, cx, surfaceY, cz, chunkHash);
            }
            else if (IsRegionCandidateChunk(chunkX, chunkZ, 11, 41, 42, 4004) &&
                     centerBiome != BiomeType.Desert &&
                     surfaceY > SEA_LEVEL + 2)
            {
                GenerateSurfaceRailwayAndMineshaft(world, cx, surfaceY, cz, chunkHash);
            }
            else if (IsRegionCandidateChunk(chunkX, chunkZ, 10, 51, 52, 5005) &&
                     surfaceY > SEA_LEVEL)
            {
                GenerateAncientRuins(world, cx, surfaceY, cz, chunkHash);
            }
            else if (IsRegionCandidateChunk(chunkX, chunkZ, 8, 11, 12, 1001) &&
                     centerBiome == BiomeType.Desert &&
                     surfaceY > SEA_LEVEL)
            {
                GenerateDesertWell(world, cx, surfaceY, cz);
            }
            else if (IsRegionCandidateChunk(chunkX, chunkZ, 8, 21, 22, 2002) &&
                     centerBiome == BiomeType.Mountains &&
                     surfaceY >= 50)
            {
                GenerateMountainCamp(world, cx, surfaceY, cz, chunkHash);
            }

            if (IsRegionCandidateChunk(chunkX, chunkZ, 8, 61, 62, 6006))
            {
                int undergroundHash = HashCoordinates(chunkX, 91, chunkZ, seed + 67890);
                int cy = 18 + (undergroundHash % 14); // Y between 18 and 31
                GenerateUndergroundMineshaft(world, cx, cy, cz, undergroundHash);
            }
        }

        private static void PlaceChestWithLoot(World world, int x, int y, int z, params (BlockType item, int count)[] items)
        {
            world.SetBlockWorld(x, y, z, BlockType.Chest);
            if (world.ChestManager != null)
            {
                var inv = world.ChestManager.GetOrCreateChest(new Vector3i(x, y, z));
                foreach (var (item, count) in items)
                {
                    inv.AddItem(item, count);
                }
            }
        }

        private void GenerateDesertWell(World world, int cx, int surfaceY, int cz)
        {
            // 5x5 sandstone and bricks desert well
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int x = cx + dx;
                    int z = cz + dz;

                    // Foundation layer beneath well
                    world.SetBlockWorld(x, surfaceY - 2, z, BlockType.Sandstone);

                    bool isBorder = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                    bool isCorner = Math.Abs(dx) == 2 && Math.Abs(dz) == 2;

                    if (isBorder)
                    {
                        // Rim curbs
                        world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Sandstone);
                        world.SetBlockWorld(x, surfaceY, z, BlockType.Sandstone);

                        if (isCorner)
                        {
                            // 4 Brick pillars rising 3 blocks high
                            for (int py = 1; py <= 3; py++)
                            {
                                world.SetBlockWorld(x, surfaceY + py, z, BlockType.Bricks);
                            }
                        }
                        else
                        {
                            // Curb lip
                            world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Sandstone);
                        }
                    }
                    else
                    {
                        // Basin filled with fresh water!
                        world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Water);
                        world.SetBlockWorld(x, surfaceY, z, BlockType.Water);
                        world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Empty);
                        world.SetBlockWorld(x, surfaceY + 2, z, BlockType.Empty);
                        world.SetBlockWorld(x, surfaceY + 3, z, BlockType.Empty);
                    }

                    // Sandstone & Brick canopy roof at surfaceY + 4
                    BlockType roofBlock = isBorder ? BlockType.Bricks : BlockType.Sandstone;
                    world.SetBlockWorld(x, surfaceY + 4, z, roofBlock);
                }
            }

            // Peak center and torch illumination
            world.SetBlockWorld(cx, surfaceY + 5, cz, BlockType.Sandstone);
            world.SetBlockWorld(cx, surfaceY + 3, cz, BlockType.Torch);

            // Well traveler stash chest hidden beside rim
            PlaceChestWithLoot(world, cx + 2, surfaceY + 1, cz + 1,
                (BlockType.Bread, 3),
                (BlockType.GoldOre, 4),
                (BlockType.Sandstone, 8),
                (BlockType.Torch, 4));
        }

        private void GenerateMountainCamp(World world, int cx, int surfaceY, int cz, int seed)
        {
            // Leveled 7x7 mountain campsite
            BlockType tentWool = (seed % 3 == 0) ? BlockType.RedWool : ((seed % 3 == 1) ? BlockType.BlueWool : BlockType.WhiteWool);

            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    int x = cx + dx;
                    int z = cz + dz;
                    world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);
                    for (int dy = 1; dy <= 4; dy++)
                    {
                        world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                    }
                }
            }

            for (int dz = -2; dz <= 2; dz++)
            {
                int z = cz + dz;
                // Wooden floor inside tent
                for (int dx = -1; dx <= 1; dx++)
                {
                    world.SetBlockWorld(cx + dx, surfaceY, z, BlockType.Planks);
                }

                // Tent walls and ridge
                world.SetBlockWorld(cx - 2, surfaceY + 1, z, tentWool);
                world.SetBlockWorld(cx + 2, surfaceY + 1, z, tentWool);

                world.SetBlockWorld(cx - 1, surfaceY + 2, z, tentWool);
                world.SetBlockWorld(cx + 1, surfaceY + 2, z, tentWool);

                world.SetBlockWorld(cx, surfaceY + 3, z, tentWool);
            }

            // Back wall of tent (dz = -2)
            world.SetBlockWorld(cx - 1, surfaceY + 1, cz - 2, tentWool);
            world.SetBlockWorld(cx, surfaceY + 1, cz - 2, tentWool);
            world.SetBlockWorld(cx + 1, surfaceY + 1, cz - 2, tentWool);
            world.SetBlockWorld(cx, surfaceY + 2, cz - 2, tentWool);

            // Inside tent: Explorer supply chest & torch
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

            // Campfire ring
            world.SetBlockWorld(cx + 1, surfaceY + 1, cz + 2, BlockType.Cobblestone);
            world.SetBlockWorld(cx + 3, surfaceY + 1, cz + 2, BlockType.Cobblestone);
            world.SetBlockWorld(cx + 2, surfaceY + 1, cz + 3, BlockType.Cobblestone);
            world.SetBlockWorld(cx + 2, surfaceY + 1, cz + 1, BlockType.Cobblestone);
            world.SetBlockWorld(cx + 2, surfaceY + 1, cz + 2, BlockType.Torch); // central campfire flame

            // Perimeter post torches for mountain night visibility
            world.SetBlockWorld(cx - 3, surfaceY + 1, cz + 3, BlockType.Cobblestone);
            world.SetBlockWorld(cx - 3, surfaceY + 2, cz + 3, BlockType.Torch);

            world.SetBlockWorld(cx + 3, surfaceY + 1, cz - 3, BlockType.Cobblestone);
            world.SetBlockWorld(cx + 3, surfaceY + 2, cz - 3, BlockType.Torch);
        }

        private void GenerateUndergroundMineshaft(World world, int cx, int cy, int cz, int seed)
        {
            // Small underground mineshaft tunnel: 14 blocks long, 3 wide, 3 high
            bool alongX = (seed % 2 == 0);
            int length = 14;

            for (int step = 0; step < length; step++)
            {
                int sx = alongX ? cx + step : cx;
                int sz = alongX ? cz : cz + step;

                for (int w = -1; w <= 1; w++)
                {
                    int px = alongX ? sx : sx + w;
                    int pz = alongX ? sz + w : sz;

                    // Solid floor
                    byte floorBlock = world.GetBlockWorld(px, cy - 1, pz);
                    if (floorBlock == (byte)BlockType.Empty || floorBlock == (byte)BlockType.Water || floorBlock == (byte)BlockType.Lava)
                    {
                        world.SetBlockWorld(px, cy - 1, pz, BlockType.Planks);
                    }

                    // Clear 3-high tunnel corridor
                    for (int dy = 0; dy < 3; dy++)
                    {
                        world.SetBlockWorld(px, cy + dy, pz, BlockType.Empty);
                    }

                    // Ceiling support
                    byte ceilBlock = world.GetBlockWorld(px, cy + 3, pz);
                    if (ceilBlock == (byte)BlockType.Empty)
                    {
                        world.SetBlockWorld(px, cy + 3, pz, BlockType.Planks);
                    }
                }

                // Central track on floor
                world.SetBlockWorld(sx, cy, sz, BlockType.Rail);

                // Timber arch support every 4 blocks
                if (step % 4 == 0)
                {
                    int leftX = alongX ? sx : sx - 1;
                    int leftZ = alongX ? sz - 1 : sz;
                    int rightX = alongX ? sx : sx + 1;
                    int rightZ = alongX ? sz + 1 : sz;

                    // Wooden support posts
                    world.SetBlockWorld(leftX, cy, leftZ, BlockType.Log);
                    world.SetBlockWorld(leftX, cy + 1, leftZ, BlockType.Log);
                    world.SetBlockWorld(leftX, cy + 2, leftZ, BlockType.Log);

                    world.SetBlockWorld(rightX, cy, rightZ, BlockType.Log);
                    world.SetBlockWorld(rightX, cy + 1, rightZ, BlockType.Log);
                    world.SetBlockWorld(rightX, cy + 2, rightZ, BlockType.Log);

                    // Crossbeam overhead
                    world.SetBlockWorld(leftX, cy + 2, leftZ, BlockType.Planks);
                    world.SetBlockWorld(sx, cy + 2, sz, BlockType.Planks);
                    world.SetBlockWorld(rightX, cy + 2, rightZ, BlockType.Planks);

                    // Torch on support post
                    world.SetBlockWorld(leftX, cy + 1, leftZ, BlockType.Torch);
                }
                else
                {
                    // Atmospheric cobwebs hanging from upper corners
                    if ((step + seed) % 3 == 0)
                    {
                        int cobX = alongX ? sx : sx + ((step % 2 == 0) ? -1 : 1);
                        int cobZ = alongX ? sz + ((step % 2 == 0) ? -1 : 1) : sz;
                        world.SetBlockWorld(cobX, cy + 2, cobZ, BlockType.Cobweb);
                    }
                }
            }

            // Abandoned miner's loot chest at end of tunnel
            int chestX = alongX ? cx + length - 2 : cx + 1;
            int chestZ = alongX ? cz + 1 : cz + length - 2;
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
            // Bigger surface mineshaft headframe, depot station & 24-block scenic railway system
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int x = cx + dx;
                    int z = cz + dz;

                    // Foundation & deck
                    world.SetBlockWorld(x, surfaceY, z, BlockType.Planks);
                    for (int dy = 1; dy <= 4; dy++)
                    {
                        world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                    }
                }
            }

            // Wooden Headframe / Station canopy pillars (4 corners)
            for (int dy = 1; dy <= 3; dy++)
            {
                world.SetBlockWorld(cx - 2, surfaceY + dy, cz - 2, BlockType.Log);
                world.SetBlockWorld(cx + 2, surfaceY + dy, cz - 2, BlockType.Log);
                world.SetBlockWorld(cx - 2, surfaceY + dy, cz + 2, BlockType.Log);
                world.SetBlockWorld(cx + 2, surfaceY + dy, cz + 2, BlockType.Log);
            }

            // Canopy roof of Planks
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    world.SetBlockWorld(cx + dx, surfaceY + 4, cz + dz, BlockType.Planks);
                }
            }

            // Workstation utilities at depot
            world.SetBlockWorld(cx - 1, surfaceY + 1, cz - 2, BlockType.CraftingTable);
            world.SetBlockWorld(cx + 1, surfaceY + 1, cz - 2, BlockType.Furnace);

            // Torches lighting the depot
            world.SetBlockWorld(cx - 2, surfaceY + 2, cz - 1, BlockType.Torch);
            world.SetBlockWorld(cx + 2, surfaceY + 2, cz - 1, BlockType.Torch);

            // Cargo depot chest loaded with railway gear
            PlaceChestWithLoot(world, cx - 1, surfaceY + 1, cz + 2,
                (BlockType.MinecartItem, 2),
                (BlockType.Rail, 16),
                (BlockType.PoweredRail, 6),
                (BlockType.Coal, 10),
                (BlockType.IronIngot, 6),
                (BlockType.Torch, 8),
                (BlockType.Bread, 3));

            int trackLength = 24;
            int trackZ = cz;
            int trackY = surfaceY + 1;

            for (int i = 0; i < trackLength; i++)
            {
                int tx = cx + i;

                // Ensure ballast bed underneath track (planks / gravel bridge trestle)
                for (int by = trackY - 1; by >= SEA_LEVEL - 5; by--)
                {
                    byte b = world.GetBlockWorld(tx, by, trackZ);
                    if (b != (byte)BlockType.Empty && b != (byte)BlockType.Water) break;
                    // Build wooden bridge trestle downward if terrain drops
                    world.SetBlockWorld(tx, by, trackZ, (by == trackY - 1) ? BlockType.Gravel : BlockType.Planks);
                }

                // Clear air space above track
                world.SetBlockWorld(tx, trackY, trackZ, BlockType.Empty);
                world.SetBlockWorld(tx, trackY + 1, trackZ, BlockType.Empty);

                // Powered rail booster every 8 blocks
                if (i > 0 && i % 8 == 0)
                {
                    world.SetBlockWorld(tx, trackY, trackZ, BlockType.PoweredRailOn);
                    // Redstone torch powering booster beside the rail
                    world.SetBlockWorld(tx, trackY, trackZ + 1, BlockType.RedstoneTorch);
                }
                else
                {
                    world.SetBlockWorld(tx, trackY, trackZ, BlockType.Rail);
                }
            }

            // Buffer stop at end of line
            world.SetBlockWorld(cx + trackLength, trackY, trackZ, BlockType.Cobblestone);
            world.SetBlockWorld(cx + trackLength, trackY + 1, trackZ, BlockType.Torch);

            world.SpawnMinecart(new Minecart(new Vector3(cx + 1.5f, trackY + 0.1f, trackZ + 0.5f)));
        }

        private void GenerateVillage(World world, int cx, int surfaceY, int cz, int seed)
        {
            // Small village built with Bricks, Cobblestone, and Stone
            // Layout:

            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int x = cx + dx;
                    int z = cz + dz;
                    bool isBorder = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                    bool isCorner = Math.Abs(dx) == 2 && Math.Abs(dz) == 2;

                    world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Cobblestone);

                    if (isBorder)
                    {
                        world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);
                        world.SetBlockWorld(x, surfaceY + 1, z, BlockType.Cobblestone);

                        if (isCorner)
                        {
                            for (int py = 1; py <= 3; py++)
                            {
                                world.SetBlockWorld(x, surfaceY + py, z, BlockType.Bricks);
                            }
                        }
                    }
                    else
                    {
                        // Deep well water pool
                        world.SetBlockWorld(x, surfaceY - 2, z, BlockType.Cobblestone);
                        world.SetBlockWorld(x, surfaceY - 1, z, BlockType.Water);
                        world.SetBlockWorld(x, surfaceY, z, BlockType.Water);
                        for (int dy = 1; dy <= 3; dy++)
                        {
                            world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                        }
                    }

                    // Canopy roof (Bricks & Stone)
                    world.SetBlockWorld(x, surfaceY + 4, z, isBorder ? BlockType.Cobblestone : BlockType.Bricks);
                }
            }
            // Well torches
            world.SetBlockWorld(cx, surfaceY + 3, cz, BlockType.Torch);

            int hx = cx - 8;
            int hz = cz - 2;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 3; dz++)
                {
                    int x = hx + dx;
                    int z = hz + dz;

                    // Cobblestone foundation
                    world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);

                    bool isWall = Math.Abs(dx) == 2 || dz == -2 || dz == 3;

                    for (int dy = 1; dy <= 3; dy++)
                    {
                        if (isWall)
                        {
                            bool isWindow = dy == 2 && ((Math.Abs(dx) == 2 && dz == 0) || (dz == -2 && dx == 0));
                            bool isDoorway = (dz == 3 && dx == 0 && (dy == 1 || dy == 2));

                            if (isWindow)
                            {
                                world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Glass);
                            }
                            else if (isDoorway)
                            {
                                world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                            }
                            else
                            {
                                world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Bricks);
                            }
                        }
                        else
                        {
                            // Floor inside is wood planks
                            world.SetBlockWorld(x, surfaceY, z, BlockType.Planks);
                            world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                        }
                    }

                    // Roof: SmoothStone
                    world.SetBlockWorld(x, surfaceY + 4, z, BlockType.SmoothStone);
                }
            }

            // Wooden door for Cottage sitting on solid foundation
            world.SetBlockWorld(hx, surfaceY + 1, hz + 3, BlockType.DoorLower, 0);
            world.SetBlockWorld(hx, surfaceY + 2, hz + 3, BlockType.DoorUpper, 0);

            // Cottage Interior Furniture
            world.SetBlockWorld(hx - 1, surfaceY + 1, hz - 1, BlockType.Bookshelf);
            world.SetBlockWorld(hx + 1, surfaceY + 1, hz - 1, BlockType.CraftingTable);
            world.SetBlockWorld(hx, surfaceY + 2, hz - 1, BlockType.Torch);

            // Cottage chest with villager food & supplies
            PlaceChestWithLoot(world, hx - 1, surfaceY + 1, hz + 1,
                (BlockType.Bread, 5),
                (BlockType.Cake, 1),
                (BlockType.Wheat, 8),
                (BlockType.Torch, 6),
                (BlockType.Bookshelf, 2));

            int bx = cx + 8;
            int bz = cz - 2;
            for (int dx = -2; dx <= 3; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int x = bx + dx;
                    int z = bz + dz;

                    world.SetBlockWorld(x, surfaceY, z, BlockType.Cobblestone);

                    bool isBackOrSideWall = dz == -2 || dx == 3 || dz == 2;
                    bool isOpenFront = dx == -2;

                    for (int dy = 1; dy <= 3; dy++)
                    {
                        if (isOpenFront)
                        {
                            bool isPillar = (dz == -2 || dz == 2);
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
                    }

                    // Roof of Stone
                    world.SetBlockWorld(x, surfaceY + 4, z, BlockType.Stone);
                }
            }

            // Blacksmith Forge Equipment
            world.SetBlockWorld(bx + 1, surfaceY + 1, bz - 1, BlockType.LitFurnace);
            world.SetBlockWorld(bx + 2, surfaceY + 1, bz - 1, BlockType.Furnace);
            // Brick chimney rising through roof
            world.SetBlockWorld(bx + 1, surfaceY + 2, bz - 1, BlockType.Bricks);
            world.SetBlockWorld(bx + 1, surfaceY + 3, bz - 1, BlockType.Bricks);
            world.SetBlockWorld(bx + 1, surfaceY + 4, bz - 1, BlockType.Bricks);
            world.SetBlockWorld(bx + 1, surfaceY + 5, bz - 1, BlockType.Bricks);

            // Anvil (IronBlock) and cooling water trough
            world.SetBlockWorld(bx, surfaceY + 1, bz, BlockType.IronBlock);
            world.SetBlockWorld(bx + 2, surfaceY + 1, bz + 1, BlockType.Water);
            world.SetBlockWorld(bx + 1, surfaceY + 2, bz + 1, BlockType.Torch);

            // Blacksmith Treasure Chest
            PlaceChestWithLoot(world, bx + 1, surfaceY + 1, bz,
                (BlockType.IronIngot, 8),
                (BlockType.IronOre, 6),
                (BlockType.Coal, 12),
                (BlockType.GoldOre, 3),
                (BlockType.Obsidian, 4),
                (BlockType.FlintAndSteel, 1),
                (BlockType.Bread, 3));

            int fx = cx - 2;
            int fz = cz + 7;
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int x = fx + dx;
                    int z = fz + dz;

                    bool isBorder = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                    bool isCenterWater = (dx == 0 && dz == 0);

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
            }

            for (int x = Math.Min(hx, cx); x <= Math.Max(bx, cx); x++)
            {
                world.SetBlockWorld(x, surfaceY, cz, BlockType.Cobblestone);
                world.SetBlockWorld(x, surfaceY + 1, cz, BlockType.Empty);
            }
            for (int z = Math.Min(hz, cz); z <= Math.Max(fz, cz); z++)
            {
                world.SetBlockWorld(cx, surfaceY, z, BlockType.Cobblestone);
                world.SetBlockWorld(cx, surfaceY + 1, z, BlockType.Empty);
            }
        }

        private void GenerateAncientRuins(World world, int cx, int surfaceY, int cz, int seed)
        {
            // Sunken stone ruins with weathered pillars, cobwebs and ancient reliquary
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    int x = cx + dx;
                    int z = cz + dz;

                    // Cracked weathered floor (mix of Stone, Cobblestone, MossyCobblestone)
                    int blockSeed = HashCoordinates(x, surfaceY, z, seed);
                    BlockType floorType = (blockSeed % 3 == 0) ? BlockType.MossyCobblestone : ((blockSeed % 3 == 1) ? BlockType.Stone : BlockType.Cobblestone);
                    world.SetBlockWorld(x, surfaceY, z, floorType);

                    for (int dy = 1; dy <= 5; dy++)
                    {
                        world.SetBlockWorld(x, surfaceY + dy, z, BlockType.Empty);
                    }
                }
            }

            // Weathered Pillars at 4 corners of various heights
            int[] pillarOffsetsX = { -3, 3, -3, 3 };
            int[] pillarOffsetsZ = { -3, -3, 3, 3 };
            int[] pillarHeights = { 2 + (seed % 3), 4, 1 + ((seed >> 2) % 3), 3 };

            for (int p = 0; p < 4; p++)
            {
                int px = cx + pillarOffsetsX[p];
                int pz = cz + pillarOffsetsZ[p];
                int h = pillarHeights[p];

                for (int dy = 1; dy <= h; dy++)
                {
                    BlockType pillarBlock = (dy % 2 == 0) ? BlockType.Bricks : BlockType.MossyCobblestone;
                    world.SetBlockWorld(px, surfaceY + dy, pz, pillarBlock);
                }

                if (h >= 4)
                {
                    world.SetBlockWorld(px, surfaceY + h, pz, BlockType.Cobblestone);
                    world.SetBlockWorld(px, surfaceY + h + 1, pz, BlockType.Torch);
                }
            }

            // Center Altar Pedestal with Ancient Reliquary Chest
            world.SetBlockWorld(cx, surfaceY + 1, cz, BlockType.Obsidian);
            PlaceChestWithLoot(world, cx, surfaceY + 2, cz,
                (BlockType.DiamondOre, 3),
                (BlockType.GoldBlock, 1),
                (BlockType.Obsidian, 6),
                (BlockType.Bookshelf, 3),
                (BlockType.Cake, 1),
                (BlockType.FlintAndSteel, 1),
                (BlockType.Torch, 8));

            // Cobwebs and Redstone Torches for ancient ruin atmosphere
            world.SetBlockWorld(cx - 1, surfaceY + 2, cz, BlockType.Cobweb);
            world.SetBlockWorld(cx + 1, surfaceY + 2, cz, BlockType.Cobweb);
            world.SetBlockWorld(cx, surfaceY + 1, cz - 2, BlockType.RedstoneTorch);
            world.SetBlockWorld(cx, surfaceY + 1, cz + 2, BlockType.RedstoneTorch);
        }

        private static bool IsLiquid(byte block)
        {
            return block == (byte)BlockType.Water || block == (byte)BlockType.Lava;
        }

        private static bool IsInvalidCactusNeighbor(byte block)
        {
            return block == (byte)BlockType.Water || block == (byte)BlockType.Lava || BlockRegistry.IsSolid(block);
        }

        public static int HashCoordinates(int x, int y, int z, int seed)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ x;
                h = (h * 397) ^ y;
                h = (h * 397) ^ z;
                h ^= (h >> 16);
                h *= 0x45d9f3b;
                h ^= (h >> 16);
                return Math.Abs(h);
            }
        }
    }
}
