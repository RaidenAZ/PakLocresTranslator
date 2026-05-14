using UEPakTool.Core.Compression;
using UEPakTool.Core.Encryption;

namespace UEPakTool.Core.Pak;

public class PakExtractor
{
    private readonly Stream _pakStream;
    private readonly ICompressionProvider?[] _providers;
    private readonly FAesKey? _aesKey;

    public PakExtractor(Stream pakStream, string compressionMethods, FAesKey? aesKey = null)
    {
        _pakStream = pakStream;
        _aesKey = aesKey;

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
        if (!entry.Encrypted)
        {
            if (entry.CompressionMethodIndex == 0)
            {
                _pakStream.Seek(entry.Offset + entry.StructSize, SeekOrigin.Begin);
                byte[] data = new byte[entry.UncompressedSize];
                _pakStream.ReadExactly(data);
                return data;
            }
            return ExtractCompressed(entry);
        }

        if (_aesKey == null)
            throw new InvalidOperationException("文件已加密，但未提供 AES 密钥。");

        if (entry.CompressionMethodIndex == 0)
            return ExtractEncryptedRaw(entry);

        return ExtractEncryptedCompressed(entry);
    }

    private byte[] ExtractEncryptedRaw(PakEntry entry)
    {
        long alignedSize = Align(entry.UncompressedSize, AesDecryptor.ALIGN);
        _pakStream.Seek(entry.Offset + entry.StructSize, SeekOrigin.Begin);
        byte[] encrypted = new byte[alignedSize];
        _pakStream.ReadExactly(encrypted);

        byte[] decrypted = AesDecryptor.Decrypt(encrypted, _aesKey!);
        if (decrypted.Length != entry.UncompressedSize)
        {
            var trimmed = new byte[entry.UncompressedSize];
            Buffer.BlockCopy(decrypted, 0, trimmed, 0, (int)entry.UncompressedSize);
            return trimmed;
        }
        return decrypted;
    }

    private byte[] ExtractEncryptedCompressed(PakEntry entry)
    {
        var blocks = entry.CompressionBlocks;
        using var result = new MemoryStream((int)entry.UncompressedSize);
        var provider = ResolveProvider(entry.CompressionMethodIndex);

        for (int bi = 0; bi < blocks.Count; bi++)
        {
            var block = blocks[bi];
            int compressedSize = (int)block.Size;
            int alignedSize = Align(compressedSize, AesDecryptor.ALIGN);

            _pakStream.Seek(block.CompressedStart, SeekOrigin.Begin);
            byte[] encrypted = new byte[alignedSize];
            _pakStream.ReadExactly(encrypted);

            byte[] decrypted = AesDecryptor.Decrypt(encrypted, _aesKey!);

            int expectedSize = 0;
            if (entry.CompressionBlockSize > 0)
            {
                long remaining = entry.UncompressedSize - bi * (long)entry.CompressionBlockSize;
                expectedSize = (int)Math.Min(entry.CompressionBlockSize, remaining);
            }

            byte[] decompressed = provider.Decompress(decrypted, compressedSize, expectedSize);
            result.Write(decompressed);
        }

        return result.ToArray();
    }

    private byte[] ExtractCompressed(PakEntry entry)
    {
        var blocks = entry.CompressionBlocks;
        using var result = new MemoryStream((int)entry.UncompressedSize);
        var provider = ResolveProvider(entry.CompressionMethodIndex);

        for (int bi = 0; bi < blocks.Count; bi++)
        {
            var block = blocks[bi];
            int compressedSize = (int)block.Size;

            int expectedSize = 0;
            if (entry.CompressionBlockSize > 0)
            {
                long remaining = entry.UncompressedSize - bi * (long)entry.CompressionBlockSize;
                expectedSize = (int)Math.Min(entry.CompressionBlockSize, remaining);
            }

            _pakStream.Seek(block.CompressedStart, SeekOrigin.Begin);
            byte[] blockData = new byte[compressedSize];
            _pakStream.ReadExactly(blockData);

            byte[] decompressed = provider.Decompress(blockData, compressedSize, expectedSize);
            result.Write(decompressed);
        }

        return result.ToArray();
    }

    private ICompressionProvider ResolveProvider(int methodIndex)
    {
        int providerIndex = methodIndex - 1;
        if (providerIndex < 0 || providerIndex >= _providers.Length)
            throw new InvalidOperationException($"无效的压缩方法索引: {methodIndex}");
        var provider = _providers[providerIndex];
        if (provider == null)
            throw new InvalidOperationException($"压缩方法 {methodIndex} 不可用");
        return provider;
    }

    private static int Align(long value, int alignment) =>
        (int)((value + alignment - 1) & ~(alignment - 1));
}
