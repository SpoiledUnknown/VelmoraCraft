using OpenTK.Mathematics;

namespace VoxelRenderEngine.src.UI
{
    public class ChestManager
    {
        private readonly Dictionary<Vector3i, Inventory> chests = new Dictionary<Vector3i, Inventory>();
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

        public void RemoveChest(Vector3i position)
        {
            chests.Remove(position);
        }

        public void Clear()
        {
            chests.Clear();
        }
    }
}
