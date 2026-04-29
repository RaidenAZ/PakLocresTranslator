namespace UEPakTool.Core.Pak;

public class PakFooter
{
    public const uint ExpectedMagic = 0x5A6F12E1;
    public const int ExpectedVersion = 11;
    public const int FooterSize = 204;

    public uint Magic { get; private set; }
    public uint Version { get; private set; }
    public long IndexOffset { get; private set; }
    public long IndexSize { get; private set; }
    public byte[] IndexHash { get; private set; } = [];
    public string CompressionMethods { get; private set; } = "";

    public static PakFooter Read(Stream stream)
    {
        if (stream.Length < FooterSize)
            throw new InvalidDataException($"文件太小，不是有效的 v11 PAK（最小 {FooterSize} 字节）");

        stream.Seek(stream.Length - FooterSize, SeekOrigin.Begin);
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

        if (footer.Version != ExpectedVersion)
            throw new InvalidDataException(
                $"不支持的 PAK 版本: v{footer.Version}，本工具仅支持 v{ExpectedVersion}");

        // 160 字节的压缩方法名字符串（零填充）
        byte[] compNameBytes = reader.ReadBytes(160);
        int nullTerm = Array.IndexOf<byte>(compNameBytes, 0);
        footer.CompressionMethods = nullTerm >= 0
            ? System.Text.Encoding.ASCII.GetString(compNameBytes, 0, nullTerm)
            : System.Text.Encoding.ASCII.GetString(compNameBytes);

        return footer;
    }
}
