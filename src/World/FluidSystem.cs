using OpenTK.Mathematics;

namespace VoxelRenderEngine.src.World
{
    public class FluidSystem
    {
        public const int MAX_FLOW_DISTANCE = 8;
        private const float TICK_INTERVAL = 0.15f; // Step interval for static expanding blocks

        public struct FlowNode
        {
            public Vector3i Pos;
            public BlockType FluidType;
            public int Distance;
            public Vector3i Source;

            public FlowNode(Vector3i pos, BlockType fluidType, int distance, Vector3i source)
            {
                Pos = pos;
                FluidType = fluidType;
                Distance = distance;
                Source = source;
            }
        }

        private static readonly Vector3i[] CardinalOffsets = new Vector3i[]
        {
            new Vector3i(1, 0, 0),
            new Vector3i(-1, 0, 0),
            new Vector3i(0, 1, 0),
            new Vector3i(0, -1, 0),
            new Vector3i(0, 0, 1),
            new Vector3i(0, 0, -1)
        };

        private readonly Queue<FlowNode> activeQueue = new Queue<FlowNode>();
        private readonly HashSet<Vector3i> visited = new HashSet<Vector3i>();
        private float timer = 0f;

        /// <summary>
        /// Triggers fluid flow expansion from the specified source block.
        /// The fluid expands up to 8 blocks away into adjacent empty blocks.
        /// </summary>
        public void TriggerFlow(World world, Vector3i sourcePos, BlockType fluidType)
        {
            if (fluidType != BlockType.Water && fluidType != BlockType.Lava) return;

            var node = new FlowNode(sourcePos, fluidType, 0, sourcePos);
            visited.Add(sourcePos);
            activeQueue.Enqueue(node);
        }

        /// <summary>
        /// Checks if any neighbors around a broken block contain fluid, and triggers flow into the newly emptied space.
        /// </summary>
        public void OnBlockBroken(World world, Vector3i brokenPos)
        {
            for (int i = 0; i < CardinalOffsets.Length; i++)
            {
                var offset = CardinalOffsets[i];
                int nx = brokenPos.X + offset.X;
                int ny = brokenPos.Y + offset.Y;
                int nz = brokenPos.Z + offset.Z;

                if (ny < 1 || ny >= Chunk.SIZE_Y) continue;
                byte blockId = world.GetBlockWorld(nx, ny, nz);
                if (blockId == (byte)BlockType.Water)
                {
                    TriggerFlow(world, new Vector3i(nx, ny, nz), BlockType.Water);
                }
                else if (blockId == (byte)BlockType.Lava)
                {
                    TriggerFlow(world, new Vector3i(nx, ny, nz), BlockType.Lava);
                }
            }
        }

        public void Update(World world, float deltaTime)
        {
            if (activeQueue.Count == 0)
            {
                if (visited.Count > 1000) visited.Clear();
                return;
            }

            timer += deltaTime;
            if (timer < TICK_INTERVAL) return;
            timer = 0f;

            // Process one wave of expanding static blocks
            int batchSize = Math.Max(1, activeQueue.Count);
            for (int i = 0; i < batchSize; i++)
            {
                if (activeQueue.Count == 0) break;
                var current = activeQueue.Dequeue();

                // 1. Gravity: Try expanding straight DOWN first
                int downY = current.Pos.Y - 1;
                if (downY >= 1)
                {
                    byte belowBlock = world.GetBlockWorld(current.Pos.X, downY, current.Pos.Z);
                    if (belowBlock == (byte)BlockType.Empty)
                    {
                        var downPos = new Vector3i(current.Pos.X, downY, current.Pos.Z);
                        world.SetBlockWorld(downPos.X, downPos.Y, downPos.Z, current.FluidType);

                        if (visited.Add(downPos))
                        {
                            // Falling liquid resets horizontal flow distance
                            activeQueue.Enqueue(new FlowNode(downPos, current.FluidType, 0, downPos));
                        }
                        // Falling liquid prioritizes vertical downward expansion
                        continue;
                    }
                    else if (belowBlock == (byte)BlockType.Water && current.FluidType == BlockType.Lava)
                    {
                        world.SetBlockWorld(current.Pos.X, downY, current.Pos.Z, BlockType.Stone);
                        continue;
                    }
                    else if (belowBlock == (byte)BlockType.Lava && current.FluidType == BlockType.Water)
                    {
                        world.SetBlockWorld(current.Pos.X, downY, current.Pos.Z, BlockType.Cobblestone);
                        continue;
                    }
                }

                // 2. Lateral: Expand HORIZONTALLY in 4 cardinal directions up to 8 blocks
                int nextDist = current.Distance + 1;
                if (nextDist > MAX_FLOW_DISTANCE)
                {
                    continue; // Reached maximum 8-block flow limit from source!
                }

                Vector3i[] horizontalDirs = new Vector3i[]
                {
                    new Vector3i( 1, 0,  0),
                    new Vector3i(-1, 0,  0),
                    new Vector3i( 0, 0,  1),
                    new Vector3i( 0, 0, -1)
                };

                foreach (var dir in horizontalDirs)
                {
                    var neighbor = current.Pos + dir;
                    if (neighbor.Y < 1 || neighbor.Y >= Chunk.SIZE_Y) continue;

                    byte neighborBlock = world.GetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z);

                    // Fluid collision interactions
                    if (neighborBlock == (byte)BlockType.Water && current.FluidType == BlockType.Lava)
                    {
                        world.SetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z, BlockType.Cobblestone);
                        continue;
                    }
                    if (neighborBlock == (byte)BlockType.Lava && current.FluidType == BlockType.Water)
                    {
                        world.SetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z, BlockType.Obsidian);
                        continue;
                    }

                    // Expand only into empty air until hitting another block
                    if (neighborBlock == (byte)BlockType.Empty)
                    {
                        if (visited.Add(neighbor))
                        {
                            world.SetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z, current.FluidType);
                            activeQueue.Enqueue(new FlowNode(neighbor, current.FluidType, nextDist, current.Source));
                        }
                    }
                }
            }
        }
    }
}
