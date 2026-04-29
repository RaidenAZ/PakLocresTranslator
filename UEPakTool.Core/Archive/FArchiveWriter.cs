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

    // ──── 基础类型 ────

    public void WriteInt32(int value) => _writer.Write(value);
    public void WriteUInt32(uint value) => _writer.Write(value);
    public void WriteInt64(long value) => _writer.Write(value);
    public void WriteUInt64(ulong value) => _writer.Write(value);
    public void WriteInt16(short value) => _writer.Write(value);
    public void WriteUInt16(ushort value) => _writer.Write(value);
    public void WriteByte(byte value) => _writer.Write(value);
    public void WriteBool(bool value) => _writer.Write((byte)(value ? 1 : 0));
    public void WriteFloat(float value) => _writer.Write(value);
    public void WriteBytes(byte[] data) => _writer.Write(data);
    public void WriteBytes(byte[] data, int offset, int count) => _writer.Write(data, offset, count);

    // ──── FString ────

    public void WriteFString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteInt32(0);
            return;
        }

        // 判断编码：全 ASCII → 正长度；含非 ASCII → 负长度 (UCS-2)
        bool isAsciiOnly = value.All(c => c < 128);
        byte[] encoded = isAsciiOnly
            ? Encoding.UTF8.GetBytes(value + '\0')
            : Encoding.Unicode.GetBytes(value + '\0');

        if (isAsciiOnly)
        {
            WriteInt32(encoded.Length); // 正值：含 null terminator
            _writer.Write(encoded);
        }
        else
        {
            WriteInt32(-(value.Length + 1)); // 负值：原字符数（含 null），不含字节乘数
            _writer.Write(encoded);
        }
    }

    // ──── TArray<T> ────

    public void WriteArrayLength(int count) => WriteInt32(count);

    public void WriteStringArray(List<string> list)
    {
        WriteArrayLength(list.Count);
        foreach (var s in list)
            WriteFString(s);
    }

    // ──── 结果 ────

    public byte[] ToArray() => _stream.ToArray();

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}
