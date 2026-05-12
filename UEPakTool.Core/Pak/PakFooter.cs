namespace UEPakTool.Core.Pak;

public class PakFooter
{
    public const uint ExpectedMagic = 0x5A6F12E1;
    public const int FooterSize = 232;  // v11: 204 + 16 GUID + 12 reserved

    public uint Magic { get; private set; }
    public uint Version { get; private set; }          // 完整 Version 字段（含标志位）
    public long IndexOffset { get; private set; }
    public long IndexSize { get; private set; }
    public byte[] IndexHash { get; private set; } = [];
    public string CompressionMethods { get; private set; } = "";
    public bool EncryptedIndex { get; private set; }
    public byte[] EncryptionKeyGuid { get; private set; } = [];

    public static PakFooter Read(Stream stream)
    {
        // 最少需要 204 字节（旧版无加密字段的 v11 footer）
        const int minFooterSize = 204;
        if (stream.Length < minFooterSize)
            throw new InvalidDataException($"文件太小，不是有效的 v11 PAK（最小 {minFooterSize} 字节）");

        // 优先按新大小读取，如果文件不够长则按旧大小
        int readSize = stream.Length >= FooterSize ? FooterSize : minFooterSize;
        stream.Seek(stream.Length - readSize, SeekOrigin.Begin);
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        var footer = new PakFooter
        {
            Magic = reader.ReadUInt32(),
            Version = reader.ReadUInt32(),
            IndexOffset = reader.ReadInt64(),
            IndexSize = reader.ReadInt64(),
            IndexHash = reader.ReadBytes(20),
        };

        if (footer.Magic != ExpectedMagic)
            throw new InvalidDataException(
                $"无效的 PAK 魔数: 0x{footer.Magic:X8}，期望 0x{ExpectedMagic:X8}");

        // EncryptedIndex 标志在 Version 字段的高位
        footer.EncryptedIndex = (footer.Version & 0x80000000) != 0;

        uint cleanVersion = footer.Version & 0x7FFFFFFF;
        if (cleanVersion != 11)
            throw new InvalidDataException(
                $"不支持的 PAK 版本: v{cleanVersion}，本工具仅支持 v11");

        // 160 字节的压缩方法名字符串（零填充）
        byte[] compNameBytes = reader.ReadBytes(160);
        int nullTerm = Array.IndexOf<byte>(compNameBytes, 0);
        footer.CompressionMethods = nullTerm >= 0
            ? System.Text.Encoding.ASCII.GetString(compNameBytes, 0, nullTerm)
            : System.Text.Encoding.ASCII.GetString(compNameBytes);

        // 读取 EncryptionKeyGuid（如果可用）
        if (readSize >= FooterSize)
        {
            footer.EncryptionKeyGuid = reader.ReadBytes(16);
        }

        return footer;
    }
}
