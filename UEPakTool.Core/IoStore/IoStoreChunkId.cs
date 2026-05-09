namespace UEPakTool.Core.IoStore;

/// <summary>
/// 12 字节 FIoChunkId：
/// [0..7]  uint64 ChunkId
/// [8..9]  uint16 ChunkIndex（通常为 0）
/// [10]    byte Padding
/// [11]    byte ChunkType (EIoChunkType)
/// </summary>
public readonly struct IoStoreChunkId
{
    public readonly ulong Id;
    public readonly ushort Index;
    public readonly EIoChunkType ChunkType;

    public IoStoreChunkId(ulong id, ushort index, EIoChunkType chunkType)
    {
        Id = id;
        Index = index;
        ChunkType = chunkType;
    }

    public static IoStoreChunkId Read(byte[] data, int offset)
    {
        ulong id = BitConverter.ToUInt64(data, offset);
        ushort index = BitConverter.ToUInt16(data, offset + 8);
        EIoChunkType type = (EIoChunkType)data[offset + 11];
        return new IoStoreChunkId(id, index, type);
    }

    public const int Size = 12;

    public byte[] ToBytes()
    {
        var bytes = new byte[12];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 8), Id);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 2), Index);
        bytes[11] = (byte)ChunkType;
        return bytes;
    }

    public override string ToString() => $"{Id:X16}:{Index}:{ChunkType}";
}
