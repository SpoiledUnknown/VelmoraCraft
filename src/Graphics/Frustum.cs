using OpenTK.Mathematics;

namespace VelmoraCraft.src.Graphics
{
    /// <summary>
    /// High-performance 3D View Frustum for bounding-box culling.
    /// Extracts the 6 frustum planes (Left, Right, Bottom, Top, Near, Far) from the View-Projection matrix
    /// using the normalized Gribb-Hartmann algorithm.
    /// </summary>
    public class Frustum
    {
        private Vector4 left;
        private Vector4 right;
        private Vector4 bottom;
        private Vector4 top;
        private Vector4 near;
        private Vector4 far;

        public void Update(Matrix4 vp)
        {
            // Extract the 6 frustum planes for row-major vector convention (v * VP) in OpenTK
            left   = new Vector4(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41);
            right  = new Vector4(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41);
            bottom = new Vector4(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42);
            top    = new Vector4(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42);
            near   = new Vector4(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43);
            far    = new Vector4(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43);

            NormalizePlane(ref left);
            NormalizePlane(ref right);
            NormalizePlane(ref bottom);
            NormalizePlane(ref top);
            NormalizePlane(ref near);
            NormalizePlane(ref far);
        }

        private static void NormalizePlane(ref Vector4 p)
        {
            float len = MathF.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
            if (len > 1e-6f)
            {
                float inv = 1.0f / len;
                p.X *= inv;
                p.Y *= inv;
                p.Z *= inv;
                p.W *= inv;
            }
        }

        /// <summary>
        /// Tests if an axis-aligned bounding box (min to max) is at least partially inside the frustum.
        /// Returns false if the box is entirely outside any of the 6 planes (culled).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsBoxVisible(Vector3 min, Vector3 max)
        {
            return IsOutsidePlane(in left, min, max) &&
                   IsOutsidePlane(in right, min, max) &&
                   IsOutsidePlane(in bottom, min, max) &&
                   IsOutsidePlane(in top, min, max) &&
                   IsOutsidePlane(in near, min, max) &&
                   IsOutsidePlane(in far, min, max);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool IsOutsidePlane(in Vector4 p, Vector3 min, Vector3 max)
        {
            // P-vertex: corner of the bounding box furthest along the plane normal
            float px = p.X >= 0f ? max.X : min.X;
            float py = p.Y >= 0f ? max.Y : min.Y;
            float pz = p.Z >= 0f ? max.Z : min.Z;

            return (p.X * px + p.Y * py + p.Z * pz + p.W) >= 0f;
        }
    }
}
