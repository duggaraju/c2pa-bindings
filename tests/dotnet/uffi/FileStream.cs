using System.IO;
using uniffi.c2pa;

internal sealed class FileStream : uniffi.c2pa.Stream, IDisposable
{
    private readonly System.IO.Stream _stream;

    public FileStream(string file)
    {
        _stream = File.OpenRead(file);
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    public byte[] ReadStream(ulong length)
    {
        var buffer =new byte[length];
        _stream.Read(buffer);
        return buffer;
    }

    public ulong SeekStream(long pos, SeekMode mode)
    {
        SeekOrigin origin = mode switch 
        {
            SeekMode.Start => SeekOrigin.Begin,
            SeekMode.Current => SeekOrigin.Current,
            SeekMode.End => SeekOrigin.End,
            _ => SeekOrigin.Current
        };
        return (ulong) _stream.Seek(pos, origin);
    }

    public ulong WriteStream(byte[] data)
    {
        throw new NotImplementedException();
    }
}