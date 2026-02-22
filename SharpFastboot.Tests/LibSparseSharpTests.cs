using LibSparseSharp;
using System.Buffers.Binary;

namespace SharpFastboot.Tests
{
    public class LibSparseSharpTests
    {
        [Fact]
        public void SparseHeader_ToBytes_FromBytes_RoundTrip()
        {
            var header = new SparseHeader
            {
                Magic = SparseFormat.SparseHeaderMagic,
                MajorVersion = 1,
                MinorVersion = 0,
                FileHeaderSize = SparseFormat.SparseHeaderSize,
                ChunkHeaderSize = SparseFormat.ChunkHeaderSize,
                BlockSize = 4096,
                TotalBlocks = 100,
                TotalChunks = 5,
                ImageChecksum = 0x12345678
            };

            var bytes = header.ToBytes();
            var restored = SparseHeader.FromBytes(bytes);

            Assert.Equal(header.Magic, restored.Magic);
            Assert.Equal(header.BlockSize, restored.BlockSize);
            Assert.Equal(header.TotalBlocks, restored.TotalBlocks);
            Assert.Equal(header.TotalChunks, restored.TotalChunks);
            Assert.Equal(header.ImageChecksum, restored.ImageChecksum);
        }

        [Fact]
        public void SparseHeader_IsValid_ChecksCorrectly()
        {
            var validHeader = new SparseHeader
            {
                Magic = SparseFormat.SparseHeaderMagic,
                MajorVersion = 1,
                MinorVersion = 0,
                FileHeaderSize = SparseFormat.SparseHeaderSize,
                ChunkHeaderSize = SparseFormat.ChunkHeaderSize,
                BlockSize = 4096,
                TotalBlocks = 100,
                TotalChunks = 5
            };

            Assert.True(validHeader.IsValid());

            var invalidMagic = validHeader with { Magic = 0x11223344 };
            Assert.False(invalidMagic.IsValid());

            var invalidMajor = validHeader with { MajorVersion = 2 };
            Assert.False(invalidMajor.IsValid());

            var invalidBlockSize = validHeader with { BlockSize = 3 }; // not multiple of 4
            Assert.False(invalidBlockSize.IsValid());
        }

        [Fact]
        public void ChunkHeader_FromBytes_ParsesCorrectly()
        {
            byte[] data = new byte[12];
            BinaryPrimitives.WriteUInt16LittleEndian(data, SparseFormat.ChunkTypeRaw);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 10); // ChunkSize
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 40960 + 12); // TotalSize (10*4096 + 12)

            var chunkHeader = ChunkHeader.FromBytes(data);

            Assert.Equal(SparseFormat.ChunkTypeRaw, chunkHeader.ChunkType);
            Assert.Equal(10u, chunkHeader.ChunkSize);
            Assert.Equal(40972u, chunkHeader.TotalSize);
        }
    }
}
