using UEPakTool.Core.Compression;

namespace UEPakTool.Core.Pak;

public class PakExtractor
{
    private readonly Stream _pakStream;
    private readonly ICompressionProvider?[] _providers; // index 0 → 第一个压缩方法，对应 entry.CompressionMethodIndex=1

    public PakExtractor(Stream pakStream, string compressionMethods)
    {
        _pakStream = pakStream;

        var methodNames = string.IsNullOrWhiteSpace(compressionMethods)
            ? []
            : compressionMethods.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        _providers = new ICompressionProvider?[methodNames.Length];
        for (int i = 0; i < methodNames.Length; i++)
        {
            var provider = CompressionFactory.GetProvider(methodNames[i]);
            if (provider == null)
                throw new NotSupportedException($"不支持的压缩方法: {methodNames[i]}");
            _providers[i] = provider;
        }
    }

    public byte[] Extract(PakEntry entry)
    {
        if (entry.Encrypted)
            throw new InvalidOperationException("不支持加密文件提取");

        if (entry.CompressionMethodIndex == 0)
        {
            _pakStream.Seek(entry.Offset + entry.StructSize, SeekOrigin.Begin);
            byte[] data = new byte[entry.UncompressedSize];
            _pakStream.ReadExactly(data);
            return data;
        }

        return ExtractCompressed(entry);
    }

    private byte[] ExtractCompressed(PakEntry entry)
    {
        var blocks = entry.CompressionBlocks;
        using var result = new MemoryStream((int)entry.UncompressedSize);

        var provider = ResolveProvider(entry.CompressionMethodIndex);

        foreach (var block in blocks)
        {
            int compressedSize = (int)block.Size;
            _pakStream.Seek(block.CompressedStart, SeekOrigin.Begin);
            byte[] blockData = new byte[compressedSize];
            _pakStream.ReadExactly(blockData);

            byte[] decompressed = provider.Decompress(blockData, compressedSize);
            result.Write(decompressed);
        }

        return result.ToArray();
    }

    private ICompressionProvider ResolveProvider(int methodIndex)
    {
        // CompressionMethodIndex 从 1 开始（0 = 未压缩）
        int providerIndex = methodIndex - 1;
        if (providerIndex < 0 || providerIndex >= _providers.Length)
            throw new InvalidOperationException($"无效的压缩方法索引: {methodIndex}");
        var provider = _providers[providerIndex];
        if (provider == null)
            throw new InvalidOperationException($"压缩方法 {methodIndex} 不可用");
        return provider;
    }
}
