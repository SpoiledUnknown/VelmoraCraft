using OpenTK.Mathematics;
using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine.src.Physics
{
    public struct RaycastHit
    {
        public bool HasHit;
        public Vector3i BlockPos;
        public Vector3i Normal;
        public Face HitFace;
        public float Distance;
    }

    public static class VoxelRaycaster
    {
        /// <summary>
        /// Fast Voxel Traversal Algorithm (Amanatides & Woo DDA)
        /// Traverses grid voxels step-by-step to find the targeted block and adjacent placement face.
        /// </summary>
        public static RaycastHit Raycast(World.World world, Vector3 origin, Vector3 direction, float maxDistance = 6.0f)
        {
            RaycastHit hit = new RaycastHit { HasHit = false, Distance = maxDistance };
            if (direction.LengthSquared < 0.0001f) return hit;

            direction = Vector3.Normalize(direction);

            // Starting voxel coordinate (since cubes are [-0.5, 0.5] centered on integer world coords)
            int x = (int)MathF.Round(origin.X);
            int y = (int)MathF.Round(origin.Y);
            int z = (int)MathF.Round(origin.Z);

            int stepX = direction.X > 0 ? 1 : (direction.X < 0 ? -1 : 0);
            int stepY = direction.Y > 0 ? 1 : (direction.Y < 0 ? -1 : 0);
            int stepZ = direction.Z > 0 ? 1 : (direction.Z < 0 ? -1 : 0);

            // Distance to next voxel boundary
            float nextVoxelBoundaryX = x + (stepX > 0 ? 0.5f : -0.5f);
            float nextVoxelBoundaryY = y + (stepY > 0 ? 0.5f : -0.5f);
            float nextVoxelBoundaryZ = z + (stepZ > 0 ? 0.5f : -0.5f);

            float tMaxX = (stepX != 0) ? (nextVoxelBoundaryX - origin.X) / direction.X : float.PositiveInfinity;
            float tMaxY = (stepY != 0) ? (nextVoxelBoundaryY - origin.Y) / direction.Y : float.PositiveInfinity;
            float tMaxZ = (stepZ != 0) ? (nextVoxelBoundaryZ - origin.Z) / direction.Z : float.PositiveInfinity;

            float tDeltaX = (stepX != 0) ? MathF.Abs(1.0f / direction.X) : float.PositiveInfinity;
            float tDeltaY = (stepY != 0) ? MathF.Abs(1.0f / direction.Y) : float.PositiveInfinity;
            float tDeltaZ = (stepZ != 0) ? MathF.Abs(1.0f / direction.Z) : float.PositiveInfinity;

            Vector3i normal = Vector3i.Zero;
            Face hitFace = Face.Top;
            float currentDist = 0.0f;

            while (currentDist <= maxDistance)
            {
                byte blockId = world.GetBlockWorld(x, y, z);
                if (blockId != (byte)BlockType.Empty && blockId != (byte)BlockType.Water && blockId != (byte)BlockType.Lava)
                {
                    hit.HasHit = true;
                    hit.BlockPos = new Vector3i(x, y, z);
                    hit.Normal = normal;
                    hit.HitFace = hitFace;
                    hit.Distance = currentDist;
                    return hit;
                }

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        currentDist = tMaxX;
                        x += stepX;
                        tMaxX += tDeltaX;
                        normal = new Vector3i(-stepX, 0, 0);
                        hitFace = stepX > 0 ? Face.Left : Face.Right;
                    }
                    else
                    {
                        currentDist = tMaxZ;
                        z += stepZ;
                        tMaxZ += tDeltaZ;
                        normal = new Vector3i(0, 0, -stepZ);
                        hitFace = stepZ > 0 ? Face.Back : Face.Front;
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        currentDist = tMaxY;
                        y += stepY;
                        tMaxY += tDeltaY;
                        normal = new Vector3i(0, -stepY, 0);
                        hitFace = stepY > 0 ? Face.Bottom : Face.Top;
                    }
                    else
                    {
                        currentDist = tMaxZ;
                        z += stepZ;
                        tMaxZ += tDeltaZ;
                        normal = new Vector3i(0, 0, -stepZ);
                        hitFace = stepZ > 0 ? Face.Back : Face.Front;
                    }
                }
            }

            return hit;
        }
    }
}
