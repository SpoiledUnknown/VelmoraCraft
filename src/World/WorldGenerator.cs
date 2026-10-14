namespace VoxelRenderEngine.src.World
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
                            else
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Grass);
                            }
                        }
                        else if (y > surfaceHeight - 4)
                        {
                            if (biome == BiomeType.Desert || (surfaceHeight < SEA_LEVEL + 2 && biome != BiomeType.Mountains))
                            {
                                chunk.SetBlock(lx, y, lz, BlockType.Sand);
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

                            if (y <= 16 && oreRoll < 12)
                            {
                                rockType = BlockType.Obsidian; // Guarantees obsidian at deep levels!
                            }
                            else if (y <= 40 && oreRoll < 25)
                            {
                                rockType = BlockType.IronOre;
                            }
                            else if (y <= 60 && oreRoll < 55)
                            {
                                rockType = BlockType.CoalOre;
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
                    if (biome == BiomeType.Desert) continue;

                    int treeChance = (biome == BiomeType.Forest) ? 35 : 180; // Much denser in forests

                    int hash = HashCoordinates(wx, 77, wz, seed + 888);
                    if (hash % treeChance == 0)
                    {
                        // Find surface block
                        for (int y = Chunk.SIZE_Y - 10; y >= SEA_LEVEL; y--)
                        {
                            byte block = chunk.GetBlock(lx, y, lz);
                            if (block == (byte)BlockType.Grass)
                            {
                                GrowTree(world, wx, y + 1, wz);
                                break;
                            }
                        }
                    }
                }
            }
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
