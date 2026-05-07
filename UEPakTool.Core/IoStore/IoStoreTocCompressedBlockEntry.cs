namespace UEPakTool.Core.IoStore;

/// <summary>
/// 12 字节 FIoStoreTocCompressedBlockEntry：
/// [0..4][4..7]  40-bit Offset (物理 .ucas 偏移)
/// [4..7] bits 8-31  24-bit CompressedSize
/// [8..11] bits 0-23 24-bit UncompressedSize
/// [8..11] bits 24-31  8-bit CompressionMethodIndex (0=未压缩)
/// </summary>
public readonly struct IoStoreTocCompressedBlockEntry
{
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public long Offset { get; }
    public int CompressedSize { get; }
    public int UncompressedSize { get; }
    public int CompressionMethodIndex { get; }

    public const int Size = 12;

    public IoStoreTocCompressedBlockEntry(long offset, int compressedSize, int uncompressedSize, int methodIndex)
    {
        Offset = offset;
        CompressedSize = compressedSize;
        UncompressedSize = uncompressedSize;
        CompressionMethodIndex = methodIndex;
    }

    public static IoStoreTocCompressedBlockEntry Read(byte[] data, int offset)
    {
        // 40-bit offset: bytes 0-4 as little-endian, masked to 40 bits
        long raw = BitConverter.ToInt64(data, offset);
        long entryOffset = raw & 0xFFFF_FFFF_FFL;

        // 24-bit compressed size: bytes 4-7, shifted right by 8
        uint mid = BitConverter.ToUInt32(data, offset + 4);
        int compSize = (int)((mid >> 8) & 0xFF_FFFF);

        // 24-bit uncompressed size: bytes 8-11, lower 24 bits
        uint high = BitConverter.ToUInt32(data, offset + 8);
        int uncompSize = (int)(high & 0xFF_FFFF);

        // 8-bit method index: byte 11 (top byte of high uint32)
        int methodIndex = (int)(high >> 24) & 0xFF;

        return new IoStoreTocCompressedBlockEntry(entryOffset, compSize, uncompSize, methodIndex);
    }
}
