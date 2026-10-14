using OpenTK.Mathematics;
using VoxelRenderEngine.src.Entities;

namespace VoxelRenderEngine.src.World
{
    public static class PortalSystem
    {
        /// <summary>
        /// Attempts to ignite an obsidian portal frame around or adjacent to the clicked position.
        /// Returns true if an obsidian frame was found and filled with portal blocks.
        /// </summary>
        public static bool TryIgnitePortal(World world, Vector3i clickedPos)
        {
            // We search around the clicked position (up to 2 blocks in each direction)
            // for the bottom-left corner of the 2x3 inner opening.
            for (int dy = -3; dy <= 1; dy++)
            {
                int baseY = clickedPos.Y + dy;

                // 1. Try X-Y plane (portal faces Z axis)
                for (int dx = -2; dx <= 1; dx++)
                {
                    int baseX = clickedPos.X + dx;
                    int baseZ = clickedPos.Z;

                    if (CheckAndFillXYFrame(world, baseX, baseY, baseZ))
                    {
                        return true;
                    }
                }

                // 2. Try Z-Y plane (portal faces X axis)
                for (int dz = -2; dz <= 1; dz++)
                {
                    int baseX = clickedPos.X;
                    int baseZ = clickedPos.Z + dz;

                    if (CheckAndFillZYFrame(world, baseX, baseY, baseZ))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckAndFillXYFrame(World world, int innerLeftX, int innerBottomY, int z)
        {
            // The inner opening must be 2 wide (innerLeftX, innerLeftX + 1) and 3 high (innerBottomY to innerBottomY + 2)
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    byte b = world.GetBlockWorld(innerLeftX + x, innerBottomY + y, z);
                    // Must be empty or existing portal
                    if (b != (byte)BlockType.Empty && b != (byte)BlockType.Portal)
                    {
                        return false;
                    }
                }
            }

            // Bottom frame (2 obsidian blocks at y = innerBottomY - 1)
            for (int x = 0; x < 2; x++)
            {
                if (world.GetBlockWorld(innerLeftX + x, innerBottomY - 1, z) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Top frame (2 obsidian blocks at y = innerBottomY + 3)
            for (int x = 0; x < 2; x++)
            {
                if (world.GetBlockWorld(innerLeftX + x, innerBottomY + 3, z) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Left column (3 obsidian blocks at x = innerLeftX - 1)
            for (int y = 0; y < 3; y++)
            {
                if (world.GetBlockWorld(innerLeftX - 1, innerBottomY + y, z) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Right column (3 obsidian blocks at x = innerLeftX + 2)
            for (int y = 0; y < 3; y++)
            {
                if (world.GetBlockWorld(innerLeftX + 2, innerBottomY + y, z) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Valid obsidian frame found! Fill the 2x3 opening with glowing Nether Portal blocks
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    world.SetBlockWorld(innerLeftX + x, innerBottomY + y, z, BlockType.Portal);
                }
            }

            return true;
        }

        private static bool CheckAndFillZYFrame(World world, int x, int innerBottomY, int innerLeftZ)
        {
            // The inner opening must be 2 wide in Z and 3 high in Y
            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < 3; y++)
                {
                    byte b = world.GetBlockWorld(x, innerBottomY + y, innerLeftZ + z);
                    if (b != (byte)BlockType.Empty && b != (byte)BlockType.Portal)
                    {
                        return false;
                    }
                }
            }

            // Bottom frame (2 obsidian blocks at y = innerBottomY - 1)
            for (int z = 0; z < 2; z++)
            {
                if (world.GetBlockWorld(x, innerBottomY - 1, innerLeftZ + z) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Top frame (2 obsidian blocks at y = innerBottomY + 3)
            for (int z = 0; z < 2; z++)
            {
                if (world.GetBlockWorld(x, innerBottomY + 3, innerLeftZ + z) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Left column (3 obsidian blocks at z = innerLeftZ - 1)
            for (int y = 0; y < 3; y++)
            {
                if (world.GetBlockWorld(x, innerBottomY + y, innerLeftZ - 1) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Right column (3 obsidian blocks at z = innerLeftZ + 2)
            for (int y = 0; y < 3; y++)
            {
                if (world.GetBlockWorld(x, innerBottomY + y, innerLeftZ + 2) != (byte)BlockType.Obsidian)
                    return false;
            }

            // Valid obsidian frame found! Fill the 2x3 opening with Portal blocks
            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < 3; y++)
                {
                    world.SetBlockWorld(x, innerBottomY + y, innerLeftZ + z, BlockType.Portal);
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the player's bounding box is touching any portal blocks.
        /// </summary>
        public static bool IsPlayerInsidePortal(World world, Player player)
        {
            var aabb = player.GetAABB();
            int minX = (int)MathF.Floor(aabb.Min.X);
            int maxX = (int)MathF.Floor(aabb.Max.X);
            int minY = (int)MathF.Floor(aabb.Min.Y);
            int maxY = (int)MathF.Floor(aabb.Max.Y);
            int minZ = (int)MathF.Floor(aabb.Min.Z);
            int maxZ = (int)MathF.Floor(aabb.Max.Z);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (world.GetBlockWorld(x, y, z) == (byte)BlockType.Portal)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
