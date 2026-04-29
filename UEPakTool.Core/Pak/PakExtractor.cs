using System.IO.Compression;

namespace UEPakTool.Core.Pak;

public class PakExtractor
{
    private readonly Stream _pakStream;

    public PakExtractor(Stream pakStream)
    {
        _pakStream = pakStream;
    }

    public byte[] Extract(PakEntry entry)
    {
        if (entry.Encrypted)
            throw new InvalidOperationException("不支持加密文件提取");

        if (!entry.IsCompressed)
        {
            // 未压缩：跳过 StructSize 读取实际数据
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

        foreach (var block in blocks)
        {
            int blockSize = (int)block.Size;

            _pakStream.Seek(block.CompressedStart, SeekOrigin.Begin);
            byte[] blockData = new byte[blockSize];
            _pakStream.ReadExactly(blockData);

            // UE4 PAK 中的 Zlib 压缩数据通常有 0x78 头
            // 也可能没有头（raw deflate），两种情况都处理
            int dataOffset = (blockData.Length >= 2 && blockData[0] == 0x78) ? 2 : 0;
            int dataLen = blockData.Length - dataOffset;

            using var ms = new MemoryStream(blockData, dataOffset, dataLen);
            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            deflate.CopyTo(result);
        }

        return result.ToArray();
    }
}
