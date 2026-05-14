using System.IO.Compression;

namespace UEPakTool.Core.Compression;

/// <summary>Deflate / Zlib 解压。自动处理 zlib 头和 raw deflate。</summary>
public class DeflateCompressionProvider : ICompressionProvider
{
    public string MethodName => "Zlib";
    public bool IsAvailable => true;

    public byte[] Decompress(byte[] compressed, int compressedSize, int _ = 0)
    {
        bool hasZlibHeader = compressed.Length >= 2 && compressed[0] == 0x78;

        int skip = hasZlibHeader ? 2 : 0;
        int actualSize = compressedSize - skip;

        using var compressedStream = new MemoryStream(compressed, skip, actualSize);
        using var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress);
        using var result = new MemoryStream();
        deflate.CopyTo(result);
        return result.ToArray();
    }
}
