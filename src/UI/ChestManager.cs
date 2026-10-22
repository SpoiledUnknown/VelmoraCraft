using OpenTK.Mathematics;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.UI;

public class DoubleChestInventory : Inventory
{
    public DoubleChestInventory(Vector3i pos1, Inventory inv1, Vector3i pos2, Inventory inv2) : base(36)
    {
        PrimaryPosition = pos1;
        SecondaryPosition = pos2;
        Inv1 = inv1;
        Inv2 = inv2;
        SyncFromChests();
    }

    public Vector3i PrimaryPosition { get; }
    public Vector3i SecondaryPosition { get; }
    public Inventory Inv1 { get; }
    public Inventory Inv2 { get; }

    public void SyncFromChests()
    {
        for (var i = 0; i < 18; i++)
        {
            Slots[i] = Inv1.Slots[i];
            Slots[18 + i] = Inv2.Slots[i];
        }
    }

    public void SyncToChests()
    {
        for (var i = 0; i < 18; i++)
        {
            Inv1.Slots[i] = Slots[i];
            Inv2.Slots[i] = Slots[18 + i];
        }
    }

    public new bool AddItem(BlockType type, int count = 1, int maxStack = 64)
    {
        var res = base.AddItem(type, count, maxStack);
        SyncToChests();
        return res;
    }

    public new bool ConsumeItem(BlockType type, int count)
    {
        var res = base.ConsumeItem(type, count);
        SyncToChests();
        return res;
    }
}

public class ChestManager
{
    private readonly Dictionary<Vector3i, Inventory> chests = new();
    public IReadOnlyDictionary<Vector3i, Inventory> AllChests => chests;

    public Inventory GetOrCreateChest(Vector3i position)
    {
        if (!chests.TryGetValue(position, out var chest))
        {
            chest = new Inventory(18); // 18 slots (2 rows of 9)
            chests[position] = chest;
        }

        return chest;
    }

    public Inventory GetChestInventory(Vector3i position, World.World world)
    {
        if (TryGetDoubleChestPartner(position, (x, y, z) => world.GetBlockWorld(x, y, z), out var partnerPos,
                out var isFirstHalf, out _))
        {
            var pos1 = isFirstHalf ? position : partnerPos;
            var pos2 = isFirstHalf ? partnerPos : position;

            var inv1 = GetOrCreateChest(pos1);
            var inv2 = GetOrCreateChest(pos2);
            return new DoubleChestInventory(pos1, inv1, pos2, inv2);
        }

        return GetOrCreateChest(position);
    }

    public static bool TryGetDoubleChestPartner(
        Vector3i pos,
        Func<int, int, int, byte> getBlock,
        out Vector3i partnerPos,
        out bool isFirstHalf,
        out bool isAxisX)
    {
        var startX = pos.X;
        while (startX - pos.X > -16 && getBlock(startX - 1, pos.Y, pos.Z) == (byte)BlockType.Chest) startX--;

        var endX = pos.X;
        while (endX - pos.X < 16 && getBlock(endX + 1, pos.Y, pos.Z) == (byte)BlockType.Chest) endX++;

        var lengthX = endX - startX + 1;
        if (lengthX >= 2)
        {
            var index = pos.X - startX;
            if (index % 2 == 0)
            {
                if (pos.X + 1 <= endX)
                {
                    partnerPos = new Vector3i(pos.X + 1, pos.Y, pos.Z);
                    isFirstHalf = true;
                    isAxisX = true;
                    return true;
                }
            }
            else
            {
                partnerPos = new Vector3i(pos.X - 1, pos.Y, pos.Z);
                isFirstHalf = false;
                isAxisX = true;
                return true;
            }
        }

        var startZ = pos.Z;
        while (startZ - pos.Z > -16 && getBlock(pos.X, pos.Y, startZ - 1) == (byte)BlockType.Chest) startZ--;

        var endZ = pos.Z;
        while (endZ - pos.Z < 16 && getBlock(pos.X, pos.Y, endZ + 1) == (byte)BlockType.Chest) endZ++;

        var lengthZ = endZ - startZ + 1;
        if (lengthZ >= 2)
        {
            var index = pos.Z - startZ;
            if (index % 2 == 0)
            {
                if (pos.Z + 1 <= endZ)
                {
                    partnerPos = new Vector3i(pos.X, pos.Y, pos.Z + 1);
                    isFirstHalf = true;
                    isAxisX = false;
                    return true;
                }
            }
            else
            {
                partnerPos = new Vector3i(pos.X, pos.Y, pos.Z - 1);
                isFirstHalf = false;
                isAxisX = false;
                return true;
            }
        }

        partnerPos = pos;
        isFirstHalf = false;
        isAxisX = false;
        return false;
    }

    public void RemoveChest(Vector3i position)
    {
        chests.Remove(position);
    }

    public void Clear()
    {
        chests.Clear();
    }
}