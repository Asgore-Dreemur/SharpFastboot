using LibLpSharp;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SharpFastboot.Tests
{
    public class LibLpSharpTests
    {
        private byte[] ToBytes<T>(T value) where T : struct
        {
            var bytes = new byte[Marshal.SizeOf<T>()];
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.StructureToPtr(value, ptr, false);
                Marshal.Copy(ptr, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        [Fact]
        public void LpMetadataGeometry_FromBytes_ComputeChecksum_RoundTrip()
        {
            var geometry = new LpMetadataGeometry
            {
                Magic = MetadataFormat.LP_METADATA_GEOMETRY_MAGIC,
                StructSize = (uint)Marshal.SizeOf<LpMetadataGeometry>(),
                MetadataSlotCount = 4,
                MetadataMaxSize = 65536,
                LogicalBlockSize = 4096
            };

            var buffer = ToBytes(geometry);

            // Re-calculate checksum for the test
            using var sha256 = SHA256.Create();
            byte[] forChecksum = (byte[])buffer.Clone();
            for (int i = 0; i < 32; i++) forChecksum[8 + i] = 0;
            byte[] computed = sha256.ComputeHash(forChecksum);
            for (int i = 0; i < 32; i++) buffer[8 + i] = computed[i];

            MetadataReader.ParseGeometry(buffer, out var parsed);

            Assert.Equal(geometry.Magic, parsed.Magic);
            Assert.Equal(geometry.MetadataSlotCount, parsed.MetadataSlotCount);
            Assert.Equal(geometry.MetadataMaxSize, parsed.MetadataMaxSize);
            Assert.Equal(geometry.LogicalBlockSize, parsed.LogicalBlockSize);
        }

        [Fact]
        public void LpMetadataGeometry_InvalidMagic_Throws()
        {
            var geometry = new LpMetadataGeometry
            {
                Magic = 0x11223344,
                StructSize = (uint)Marshal.SizeOf<LpMetadataGeometry>()
            };

            var buffer = ToBytes(geometry);
            Assert.Throws<InvalidDataException>(() => MetadataReader.ParseGeometry(buffer, out var _));
        }
    }
}
