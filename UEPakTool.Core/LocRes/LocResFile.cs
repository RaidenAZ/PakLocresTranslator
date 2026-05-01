using UEPakTool.Core.Archive;

namespace UEPakTool.Core.LocRes;

public class LocResFile
{
    // UE4.26 FGuid magic: { 0x7574140E, 0xFC034A67, 0x9D90154A, 0x1B7F37C3 }
    private static readonly byte[] LocResMagic = [
        0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC,
        0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B
    ];

    public enum LocResVersion : byte
    {
        Legacy = 0,
        Compact = 1,
        Optimized_CRC32 = 2,
        Optimized_CityHash64_UTF16 = 3,
    }

    public LocResVersion Version { get; set; } = LocResVersion.Legacy;
    public string Locale { get; set; } = "";
    public List<LocResEntry> Entries { get; set; } = [];

    public static LocResFile Load(byte[] data)
    {
        using var reader = new FArchiveReader(data);
        var locres = new LocResFile();

        // 检查 FGuid magic
        byte[] magic = reader.ReadBytes(16);
        var version = LocResVersion.Legacy;
        if (magic.AsSpan().SequenceEqual(LocResMagic))
        {
            version = (LocResVersion)reader.ReadByte();
        }
        else
        {
            // 无 magic 的遗留格式，回退到开头
            reader.Seek(0);
        }

        locres.Version = version;

        // Compact+ 格式：读取本地化字符串数组
        string[]? localizedStringArray = null;
        if (version >= LocResVersion.Compact)
        {
            long stringArrayOffset = reader.ReadInt64();
            if (stringArrayOffset != -1)
            {
                long savedPos = reader.Position;
                reader.Seek(stringArrayOffset);

                if (version >= LocResVersion.Optimized_CRC32)
                {
                    int strCount = reader.ReadInt32();
                    localizedStringArray = new string[strCount];
                    for (int i = 0; i < strCount; i++)
                    {
                        localizedStringArray[i] = reader.ReadFString();
                        _ = reader.ReadInt32(); // RefCount，忽略
                    }
                }
                else
                {
                    int strCount = reader.ReadInt32();
                    localizedStringArray = new string[strCount];
                    for (int i = 0; i < strCount; i++)
                    {
                        localizedStringArray[i] = reader.ReadFString();
                    }
                }

                reader.Seek(savedPos);
            }
        }

        // Optimized 格式跳过 EntriesCount
        if (version >= LocResVersion.Optimized_CRC32)
        {
            reader.ReadBytes(4); // EntriesCount, 不需用
        }

        // 读取命名空间
        uint namespaceCount = reader.ReadUInt32();
        locres.Entries = new List<LocResEntry>();

        for (int n = 0; n < namespaceCount; n++)
        {
            string ns = ReadTextKey(reader, version);
            uint keyCount = reader.ReadUInt32();

            for (int k = 0; k < keyCount; k++)
            {
                string key = ReadTextKey(reader, version);
                uint sourceHash = reader.ReadUInt32();

                string localized;
                if (version >= LocResVersion.Compact)
                {
                    int strIdx = reader.ReadInt32();
                    localized = (localizedStringArray != null && strIdx >= 0 && strIdx < localizedStringArray.Length)
                        ? localizedStringArray[strIdx] : "";
                }
                else
                {
                    localized = reader.ReadFString();
                }

                locres.Entries.Add(new LocResEntry
                {
                    Namespace = ns,
                    Key = key,
                    SourceStringHash = sourceHash,
                    LocalizedString = localized
                });
            }
        }

        return locres;
    }

    private static string ReadTextKey(FArchiveReader reader, LocResVersion version)
    {
        if (version >= LocResVersion.Optimized_CRC32)
        {
            _ = reader.ReadUInt32(); // StrHash，跳过
        }
        return reader.ReadFString();
    }

    public byte[] Save()
    {
        using var writer = new FArchiveWriter();

        // FGuid magic
        writer.WriteBytes(LocResMagic);
        writer.WriteByte((byte)Version);

        // 收集所有 LocalizedString 并去重
        var stringMap = new Dictionary<string, int>();
        var stringList = new List<string>();
        foreach (var e in Entries)
        {
            if (!stringMap.ContainsKey(e.LocalizedString))
            {
                stringMap[e.LocalizedString] = stringList.Count;
                stringList.Add(e.LocalizedString);
            }
        }

        // Compact+：先占位 localizedStringArrayOffset
        long offsetPos = 0;
        if (Version >= LocResVersion.Compact)
        {
            offsetPos = writer.Position;
            writer.WriteInt64(0); // placeholder, 稍后回填
        }

        // 按 Namespace 分组
        var groups = Entries.GroupBy(e => e.Namespace).ToList();
        if (Version >= LocResVersion.Optimized_CRC32)
        {
            writer.WriteInt32(Entries.Count);
        }
        writer.WriteUInt32((uint)groups.Count);

        foreach (var g in groups)
        {
            WriteTextKey(writer, g.Key, Version);
            writer.WriteUInt32((uint)g.Count());

            foreach (var e in g)
            {
                WriteTextKey(writer, e.Key, Version);
                writer.WriteUInt32(e.SourceStringHash);

                if (Version >= LocResVersion.Compact)
                {
                    int idx = stringMap.GetValueOrDefault(e.LocalizedString, 0);
                    writer.WriteInt32(idx);
                }
                else
                {
                    writer.WriteFString(e.LocalizedString);
                }
            }
        }

        // Compact+：字符串表写到文件末尾
        if (Version >= LocResVersion.Compact)
        {
            // 计算每个 localized string 的实际引用次数
            var refCounts = new Dictionary<string, int>();
            foreach (var e in Entries)
            {
                refCounts.TryGetValue(e.LocalizedString, out int c);
                refCounts[e.LocalizedString] = c + 1;
            }

            long stringArrayOffset = writer.Position;
            writer.WriteInt32(stringList.Count);
            foreach (var s in stringList)
            {
                writer.WriteFString(s);
                refCounts.TryGetValue(s, out int rc);
                writer.WriteInt32(rc);
            }

            // 回填 offset
            long savedPos = writer.Position;
            writer.Position = offsetPos;
            writer.WriteInt64(stringArrayOffset);
            writer.Position = savedPos;
        }

        return writer.ToArray();
    }

    private static void WriteTextKey(FArchiveWriter writer, string value, LocResVersion version)
    {
        if (version >= LocResVersion.Optimized_CRC32)
        {
            writer.WriteUInt32(CityHash.Ue4StrHash(value));
        }
        writer.WriteFString(value);
    }
}
