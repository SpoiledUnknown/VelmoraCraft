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

            // 1. Base Elevation Noise (rolling terrain)
            elevationNoise = new FastNoiseLite(seed);
            elevationNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            elevationNoise.SetFrequency(0.012f);
            elevationNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
            elevationNoise.SetFractalOctaves(3);

            // 2. Continental / Mountain Noise (large elevation variations)
            continentalNoise = new FastNoiseLite(seed + 101);
            continentalNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            continentalNoise.SetFrequency(0.005f);

            // 3. 3D Cave Noise (Dual-noise worm / Swiss-cheese tunnels)
            caveNoiseA = new FastNoiseLite(seed + 202);
            caveNoiseA.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            caveNoiseA.SetFrequency(0.035f);

            caveNoiseB = new FastNoiseLite(seed + 303);
            caveNoiseB.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            caveNoiseB.SetFrequency(0.035f);

            // 4. Biome parameter noise
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
                        // 1. Indestructible Bedrock floor
                        if (y == 0)
                        {
                            chunk.SetBlock(lx, y, lz, BlockType.Bedrock);
                            continue;
                        }

                        // 2. Below bedrock or above surface
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

                        // 3. 3D Cave carving (carve air where both 3D noises overlap within narrow band)
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

                        // 4. Ground layer blocks
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

                    // 1. Desert Decoration (Cactus & Sugar Cane)
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

                    // 2. Tree Generation
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

                    // 3. Surface Flora & Crops (Flowers, Mushrooms, Pumpkins, Sugar Cane)
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
                        else if (floraHash % 120 == 0)
                        {
                            // Mushrooms in shade
                            BlockType mushroom = (floraHash % 2 == 0) ? BlockType.RedMushroom : BlockType.BrownMushroom;
                            world.SetBlockWorld(wx, surface + 1, wz, mushroom);
                        }
                        else if (floraHash % 350 == 0)
                        {
                            // Pumpkin patch
                            world.SetBlockWorld(wx, surface + 1, wz, BlockType.Pumpkin);
                        }
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

                    // 4. Rare Underground Dungeon Spawner Room
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
        }

        private void GrowBirchTree(World world, int wx, int trunkBaseY, int wz)
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

        private void GrowSpruceTree(World world, int wx, int trunkBaseY, int wz)
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

        private void GrowTree(World world, int wx, int trunkBaseY, int wz)
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
        }

        private static bool IsLiquid(byte block)
        {
            return block == (byte)BlockType.Water || block == (byte)BlockType.Lava;
        }

        private static bool IsInvalidCactusNeighbor(byte block)
        {
            return block == (byte)BlockType.Water || block == (byte)BlockType.Lava || BlockRegistry.IsSolid(block);
        }

        private static int HashCoordinates(int x, int y, int z, int seed)
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
