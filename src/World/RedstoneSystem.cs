using OpenTK.Mathematics;

namespace VelmoraCraft.src.World;

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
    private static readonly List<ScheduledBlockRevert> ScheduledReverts = new();

    private static readonly Vector3i[] NeighborOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0),
        new(0, 0, 1),
        new(0, 0, -1)
    };

    public static void Update(World world, float deltaTime)
    {
        for (var i = ScheduledReverts.Count - 1; i >= 0; i--)
        {
            var item = ScheduledReverts[i];
            item.TimeRemaining -= deltaTime;

            if (item.TimeRemaining <= 0)
            {
                world.SetBlockWorld(item.Position.X, item.Position.Y, item.Position.Z, item.RevertType);
                PropagatePower(world, item.Position, false);
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
        var blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);

        var (lowerPos, upperPos, isCurrentlyOpen, isIron, isValid) = (BlockType)blockId switch
        {
            BlockType.DoorLower => (pos, pos + Vector3i.UnitY, false, false, true),
            BlockType.DoorUpper => (pos - Vector3i.UnitY, pos, false, false, true),
            BlockType.DoorLowerOpen => (pos, pos + Vector3i.UnitY, true, false, true),
            BlockType.DoorUpperOpen => (pos - Vector3i.UnitY, pos, true, false, true),
            BlockType.IronDoorLower => (pos, pos + Vector3i.UnitY, false, true, true),
            BlockType.IronDoorUpper => (pos - Vector3i.UnitY, pos, false, true, true),
            BlockType.IronDoorLowerOpen => (pos, pos + Vector3i.UnitY, true, true, true),
            BlockType.IronDoorUpperOpen => (pos - Vector3i.UnitY, pos, true, true, true),
            _ => (pos, pos, false, false, false)
        };

        if (!isValid) return false;

        var doorMeta = world.GetBlockMetadata(lowerPos.X, lowerPos.Y, lowerPos.Z);
        if (doorMeta == 0)
        {
            var uMeta = world.GetBlockMetadata(upperPos.X, upperPos.Y, upperPos.Z);
            if (uMeta != 0) doorMeta = uMeta;
        }

        if (isCurrentlyOpen)
        {
            world.SetBlockWorld(lowerPos.X, lowerPos.Y, lowerPos.Z,
                isIron ? BlockType.IronDoorLower : BlockType.DoorLower, doorMeta);
            world.SetBlockWorld(upperPos.X, upperPos.Y, upperPos.Z,
                isIron ? BlockType.IronDoorUpper : BlockType.DoorUpper, doorMeta);
        }
        else
        {
            world.SetBlockWorld(lowerPos.X, lowerPos.Y, lowerPos.Z,
                isIron ? BlockType.IronDoorLowerOpen : BlockType.DoorLowerOpen, doorMeta);
            world.SetBlockWorld(upperPos.X, upperPos.Y, upperPos.Z,
                isIron ? BlockType.IronDoorUpperOpen : BlockType.DoorUpperOpen, doorMeta);
        }

        return true;
    }

    public static bool ToggleLever(World world, Vector3i pos)
    {
        var blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);

        if (blockId == (byte)BlockType.Lever)
        {
            world.SetBlockWorld(pos.X, pos.Y, pos.Z, BlockType.LeverOn);
            PropagatePower(world, pos, true);
            return true;
        }

        if (blockId == (byte)BlockType.LeverOn)
        {
            world.SetBlockWorld(pos.X, pos.Y, pos.Z, BlockType.Lever);
            PropagatePower(world, pos, false);
            return true;
        }

        return false;
    }

    public static bool PressButton(World world, Vector3i pos)
    {
        var blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);

        if (blockId == (byte)BlockType.StoneButton)
        {
            world.SetBlockWorld(pos.X, pos.Y, pos.Z, BlockType.StoneButtonPressed);
            PropagatePower(world, pos, true);
            ScheduledReverts.Add(new ScheduledBlockRevert(pos, BlockType.StoneButton, 1.2f));
            return true;
        }

        return false;
    }

    public static void PropagatePower(World world, Vector3i sourcePos, bool isPowered)
    {
        foreach (var offset in NeighborOffsets)
        {
            var neighbor = sourcePos + offset;
            var neighborId = world.GetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z);

            if (isPowered)
            {
                if (neighborId == (byte)BlockType.PoweredRail)
                    world.SetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z, BlockType.PoweredRailOn);
                else if (neighborId is (byte)BlockType.DoorLower or (byte)BlockType.DoorUpper or
                         (byte)BlockType.IronDoorLower or (byte)BlockType.IronDoorUpper)
                    SetDoorState(world, neighbor, true);
            }
            else
            {
                if (neighborId == (byte)BlockType.PoweredRailOn)
                {
                    if (!HasActivePowerNeighbor(world, neighbor, sourcePos))
                        world.SetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z, BlockType.PoweredRail);
                }
                else if (neighborId is (byte)BlockType.DoorLowerOpen or (byte)BlockType.DoorUpperOpen or
                         (byte)BlockType.IronDoorLowerOpen or (byte)BlockType.IronDoorUpperOpen)
                {
                    if (!HasActivePowerNeighbor(world, neighbor, sourcePos)) SetDoorState(world, neighbor, false);
                }
            }
        }
    }

    private static void SetDoorState(World world, Vector3i pos, bool open)
    {
        var blockId = world.GetBlockWorld(pos.X, pos.Y, pos.Z);
        var lower = pos;
        var upper = pos;
        var isIron = false;

        if (blockId is (byte)BlockType.DoorLower or (byte)BlockType.DoorLowerOpen)
        {
            upper = pos + Vector3i.UnitY;
        }
        else if (blockId is (byte)BlockType.DoorUpper or (byte)BlockType.DoorUpperOpen)
        {
            lower = pos - Vector3i.UnitY;
        }
        else if (blockId is (byte)BlockType.IronDoorLower or (byte)BlockType.IronDoorLowerOpen)
        {
            upper = pos + Vector3i.UnitY;
            isIron = true;
        }
        else if (blockId is (byte)BlockType.IronDoorUpper or (byte)BlockType.IronDoorUpperOpen)
        {
            lower = pos - Vector3i.UnitY;
            isIron = true;
        }

        var doorMeta = world.GetBlockMetadata(lower.X, lower.Y, lower.Z);
        if (doorMeta == 0)
        {
            var uMeta = world.GetBlockMetadata(upper.X, upper.Y, upper.Z);
            if (uMeta != 0) doorMeta = uMeta;
        }

        if (open)
        {
            world.SetBlockWorld(lower.X, lower.Y, lower.Z,
                isIron ? BlockType.IronDoorLowerOpen : BlockType.DoorLowerOpen, doorMeta);
            world.SetBlockWorld(upper.X, upper.Y, upper.Z,
                isIron ? BlockType.IronDoorUpperOpen : BlockType.DoorUpperOpen, doorMeta);
        }
        else
        {
            world.SetBlockWorld(lower.X, lower.Y, lower.Z, isIron ? BlockType.IronDoorLower : BlockType.DoorLower,
                doorMeta);
            world.SetBlockWorld(upper.X, upper.Y, upper.Z, isIron ? BlockType.IronDoorUpper : BlockType.DoorUpper,
                doorMeta);
        }
    }

    public static bool HasActivePowerNeighbor(World world, Vector3i pos, Vector3i? excludePos = null)
    {
        foreach (var offset in NeighborOffsets)
        {
            var neighbor = pos + offset;
            if (excludePos.HasValue && neighbor == excludePos.Value) continue;

            var b = world.GetBlockWorld(neighbor.X, neighbor.Y, neighbor.Z);
            if (b is (byte)BlockType.LeverOn or (byte)BlockType.StoneButtonPressed) return true;
        }

        return false;
    }
}