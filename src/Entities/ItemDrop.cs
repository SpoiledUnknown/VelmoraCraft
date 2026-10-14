using OpenTK.Mathematics;
using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine.src.Entities
{
    public class ItemDrop
    {
        public BlockType ItemType { get; set; }
        public int Count { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public bool IsAlive { get; set; } = true;
        public float Lifetime { get; set; } = 0.0f;

        public ItemDrop(BlockType type, Vector3 position, int count = 1)
        {
            ItemType = type;
            Position = position;
            Count = count;
            // Pop slightly upwards
            Velocity = new Vector3(
                (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * 1.5f,
                3.5f,
                (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * 1.5f
            );
        }

        public void Update(World.World world, float deltaTime)
        {
            Lifetime += deltaTime;
            if (Lifetime > 300.0f) // Despawn after 5 minutes
            {
                IsAlive = false;
                return;
            }

            // Apply gravity
            Velocity = new Vector3(Velocity.X * 0.95f, Velocity.Y - 18.0f * deltaTime, Velocity.Z * 0.95f);
            Vector3 newPos = Position + (Velocity * deltaTime);

            // Simple floor collision
            int checkX = (int)MathF.Round(newPos.X);
            int checkY = (int)MathF.Floor(newPos.Y);
            int checkZ = (int)MathF.Round(newPos.Z);

            if (world.GetBlockWorld(checkX, checkY, checkZ) != (byte)BlockType.Empty)
            {
                newPos.Y = checkY + 0.6f;
                Velocity = new Vector3(Velocity.X * 0.7f, 0, Velocity.Z * 0.7f);
            }

            Position = newPos;
        }
    }
}
