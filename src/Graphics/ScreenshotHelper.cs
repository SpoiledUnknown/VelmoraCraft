using System.IO.Compression;
using OpenTK.Graphics.OpenGL;

namespace VelmoraCraft.src.Graphics
{
    public static class ScreenshotHelper
    {
        public static string CaptureAndSave(int width, int height)
        {
            // 1. Read pixels from OpenGL framebuffer (RGBA, 4 bytes per pixel)
            byte[] glPixels = new byte[width * height * 4];
            GL.PixelStorei(PixelStoreParameter.PackAlignment, 1);
            unsafe
            {
                fixed (byte* p = glPixels)
                {
                    GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)p);
                }
            }

            // 2. Prepare destination path in user's Pictures folder
            string picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string screenshotsDir = Path.Combine(picturesDir, "VoxelScreenshots");
            Directory.CreateDirectory(screenshotsDir);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
            string baseFilename = $"screenshot_{timestamp}";
            string fullPath = Path.Combine(screenshotsDir, $"{baseFilename}.png");
            int counter = 1;
            while (File.Exists(fullPath))
            {
                fullPath = Path.Combine(screenshotsDir, $"{baseFilename}_{counter++}.png");
            }

            // 3. Save as standard PNG with vertical flip (OpenGL bottom-to-top -> PNG top-to-bottom)
            SavePng(fullPath, glPixels, width, height);

            return fullPath;
        }

        private static void SavePng(string filePath, byte[] glPixels, int width, int height)
        {
            // Raw uncompressed IDAT scanline buffer:
            // Each scanline starts with 1 filter byte (0 = None), followed by width * 4 RGBA bytes
            int stride = width * 4;
            byte[] rawData = new byte[height * (stride + 1)];

            for (int y = 0; y < height; y++)
            {
                // OpenGL is bottom-to-top, PNG is top-to-bottom
                int glY = height - 1 - y;
                int srcOffset = glY * stride;
                int dstOffset = y * (stride + 1);

                rawData[dstOffset] = 0; // Filter: None
                System.Buffer.BlockCopy(glPixels, srcOffset, rawData, dstOffset + 1, stride);
            }

            // Compress raw scanline data using zlib
            byte[] compressedData;
            using (var memoryStream = new MemoryStream())
            {
                using (var zlib = new ZLibStream(memoryStream, CompressionLevel.Fastest))
                {
                    zlib.Write(rawData, 0, rawData.Length);
                }
                compressedData = memoryStream.ToArray();
            }

            // Write PNG File
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // PNG signature
                bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

                // IHDR chunk
                byte[] ihdr = new byte[13];
                WriteBigEndianInt32(ihdr, 0, width);
                WriteBigEndianInt32(ihdr, 4, height);
                ihdr[8] = 8; // Bit depth: 8
                ihdr[9] = 6; // Color type: RGBA (6)
                ihdr[10] = 0; // Compression: Deflate
                ihdr[11] = 0; // Filter: Standard
                ihdr[12] = 0; // Interlace: None
                WriteChunk(bw, "IHDR"u8.ToArray(), ihdr);

                // IDAT chunk
                WriteChunk(bw, "IDAT"u8.ToArray(), compressedData);

                // IEND chunk
                WriteChunk(bw, "IEND"u8.ToArray(), Array.Empty<byte>());
            }
        }

        private static void WriteChunk(BinaryWriter bw, byte[] type, byte[] data)
        {
            // Chunk length (big-endian)
            bw.Write(SwapBytes((uint)data.Length));
            // Chunk type
            bw.Write(type);
            // Chunk data
            if (data.Length > 0)
            {
                bw.Write(data);
            }
            // CRC32 calculated over type and data
            uint crc = CalculateCrc(type, data);
            bw.Write(SwapBytes(crc));
        }

        private static void WriteBigEndianInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset + 0] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        private static uint SwapBytes(uint value)
        {
            return ((value & 0x000000FFU) << 24) |
                   ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) |
                   ((value & 0xFF000000U) >> 24);
        }

        private static readonly uint[] CrcTable = new uint[256];

        static ScreenshotHelper()
        {
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) != 0)
                        c = 0xedb88320U ^ (c >> 1);
                    else
                        c >>= 1;
                }
                CrcTable[n] = c;
            }
        }

        private static uint CalculateCrc(byte[] type, byte[] data)
        {
            uint crc = 0xffffffffU;
            for (int i = 0; i < type.Length; i++)
                crc = CrcTable[(crc ^ type[i]) & 0xff] ^ (crc >> 8);
            for (int i = 0; i < data.Length; i++)
                crc = CrcTable[(crc ^ data[i]) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffffU;
        }
    }
}
