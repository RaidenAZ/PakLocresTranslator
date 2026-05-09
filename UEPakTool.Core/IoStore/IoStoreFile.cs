using System.Text;
using UEPakTool.Core.Compression;
using UEPakTool.Core.Container;

namespace UEPakTool.Core.IoStore;

/// <summary>
/// IoStore 容器解析器。读取 .utoc (Table of Contents) + .ucas (数据块)。
/// </summary>
public class IoStoreFile : IContainerFile
{
    private FileStream? _ucasStream;
    private bool _disposed;

    public IoStoreTocHeader Header { get; }
    public string TocPath { get; }
    public string CasPath { get; }

    public IoStoreChunkId[] ChunkIds { get; }
    public FIoOffsetAndLength[] OffsetLengths { get; }
    public int[]? PerfectHashSeeds { get; }
    public int[]? ChunksWithoutPerfectHash { get; }
    public IoStoreTocCompressedBlockEntry[] CompressionBlocks { get; }
    public string[] CompressionMethods { get; }
    public int TotalChunks => ChunkIds.Length;

    private readonly ICompressionProvider?[] _providers;

    private IoStoreFile(string tocPath, string casPath,
        IoStoreTocHeader header, IoStoreChunkId[] chunkIds, FIoOffsetAndLength[] offsetLengths,
        int[]? perfectHashSeeds, int[]? chunksWithoutPerfectHash,
        IoStoreTocCompressedBlockEntry[] compressionBlocks, string[] compressionMethods)
    {
        TocPath = tocPath;
        CasPath = casPath;
        Header = header;
        ChunkIds = chunkIds;
        OffsetLengths = offsetLengths;
        PerfectHashSeeds = perfectHashSeeds;
        ChunksWithoutPerfectHash = chunksWithoutPerfectHash;
        CompressionBlocks = compressionBlocks;
        CompressionMethods = compressionMethods;

        _providers = new ICompressionProvider?[compressionMethods.Length];
        for (int i = 0; i < compressionMethods.Length; i++)
        {
            _providers[i] = CompressionFactory.GetProvider(compressionMethods[i]);
        }
    }

    public static IoStoreFile Open(string tocPath)
    {
        _ = CompressionFactory.GetProvider("Zlib"); // ensure factory initialized

        if (!File.Exists(tocPath))
            throw new FileNotFoundException($"TOC file not found: {tocPath}");

        // 推导 .ucas 路径：替换 .utoc → .ucas
        string casPath;
        if (tocPath.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase))
            casPath = Path.ChangeExtension(tocPath, ".ucas");
        else
            casPath = tocPath + ".ucas";

        if (!File.Exists(casPath))
            throw new FileNotFoundException($"CAS file not found: {casPath}");

        // 读取 TOC 文件
        byte[] tocData = File.ReadAllBytes(tocPath);
        var header = IoStoreTocHeader.Read(tocData);

        int pos = IoStoreTocHeader.ExpectedSize;
        int remaining = tocData.Length - pos;

        // 1. ChunkIds
        int chunkIdsSize = (int)header.TocEntryCount * IoStoreChunkId.Size;
        if (pos + chunkIdsSize > tocData.Length)
            throw new InvalidDataException("TOC data truncated at ChunkIds");
        var chunkIds = new IoStoreChunkId[header.TocEntryCount];
        for (int i = 0; i < header.TocEntryCount; i++)
            chunkIds[i] = IoStoreChunkId.Read(tocData, pos + i * IoStoreChunkId.Size);
        pos += chunkIdsSize;

        // 2. FIoOffsetAndLength
        int offsetLengthsSize = (int)header.TocEntryCount * FIoOffsetAndLength.Size;
        if (pos + offsetLengthsSize > tocData.Length)
            throw new InvalidDataException("TOC data truncated at OffsetLengths");
        var offsetLengths = new FIoOffsetAndLength[header.TocEntryCount];
        for (int i = 0; i < header.TocEntryCount; i++)
            offsetLengths[i] = FIoOffsetAndLength.Read(tocData, pos + i * FIoOffsetAndLength.Size);
        pos += offsetLengthsSize;

        // 3. PerfectHashSeeds (v4+)
        int[]? perfectHashSeeds = null;
        if (header.TocChunkPerfectHashSeedsCount > 0)
        {
            perfectHashSeeds = new int[header.TocChunkPerfectHashSeedsCount];
            Buffer.BlockCopy(tocData, pos, perfectHashSeeds, 0, perfectHashSeeds.Length * 4);
            pos += perfectHashSeeds.Length * 4;
        }

        // 4. ChunksWithoutPerfectHash (v5+)
        int[]? chunksWithoutPerfectHash = null;
        if (header.TocChunksWithoutPerfectHashCount > 0)
        {
            chunksWithoutPerfectHash = new int[header.TocChunksWithoutPerfectHashCount];
            Buffer.BlockCopy(tocData, pos, chunksWithoutPerfectHash, 0, chunksWithoutPerfectHash.Length * 4);
            pos += chunksWithoutPerfectHash.Length * 4;
        }

        // 5. CompressionBlocks
        int blockEntrySize = (int)header.TocCompressedBlockEntrySize;
        int blocksSize = (int)header.TocCompressedBlockEntryCount * blockEntrySize;
        if (pos + blocksSize > tocData.Length)
            throw new InvalidDataException("TOC data truncated at CompressionBlocks");
        var compressionBlocks = new IoStoreTocCompressedBlockEntry[header.TocCompressedBlockEntryCount];
        for (int i = 0; i < header.TocCompressedBlockEntryCount; i++)
            compressionBlocks[i] = IoStoreTocCompressedBlockEntry.Read(tocData, pos + i * blockEntrySize);
        pos += blocksSize;

        // 6. Compression method names (fixed-width ANSI strings)
        int nameLen = (int)Math.Max(header.CompressionMethodNameLength, 1);
        string[] compressionMethods = new string[header.CompressionMethodNameCount + 1]; // +1 for index 0 = None
        compressionMethods[0] = "None";
        for (int i = 0; i < header.CompressionMethodNameCount; i++)
        {
            int stringStart = pos + i * nameLen;
            int end = stringStart;
            while (end < tocData.Length && end < stringStart + nameLen && tocData[end] != 0)
                end++;
            compressionMethods[i + 1] = Encoding.ASCII.GetString(tocData, stringStart, end - stringStart).Trim();
        }
        // skip method names block — not needed for further parsing since we use CompressionBlocks only
        // pos += (int)header.CompressionMethodNameCount * nameLen;

        return new IoStoreFile(tocPath, casPath, header, chunkIds, offsetLengths,
            perfectHashSeeds, chunksWithoutPerfectHash, compressionBlocks, compressionMethods);
    }

    /// <summary>
    /// 获取指定类型的所有 chunk 索引列表。
    /// </summary>
    public List<int> GetChunksByType(EIoChunkType type)
    {
        var indices = new List<int>();
        for (int i = 0; i < ChunkIds.Length; i++)
        {
            if (ChunkIds[i].ChunkType == type)
                indices.Add(i);
        }
        return indices;
    }

    /// <summary>
    /// 按 chunk 索引提取解压后的数据。
    /// </summary>
    public byte[] ExtractChunk(int chunkIndex)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(IoStoreFile));

        var ol = OffsetLengths[chunkIndex];
        long logicalOffset = (long)ol.Offset;
        long logicalLength = (long)ol.Length;

        if (logicalLength == 0)
            return [];

        // 找到覆盖此逻辑范围的压缩块
        // 每个 CompressionBlock 覆盖 [blockIndex * BlockSize, (blockIndex+1) * BlockSize) 范围的逻辑空间
        long blockSize = (long)Header.CompressionBlockSize;
        if (blockSize == 0) blockSize = 64 * 1024; // 缺省 64KB

        long firstBlock = logicalOffset / blockSize;
        long lastBlock = (logicalOffset + logicalLength + blockSize - 1) / blockSize - 1;

        if (firstBlock >= CompressionBlocks.Length)
            throw new InvalidDataException($"Chunk {chunkIndex} offset {logicalOffset:X} out of range");

        lastBlock = Math.Min(lastBlock, CompressionBlocks.Length - 1);

        using var result = new MemoryStream();
        long bytesRemaining = logicalLength;

        // 确保 CAS 流已打开
        _ucasStream ??= new FileStream(CasPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        for (long bi = firstBlock; bi <= lastBlock && bytesRemaining > 0; bi++)
        {
            var block = CompressionBlocks[bi];

            // 计算此 block 内要拷贝的起始偏移和长度
            long blockLogicalStart = bi * blockSize;
            long skipInBlock = (bi == firstBlock) ? logicalOffset - blockLogicalStart : 0;
            long takeFromBlock = Math.Min(block.UncompressedSize - skipInBlock, bytesRemaining);

            if (block.CompressionMethodIndex == 0)
            {
                // 未压缩：直接读取
                _ucasStream.Seek(block.Offset + skipInBlock, SeekOrigin.Begin);
                var raw = new byte[takeFromBlock];
                _ucasStream.ReadExactly(raw);
                result.Write(raw);
            }
            else
            {
                // 压缩：读取整个 block → 解压 → 切所需部分
                _ucasStream.Seek(block.Offset, SeekOrigin.Begin);
                var compData = new byte[block.CompressedSize];
                _ucasStream.ReadExactly(compData);

                int providerIndex = block.CompressionMethodIndex - 1; // 0=None, 1+=method index
                if (providerIndex < 0 || providerIndex >= _providers.Length || _providers[providerIndex] == null)
                    throw new InvalidOperationException(
                        $"Compression method '{CompressionMethods[block.CompressionMethodIndex]}' not available.");

                var provider = _providers[providerIndex]!;
                byte[] decompressed = provider.Decompress(compData, block.CompressedSize);

                // 切出所需范围
                result.Write(decompressed, (int)skipInBlock, (int)takeFromBlock);
            }

            bytesRemaining -= takeFromBlock;
        }

        return result.ToArray();
    }

    /// <summary>
    /// 获取指定 chunk 索引的显示名称（用于 UI 展示）。
    /// 对于有 DirectoryIndex 的容器返回文件路径，否则返回 chunk ID 字符串。
    /// </summary>
    public string GetChunkDisplayName(int chunkIndex)
    {
        if (chunkIndex < 0 || chunkIndex >= ChunkIds.Length)
            return "<invalid>";
        var cid = ChunkIds[chunkIndex];
        return $"{cid.Id:X16}";
    }

    // ──── IContainerFile ────

    string IContainerFile.ContainerPath => TocPath;

    byte[] IContainerFile.ExtractEntry(object entryTag) =>
        ExtractChunk((int)entryTag);

    string IContainerFile.GetEntryPath(object entryTag) =>
        GetChunkDisplayName((int)entryTag);

    IReadOnlyList<object> IContainerFile.GetLocresEntryTags(string? targetLocale)
    {
        var indices = GetChunksByType(EIoChunkType.Localization);
        // TODO: filter by targetLocale when directory index is implemented
        return indices.Cast<object>().ToList();
    }

    public override string ToString() => $"{Path.GetFileName(TocPath)} ({TotalChunks} chunks)";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ucasStream?.Dispose();
    }
}

/// <summary>
/// 10 字节 FIoOffsetAndLength：5 字节 offset + 5 字节 length。
/// </summary>
public readonly struct FIoOffsetAndLength
{
    public readonly ulong Offset;
    public readonly ulong Length;

    public const int Size = 10;

    public FIoOffsetAndLength(ulong offset, ulong length)
    {
        Offset = offset;
        Length = length;
    }

    public static FIoOffsetAndLength Read(byte[] data, int offset)
    {
        // 5 bytes offset: MSB at index 0
        ulong off = ((ulong)data[offset] << 32)
                  | ((ulong)data[offset + 1] << 24)
                  | ((ulong)data[offset + 2] << 16)
                  | ((ulong)data[offset + 3] << 8)
                  | data[offset + 4];

        // 5 bytes length: MSB at index 5
        ulong len = ((ulong)data[offset + 5] << 32)
                  | ((ulong)data[offset + 6] << 24)
                  | ((ulong)data[offset + 7] << 16)
                  | ((ulong)data[offset + 8] << 8)
                  | data[offset + 9];

        return new FIoOffsetAndLength(off, len);
    }
}
