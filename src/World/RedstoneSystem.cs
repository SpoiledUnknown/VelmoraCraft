using OpenTK.Mathematics;

namespace VelmoraCraft.src.World
{
    public struct ScheduledBlockRevert
    {
        public Vector3i Position;
        public BlockType RevertType;
        public float TimeRemaining;

        public ScheduledBlockRevert(Vector3i position, BlockType revertType, float time)
        {
            Position = position;
            RevertType = revertType;
            TimeRemaining = time;
        }
    }

    public static class RedstoneSystem
    {
        private static readonly List<ScheduledBlockRevert> ScheduledReverts = new List<ScheduledBlockRevert>();

        private static readonly Vector3i[] NeighborOffsets = new[]
        {
            new Vector3i(1, 0, 0),
            new Vector3i(-1, 0, 0),
            new Vector3i(0, 1, 0),
            new Vector3i(0, -1, 0),
            new Vector3i(0, 0, 1),
            new Vector3i(0, 0, -1)
        };

        public static void Update(World world, float deltaTime)
        {
            for (int i = ScheduledReverts.Count - 1; i >= 0; i--)
            {
                var item = ScheduledReverts[i];
                item.TimeRemaining -= deltaTime;

                if (item.TimeRemaining <= 0)
                {
                    // Revert block (e.g. StoneButtonPressed -> StoneButton)
                    world.SetBlockWorld(item.Position.X, item.Position.Y, item.Position.Z, item.RevertType);
                    PropagatePower(world, item.Position, isPowered: false);
                    ScheduledReverts.RemoveAt(i);
                }
                else
                {
                    ScheduledReverts[i] = item;
                }
            }
        }

        public static bool ToggleDoor(World world, Vector3i pos)
        {
            byte blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);

            Vector3i lowerPos = pos;
            Vector3i upperPos = pos;
            bool isCurrentlyOpen = false;
            bool isIron = false;

            if (blockId == (byte)BlockType.DoorLower)
            {
                upperPos = pos + Vector3i.UnitY;
                isCurrentlyOpen = false;
            }
            else if (blockId == (byte)BlockType.DoorUpper)
            {
                lowerPos = pos - Vector3i.UnitY;
                isCurrentlyOpen = false;
            }
            else if (blockId == (byte)BlockType.DoorLowerOpen)
            {
                upperPos = pos + Vector3i.UnitY;
                isCurrentlyOpen = true;
            }
            else if (blockId == (byte)BlockType.DoorUpperOpen)
            {
                lowerPos = pos - Vector3i.UnitY;
                isCurrentlyOpen = true;
            }
            else if (blockId == (byte)BlockType.IronDoorLower)
            {
                upperPos = pos + Vector3i.UnitY;
                isCurrentlyOpen = false;
                isIron = true;
            }
            else if (blockId == (byte)BlockType.IronDoorUpper)
            {
                lowerPos = pos - Vector3i.UnitY;
                isCurrentlyOpen = false;
                isIron = true;
            }
            else if (blockId == (byte)BlockType.IronDoorLowerOpen)
            {
                upperPos = pos + Vector3i.UnitY;
                isCurrentlyOpen = true;
                isIron = true;
            }
            else if (blockId == (byte)BlockType.IronDoorUpperOpen)
            {
                lowerPos = pos - Vector3i.UnitY;
                isCurrentlyOpen = true;
                isIron = true;
            }
            else
            {
                return false;
            }

            byte doorMeta = world.GetBlockMetadata(lowerPos.X, lowerPos.Y, lowerPos.Z);
            if (doorMeta == 0)
            {
                byte uMeta = world.GetBlockMetadata(upperPos.X, upperPos.Y, upperPos.Z);
                if (uMeta != 0) doorMeta = uMeta;
            }

            if (isCurrentlyOpen)
            {
                // Close door
                world.SetBlockWorld(lowerPos.X, lowerPos.Y, lowerPos.Z, isIron ? BlockType.IronDoorLower : BlockType.DoorLower, metadata: doorMeta);
                world.SetBlockWorld(upperPos.X, upperPos.Y, upperPos.Z, isIron ? BlockType.IronDoorUpper : BlockType.DoorUpper, metadata: doorMeta);
            }
            else
            {
                // Open door
                world.SetBlockWorld(lowerPos.X, lowerPos.Y, lowerPos.Z, isIron ? BlockType.IronDoorLowerOpen : BlockType.DoorLowerOpen, metadata: doorMeta);
                world.SetBlockWorld(upperPos.X, upperPos.Y, upperPos.Z, isIron ? BlockType.IronDoorUpperOpen : BlockType.DoorUpperOpen, metadata: doorMeta);
            }

            return true;
        }

        public static bool ToggleLever(World world, Vector3i pos)
        {
            byte blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);

            if (blockId == (byte)BlockType.Lever)
            {
                world.SetBlockWorld(pos.X, pos.Y, pos.Z, BlockType.LeverOn);
                PropagatePower(world, pos, isPowered: true);
                return true;
            }
            else if (blockId == (byte)BlockType.LeverOn)
            {
                world.SetBlockWorld(pos.X, pos.Y, pos.Z, BlockType.Lever);
                PropagatePower(world, pos, isPowered: false);
                return true;
            }

            return false;
        }

        public static bool PressButton(World world, Vector3i pos)
        {
            byte blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);

            if (blockId == (byte)BlockType.StoneButton)
            {
                world.SetBlockWorld(pos.X, pos.Y, pos.Z, BlockType.StoneButtonPressed);
                PropagatePower(world, pos, isPowered: true);

                // Schedule reset back to unpressed in 1.2 seconds
                ScheduledReverts.Add(new ScheduledBlockRevert(pos, BlockType.StoneButton, 1.2f));
                return true;
            }

            return false;
        }

        public static void PropagatePower(World world, Vector3i sourcePos, bool isPowered)
        {
            foreach (var offset in NeighborOffsets)
            {
                Vector3i neighbor = sourcePos + offset;
                byte neighborId = world.GetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z);

                // 1. Powered Rails
                if (isPowered)
                {
                    if (neighborId == (byte)BlockType.PoweredRail)
                    {
                        world.SetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z, BlockType.PoweredRailOn);
                    }
                }
                else
                {
                    if (neighborId == (byte)BlockType.PoweredRailOn)
                    {
                        // Check if another active source is still powering this rail
                        if (!HasActivePowerNeighbor(world, neighbor, sourcePos))
                        {
                            world.SetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z, BlockType.PoweredRail);
                        }
                    }
                }

                // 2. Doors (Redstone activation)
                if (isPowered)
                {
                    if (neighborId == (byte)BlockType.DoorLower || neighborId == (byte)BlockType.DoorUpper ||
                        neighborId == (byte)BlockType.IronDoorLower || neighborId == (byte)BlockType.IronDoorUpper)
                    {
                        SetDoorState(world, neighbor, open: true);
                    }
                }
                else
                {
                    if (neighborId == (byte)BlockType.DoorLowerOpen || neighborId == (byte)BlockType.DoorUpperOpen ||
                        neighborId == (byte)BlockType.IronDoorLowerOpen || neighborId == (byte)BlockType.IronDoorUpperOpen)
                    {
                        if (!HasActivePowerNeighbor(world, neighbor, sourcePos))
                        {
                            SetDoorState(world, neighbor, open: false);
                        }
                    }
                }
            }
        }

        private static void SetDoorState(World world, Vector3i pos, bool open)
        {
            byte blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);
            Vector3i lower = pos;
            Vector3i upper = pos;
            bool isIron = false;

            if (blockId == (byte)BlockType.DoorLower || blockId == (byte)BlockType.DoorLowerOpen)
            {
                upper = pos + Vector3i.UnitY;
            }
            else if (blockId == (byte)BlockType.DoorUpper || blockId == (byte)BlockType.DoorUpperOpen)
            {
                lower = pos - Vector3i.UnitY;
            }
            else if (blockId == (byte)BlockType.IronDoorLower || blockId == (byte)BlockType.IronDoorLowerOpen)
            {
                upper = pos + Vector3i.UnitY;
                isIron = true;
            }
            else if (blockId == (byte)BlockType.IronDoorUpper || blockId == (byte)BlockType.IronDoorUpperOpen)
            {
                lower = pos - Vector3i.UnitY;
                isIron = true;
            }

            byte doorMeta = world.GetBlockMetadata(lower.X, lower.Y, lower.Z);
            if (doorMeta == 0)
            {
                byte uMeta = world.GetBlockMetadata(upper.X, upper.Y, upper.Z);
                if (uMeta != 0) doorMeta = uMeta;
            }

            if (open)
            {
                world.SetBlockWorld(lower.X, lower.Y, lower.Z, isIron ? BlockType.IronDoorLowerOpen : BlockType.DoorLowerOpen, metadata: doorMeta);
                world.SetBlockWorld(upper.X, upper.Y, upper.Z, isIron ? BlockType.IronDoorUpperOpen : BlockType.DoorUpperOpen, metadata: doorMeta);
            }
            else
            {
                world.SetBlockWorld(lower.X, lower.Y, lower.Z, isIron ? BlockType.IronDoorLower : BlockType.DoorLower, metadata: doorMeta);
                world.SetBlockWorld(upper.X, upper.Y, upper.Z, isIron ? BlockType.IronDoorUpper : BlockType.DoorUpper, metadata: doorMeta);
            }
        }

        public static bool HasActivePowerNeighbor(World world, Vector3i pos, Vector3i? excludePos = null)
        {
            foreach (var offset in NeighborOffsets)
            {
                Vector3i neighbor = pos + offset;
                if (excludePos.HasValue && neighbor == excludePos.Value) continue;

                byte b = world.GetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z);
                if (b == (byte)BlockType.LeverOn || b == (byte)BlockType.StoneButtonPressed)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
