namespace LibSparseSharp;

public class MemoryDataProvider(byte[] data, int offset = 0, int length = -1) : ISparseDataProvider
{
    private readonly int _offset = offset;
    private readonly int _length = length < 0 ? data.Length - offset : length;

    public long Length => _length;

    public void WriteTo(Stream stream) => stream.Write(data, _offset, _length);

    public int Read(long offset, byte[] buffer, int bufferOffset, int count)
    {
        var available = (int)Math.Max(0, _length - offset);
        var toCopy = Math.Min(count, available);
        if (toCopy <= 0)
        {
            return 0;
        }

        Array.Copy(data, _offset + (int)offset, buffer, bufferOffset, toCopy);
        return toCopy;
    }

    public ISparseDataProvider GetSubProvider(long offset, long length)
        => new MemoryDataProvider(data, _offset + (int)offset, (int)length);

    public void Dispose() { }
}
