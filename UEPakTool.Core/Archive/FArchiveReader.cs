using System.Text;

namespace UEPakTool.Core.Archive;

public class FArchiveReader : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public FArchiveReader(byte[] data)
        : this(new MemoryStream(data, writable: false), leaveOpen: false) { }

    public FArchiveReader(Stream stream, bool leaveOpen = false)
    {
        _stream = stream;
        _reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        _leaveOpen = leaveOpen;
    }

    public long Position => _stream.Position;
    public long Length => _stream.Length;

    public void Seek(long offset) => _stream.Seek(offset, SeekOrigin.Begin);

    // ──── 基础类型 ────

    public int ReadInt32() => _reader.ReadInt32();
    public uint ReadUInt32() => _reader.ReadUInt32();
    public long ReadInt64() => _reader.ReadInt64();
    public ulong ReadUInt64() => _reader.ReadUInt64();
    public short ReadInt16() => _reader.ReadInt16();
    public ushort ReadUInt16() => _reader.ReadUInt16();
    public byte ReadByte() => _reader.ReadByte();
    public bool ReadBool() => _reader.ReadByte() != 0;
    public float ReadFloat() => _reader.ReadSingle();
    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

    // ──── FString ────

    public string ReadFString()
    {
        int length = ReadInt32();
        if (length == 0) return "";

        if (length > 0)
        {
            // ANSI/ASCII，length 包含 null terminator
            byte[] bytes = _reader.ReadBytes(length);
            int actual = Array.IndexOf<byte>(bytes, 0);
            if (actual >= 0) length = actual;
            return Encoding.UTF8.GetString(bytes, 0, length);
        }
        else
        {
            // UCS-2/UTF-16，取绝对值
            int charCount = -length;
            byte[] bytes = _reader.ReadBytes(charCount * 2);
            return Encoding.Unicode.GetString(bytes, 0, bytes.Length);
        }
    }

    // ──── TArray<T> ────

    public int ReadArrayLength() => ReadInt32();

    public List<string> ReadStringArray()
    {
        int count = ReadArrayLength();
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
            list.Add(ReadFString());
        return list;
    }

    public T[] ReadArray<T>(Func<FArchiveReader, T> elementReader)
    {
        int count = ReadArrayLength();
        var array = new T[count];
        for (int i = 0; i < count; i++)
            array[i] = elementReader(this);
        return array;
    }

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _reader.Dispose();
            _stream.Dispose();
        }
    }
}
