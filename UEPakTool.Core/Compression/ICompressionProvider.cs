namespace UEPakTool.Core.Compression;

public interface ICompressionProvider
{
    string MethodName { get; }
    bool IsAvailable { get; }

    /// <summary>解压数据。compressed 是完整压缩块，compressedSize 是有效长度。</summary>
    byte[] Decompress(byte[] compressed, int compressedSize, int expectedUncompressedSize = 0);
}
