namespace UEPakTool.Core.IoStore;

/// <summary>
/// 144 字节 FIoStoreTocHeader。
/// </summary>
public class IoStoreTocHeader
{
    public const int ExpectedSize = 144;
    public static readonly byte[] ExpectedMagic = "-==--==--==--==-"u8.ToArray();

    public byte Version { get; private set; }
    public uint TocHeaderSize { get; private set; }
    public uint TocEntryCount { get; private set; }
    public uint TocCompressedBlockEntryCount { get; private set; }
    public uint TocCompressedBlockEntrySize { get; private set; }
    public uint CompressionMethodNameCount { get; private set; }
    public uint CompressionMethodNameLength { get; private set; }
    public uint CompressionBlockSize { get; private set; }
    public uint DirectoryIndexSize { get; private set; }
    public uint PartitionCount { get; private set; }
    public ulong ContainerId { get; private set; }
    public byte[] EncryptionKeyGuid { get; private set; } = [];
    public byte ContainerFlags { get; private set; }
    public uint TocChunkPerfectHashSeedsCount { get; private set; }
    public ulong PartitionSize { get; private set; }
    public uint TocChunksWithoutPerfectHashCount { get; private set; }

    public bool IsCompressed => (ContainerFlags & 1) != 0;
    public bool IsEncrypted => (ContainerFlags & 2) != 0;
    public bool IsSigned => (ContainerFlags & 4) != 0;
    public bool IsIndexed => (ContainerFlags & 8) != 0;

    public static IoStoreTocHeader Read(byte[] data)
    {
        if (data.Length < ExpectedSize)
            throw new InvalidDataException($"TOC header too small: {data.Length} < {ExpectedSize}");

        var magic = data.AsSpan(0, 16);
        if (!magic.SequenceEqual(ExpectedMagic))
            throw new InvalidDataException($"Invalid TOC magic: {Convert.ToHexString(magic)}");

        var header = new IoStoreTocHeader
        {
            Version = data[16],
            TocHeaderSize = BitConverter.ToUInt32(data, 20),
            TocEntryCount = BitConverter.ToUInt32(data, 24),
            TocCompressedBlockEntryCount = BitConverter.ToUInt32(data, 28),
            TocCompressedBlockEntrySize = BitConverter.ToUInt32(data, 32),
            CompressionMethodNameCount = BitConverter.ToUInt32(data, 36),
            CompressionMethodNameLength = BitConverter.ToUInt32(data, 40),
            CompressionBlockSize = BitConverter.ToUInt32(data, 44),
            DirectoryIndexSize = BitConverter.ToUInt32(data, 48),
            PartitionCount = BitConverter.ToUInt32(data, 52),
            ContainerId = BitConverter.ToUInt64(data, 56),
            EncryptionKeyGuid = data[64..80],
            ContainerFlags = data[80],
            TocChunkPerfectHashSeedsCount = BitConverter.ToUInt32(data, 84),
            PartitionSize = BitConverter.ToUInt64(data, 88),
            TocChunksWithoutPerfectHashCount = BitConverter.ToUInt32(data, 96),
        };

        if (header.TocHeaderSize != ExpectedSize)
            throw new InvalidDataException($"Unexpected TOC header size: {header.TocHeaderSize} != {ExpectedSize}");

        if (header.TocCompressedBlockEntrySize != IoStoreTocCompressedBlockEntry.Size)
            throw new InvalidDataException($"Unexpected TOC compressed block entry size: {header.TocCompressedBlockEntrySize} != {IoStoreTocCompressedBlockEntry.Size}");

        return header;
    }
}
