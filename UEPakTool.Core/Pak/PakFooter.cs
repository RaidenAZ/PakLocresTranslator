namespace UEPakTool.Core.Pak;

/// <summary>
/// UE PAK 文件版本枚举（与引擎 FPakFileVersion 对应）
/// </summary>
public enum EPakFileVersion
{
    Initial = 1,
    NoTimestamps = 2,
    CompressionEncryption = 3,
    IndexEncryption = 4,
    RelativeChunkOffsets = 5,
    DeleteRecords = 6,
    EncryptionKeyGuid = 7,
    FNameBasedCompressionMethod = 8,
    FrozenIndex = 9,
    PathHashIndex = 10,
    Fnv64BugFix = 11,
    Utf8PakDirectory = 12,
}

public class PakFooter
{
    public const uint ExpectedMagic = 0x5A6F12E1;
    public const int CompressionMethodNameLen = 32;

    // 标准 v11/v12 footer 大小: 新字段(17) + 旧字段(44) + 5*32 压缩方法 = 221
    // 新字段: EncryptionKeyGuid(16) + EncryptedIndex(1) = 17
    // 旧字段: Magic(4) + Version(4) + IndexOffset(8) + IndexSize(8) + IndexHash(20) = 44
    public const int DefaultFooterSize = 17 + 44 + 5 * CompressionMethodNameLen; // = 221

    public uint Magic { get; private set; }
    public EPakFileVersion Version { get; private set; }
    public long IndexOffset { get; private set; }
    public long IndexSize { get; private set; }
    public byte[] IndexHash { get; private set; } = [];
    public string CompressionMethods { get; private set; } = "";
    public bool EncryptedIndex { get; private set; }
    public byte[] EncryptionKeyGuid { get; private set; } = [];

    /// <summary>
    /// 探测 PAK 尾部并解析。支持 v11 (Fnv64BugFix) 和 v12 (Utf8PakDirectory)。
    /// 布局（UE 新字段前置）：EncryptionKeyGuid + EncryptedIndex + Magic + Version + ... + CompressionMethods
    /// </summary>
    public static PakFooter Read(Stream stream)
    {
        if (stream.Length < 44)
            throw new InvalidDataException("文件太小，不是有效的Pak文件");

        // 按优先级尝试不同的 footer 大小（最多 5 个压缩方法，共 221 字节）
        int[] sizesToTry = { 221, 222, 189, 157, 125, 93, 61 };
        // 旧版无前置字段的格式：从 fileEnd-204 开始，Magic 在开头（向后兼容）
        int[] legacySizesToTry = { 204 };

        foreach (int footerSize in sizesToTry)
        {
            if (stream.Length < footerSize) continue;
            var result = TryRead(stream, footerSize, expectNewFields: true);
            if (result != null) return result;
        }

        // 回退到旧版格式（无前置字段）
        foreach (int footerSize in legacySizesToTry)
        {
            if (stream.Length < footerSize) continue;
            var result = TryReadLegacy(stream, footerSize);
            if (result != null) return result;
        }

        throw new InvalidDataException($"无效的Pak魔数，不是支持的Pak文件格式");
    }

    private static PakFooter? TryRead(Stream stream, int footerSize, bool expectNewFields)
    {
        try
        {
            stream.Seek(stream.Length - footerSize, SeekOrigin.Begin);
            using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

            // ──── 新字段（序列化在 Magic 之前）────
            var encKeyGuid = reader.ReadBytes(16);
            bool encIndex = reader.ReadByte() != 0;

            // ──── 旧字段 ────
            uint magic = reader.ReadUInt32();
            if (magic != ExpectedMagic) return null;

            int versionRaw = reader.ReadInt32();
            if (versionRaw < 1 || versionRaw > (int)EPakFileVersion.Utf8PakDirectory) return null;
            var version = (EPakFileVersion)versionRaw;

            // 仅支持 v11 和 v12
            if (version != EPakFileVersion.Fnv64BugFix &&
                version != EPakFileVersion.Utf8PakDirectory)
                return null;

            long indexOffset = reader.ReadInt64();
            long indexSize = reader.ReadInt64();
            byte[] indexHash = reader.ReadBytes(20);

            // ──── 压缩方法名 ────
            // 计算压缩方法名数量
            int remainingAfterBase = footerSize - 61; // 61 = 16+1+4+4+8+8+20
            int numMethods = remainingAfterBase / CompressionMethodNameLen;

            var methodNames = new System.Text.StringBuilder();
            for (int i = 0; i < numMethods; i++)
            {
                byte[] nameBytes = reader.ReadBytes(CompressionMethodNameLen);
                int nullTerm = Array.IndexOf<byte>(nameBytes, (byte)0);
                string name = nullTerm >= 0
                    ? System.Text.Encoding.ASCII.GetString(nameBytes, 0, nullTerm)
                    : System.Text.Encoding.ASCII.GetString(nameBytes);
                if (!string.IsNullOrEmpty(name))
                {
                    if (methodNames.Length > 0) methodNames.Append(',');
                    methodNames.Append(name);
                }
            }

            // 验证 indexOffset/indexSize 的合理性
            if (indexOffset < 0 || indexOffset >= stream.Length) return null;
            if (indexSize <= 0 || indexSize > stream.Length) return null;

            return new PakFooter
            {
                Magic = magic,
                Version = version,
                IndexOffset = indexOffset,
                IndexSize = indexSize,
                IndexHash = indexHash,
                CompressionMethods = methodNames.ToString(),
                EncryptedIndex = encIndex,
                EncryptionKeyGuid = encKeyGuid
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 旧版格式：Magic 直接从 footer 开头开始（无前置的 EncryptionKeyGuid/EncryptedIndex）
    /// </summary>
    private static PakFooter? TryReadLegacy(Stream stream, int footerSize)
    {
        try
        {
            stream.Seek(stream.Length - footerSize, SeekOrigin.Begin);
            using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

            uint magic = reader.ReadUInt32();
            if (magic != ExpectedMagic) return null;

            int versionRaw = reader.ReadInt32();
            if (versionRaw < 1 || versionRaw > (int)EPakFileVersion.Utf8PakDirectory) return null;
            var version = (EPakFileVersion)versionRaw;

            if (version != EPakFileVersion.Fnv64BugFix &&
                version != EPakFileVersion.Utf8PakDirectory)
                return null;

            long indexOffset = reader.ReadInt64();
            long indexSize = reader.ReadInt64();
            byte[] indexHash = reader.ReadBytes(20);

            // 压缩方法名（160 字节）
            int remaining = footerSize - 44; // 44 = 4+4+8+8+20
            byte[] compNameBytes = reader.ReadBytes(remaining);
            int nullTerm = Array.IndexOf<byte>(compNameBytes, (byte)0);
            string compMethods = nullTerm >= 0
                ? System.Text.Encoding.ASCII.GetString(compNameBytes, 0, nullTerm)
                : System.Text.Encoding.ASCII.GetString(compNameBytes);

            return new PakFooter
            {
                Magic = magic,
                Version = version,
                IndexOffset = indexOffset,
                IndexSize = indexSize,
                IndexHash = indexHash,
                CompressionMethods = compMethods,
                EncryptedIndex = false,
                EncryptionKeyGuid = new byte[16]
            };
        }
        catch
        {
            return null;
        }
    }
}
