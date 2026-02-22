namespace SharpFastboot.DataModel
{
    public struct Ext4FileHeader
    {
        public uint Magic;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ushort FileHeaderSize;
        public ushort ChunkHeaderSize;
        public uint BlockSize;
        public uint TotalBlocks;
        public uint TotalChunks;
        public uint CRC32;
    }

    public struct Ext4ChunkHeader
    {
        public ushort Type;
        public ushort Reserved;
        public uint ChunkSize;
        public uint TotalSize;
    }
}
