using ZstdSharp;

namespace UEPakTool.Core.Compression;

/// <summary>Zstandard 解压（纯 C#）。</summary>
public class ZstdCompressionProvider : ICompressionProvider
{
    public string MethodName => "Zstd";
    public bool IsAvailable => true;

    public byte[] Decompress(byte[] compressed, int compressedSize)
    {
        using var decompressor = new Decompressor();
        var src = new ReadOnlySpan<byte>(compressed, 0, compressedSize);
        return decompressor.Unwrap(src).ToArray();
    }
}
