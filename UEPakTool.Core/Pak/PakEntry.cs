namespace UEPakTool.Core.Pak;

public class PakEntry
{
    public string Filename { get; init; } = "";
    public long Offset { get; init; }
    public long UncompressedSize { get; init; }
    public long CompressedSize { get; init; }
    public int CompressionMethodIndex { get; init; }
    public List<CompressionBlock> CompressionBlocks { get; init; } = [];
    public bool Encrypted { get; init; }
    public uint CompressionBlockSize { get; init; }
    public int StructSize { get; init; }

    public bool IsCompressed => CompressionMethodIndex != 0 && CompressedSize != UncompressedSize;
}

public class CompressionBlock
{
    public long CompressedStart { get; set; }
    public long CompressedEnd { get; set; }
    public long Size => CompressedEnd - CompressedStart;
}
