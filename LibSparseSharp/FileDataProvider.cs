namespace LibSparseSharp;

public class FileDataProvider(string filePath, long offset, long length) : ISparseDataProvider
{
    public long Length => length;

    public void WriteTo(Stream stream)
    {
        // Using RandomAccess for efficient reading without seeking
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[1024 * 1024];
        var remaining = length;
        long currentOffset = offset;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = RandomAccess.Read(fs.SafeFileHandle, buffer.AsSpan(0, toRead), currentOffset);
            if (read == 0)
            {
                break;
            }

            stream.Write(buffer, 0, read);
            remaining -= read;
            currentOffset += read;
        }
    }

    public int Read(long inOffset, byte[] buffer, int bufferOffset, int count)
    {
        if (inOffset >= length)
        {
            return 0;
        }

        var toRead = (int)Math.Min(count, length - inOffset);
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return RandomAccess.Read(fs.SafeFileHandle, buffer.AsSpan(bufferOffset, toRead), offset + inOffset);
    }

    public ISparseDataProvider GetSubProvider(long subOffset, long subLength)
        => new FileDataProvider(filePath, offset + subOffset, subLength);

    public void Dispose() { }
}
