using VelmoraCraft.src.World;

namespace VelmoraCraft.src.UI
{
    public struct ItemStack
    {
        public BlockType Type;
        public int Count;

        public bool IsEmpty => Type == BlockType.Empty || Count <= 0;

        public ItemStack(BlockType type, int count = 1)
        {
            Type = type;
            Count = count;
        }

        public static ItemStack Empty => new ItemStack(BlockType.Empty, 0);

        public void Clear()
        {
            Type = BlockType.Empty;
            Count = 0;
        }
    }

    public class Inventory
    {
        public ItemStack[] Slots { get; }
        public int SlotCount => Slots.Length;

        public Inventory(int size)
        {
            Slots = new ItemStack[size];
            for (int i = 0; i < size; i++)
            {
                Slots[i] = ItemStack.Empty;
            }
        }

        public bool AddItem(BlockType type, int count = 1, int maxStack = 64)
        {
            if (type == BlockType.Empty || count <= 0) return false;

            // 1. Try to merge into existing slots
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].Type == type && Slots[i].Count < maxStack)
                {
                    int space = maxStack - Slots[i].Count;
                    int add = Math.Min(space, count);
                    Slots[i].Count += add;
                    count -= add;

                    if (count <= 0) return true;
                }
            }

            // 2. Try to put into empty slots
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsEmpty)
                {
                    int add = Math.Min(maxStack, count);
                    Slots[i] = new ItemStack(type, add);
                    count -= add;

                    if (count <= 0) return true;
                }
            }

            return count <= 0;
        }

        public bool HasItem(BlockType type, int count)
        {
            int total = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Type == type) total += Slots[i].Count;
                if (total >= count) return true;
            }
            return false;
        }

        public bool ConsumeItem(BlockType type, int count)
        {
            if (!HasItem(type, count)) return false;

            int remaining = count;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Type == type)
                {
                    int take = Math.Min(Slots[i].Count, remaining);
                    Slots[i].Count -= take;
                    remaining -= take;

                    if (Slots[i].Count <= 0)
                    {
                        Slots[i].Clear();
                    }

                    if (remaining <= 0) break;
                }
            }
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                Slots[i] = ItemStack.Empty;
            }
        }
    }
}
