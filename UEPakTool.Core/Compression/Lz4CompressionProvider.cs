using K4os.Compression.LZ4;

namespace UEPakTool.Core.Compression;

/// <summary>LZ4 解压（纯 C#）。</summary>
public class Lz4CompressionProvider : ICompressionProvider
{
    public string MethodName => "LZ4";
    public bool IsAvailable => true;

    public byte[] Decompress(byte[] compressed, int compressedSize)
    {
        // LZ4 最大解压后大小约为压缩大小的 255 倍 + 16，但实际通常不超过几 MB
        int maxDecompressed = compressedSize * 2 + 256;
        byte[] output = new byte[maxDecompressed];

        int decoded = LZ4Codec.Decode(compressed, 0, compressedSize, output, 0, maxDecompressed);
        if (decoded <= 0)
            throw new InvalidDataException("LZ4 decompression failed.");

        // 如果刚好填满缓冲区，可能是被截断了，用更大的缓冲区重试
        if (decoded == maxDecompressed)
        {
            maxDecompressed = compressedSize * 255 + 16;
            output = new byte[maxDecompressed];
            decoded = LZ4Codec.Decode(compressed, 0, compressedSize, output, 0, maxDecompressed);
        }

        if (decoded == output.Length)
            return output;
        return output.AsSpan(0, decoded).ToArray();
    }
}
