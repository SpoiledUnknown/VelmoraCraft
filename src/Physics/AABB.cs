using OpenTK.Mathematics;

namespace VelmoraCraft.src.Physics;

public struct AABB
{
    public Vector3 Min;
    public Vector3 Max;

    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public AABB(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        Min = new Vector3(minX, minY, minZ);
        Max = new Vector3(maxX, maxY, maxZ);
    }

    public readonly AABB Offset(Vector3 offset)
    {
        return new AABB(Min + offset, Max + offset);
    }

    public readonly bool Intersects(AABB other)
    {
        return Min.X < other.Max.X && Max.X > other.Min.X &&
               Min.Y < other.Max.Y && Max.Y > other.Min.Y &&
               Min.Z < other.Max.Z && Max.Z > other.Min.Z;
    }

    public readonly bool IntersectsRay(Vector3 origin, Vector3 dir, float maxDist, out float hitDist)
    {
        hitDist = 0;
        var tmin = (Min.X - origin.X) / (MathF.Abs(dir.X) < 1e-6f ? dir.X >= 0 ? 1e-6f : -1e-6f : dir.X);
        var tmax = (Max.X - origin.X) / (MathF.Abs(dir.X) < 1e-6f ? dir.X >= 0 ? 1e-6f : -1e-6f : dir.X);
        if (tmin > tmax)
        {
            var tmp = tmin;
            tmin = tmax;
            tmax = tmp;
        }

        var tymin = (Min.Y - origin.Y) / (MathF.Abs(dir.Y) < 1e-6f ? dir.Y >= 0 ? 1e-6f : -1e-6f : dir.Y);
        var tymax = (Max.Y - origin.Y) / (MathF.Abs(dir.Y) < 1e-6f ? dir.Y >= 0 ? 1e-6f : -1e-6f : dir.Y);
        if (tymin > tymax)
        {
            var tmp = tymin;
            tymin = tymax;
            tmax = tmp;
        }

        if (tmin > tymax || tymin > tmax) return false;
        if (tymin > tmin) tmin = tymin;
        if (tymax < tmax) tmax = tymax;

        var tzmin = (Min.Z - origin.Z) / (MathF.Abs(dir.Z) < 1e-6f ? dir.Z >= 0 ? 1e-6f : -1e-6f : dir.Z);
        var tzmax = (Max.Z - origin.Z) / (MathF.Abs(dir.Z) < 1e-6f ? dir.Z >= 0 ? 1e-6f : -1e-6f : dir.Z);
        if (tzmin > tzmax)
        {
            var tmp = tzmin;
            tzmin = tzmax;
            tzmax = tmp;
        }

        if (tmin > tzmax || tzmin > tmax) return false;
        if (tzmin > tmin) tmin = tzmin;
        if (tzmax < tmax) tmax = tzmax;

        hitDist = tmin > 0 ? tmin : tmax;
        return hitDist >= 0 && hitDist <= maxDist;
    }
}