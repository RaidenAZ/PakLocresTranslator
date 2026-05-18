using System.Text;

namespace UEPakTool.Core.Archive;

public class FArchiveWriter : IDisposable
{
    private readonly BinaryWriter _writer;
    private readonly MemoryStream _stream;

    public FArchiveWriter()
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
    }

    public long Position
    {
        get => _stream.Position;
        set => _stream.Seek(value, SeekOrigin.Begin);
    }

    public void WriteInt32(int value) => _writer.Write(value);
    public void WriteUInt32(uint value) => _writer.Write(value);
    public void WriteInt64(long value) => _writer.Write(value);
    public void WriteByte(byte value) => _writer.Write(value);
    public void WriteBytes(byte[] data) => _writer.Write(data);

    public void WriteFString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteInt32(0);
            return;
        }

        bool isAsciiOnly = value.All(c => c < 128);
        byte[] encoded = isAsciiOnly
            ? Encoding.UTF8.GetBytes(value + '\0')
            : Encoding.Unicode.GetBytes(value + '\0');

        if (isAsciiOnly)
        {
            WriteInt32(encoded.Length);
            _writer.Write(encoded);
        }
        else
        {
            WriteInt32(-(value.Length + 1));
            _writer.Write(encoded);
        }
    }

    public byte[] ToArray() => _stream.ToArray();

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}
