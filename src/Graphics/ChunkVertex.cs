using System.Runtime.InteropServices;

namespace VoxelRenderEngine.src.Graphics
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChunkVertex
    {
        public const int SizeInBytes = 44; // 12 + 8 + 12 + 12 = 44 bytes

        public float PosX, PosY, PosZ;      // Location 0 (Offset 0, 12 bytes)
        public float U, V;                  // Location 1 (Offset 12, 8 bytes)
        public float ColR, ColG, ColB;      // Location 2 (Offset 20, 12 bytes)
        public float NormX, NormY, NormZ;   // Location 3 (Offset 32, 12 bytes)

        public ChunkVertex(
            float px, float py, float pz,
            float u, float v,
            float cr, float cg, float cb,
            float nx, float ny, float nz)
        {
            PosX = px; PosY = py; PosZ = pz;
            U = u; V = v;
            ColR = cr; ColG = cg; ColB = cb;
            NormX = nx; NormY = ny; NormZ = nz;
        }
    }
}
