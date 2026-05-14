using UEPakTool.Core.Archive;
using UEPakTool.Core.Container;
using UEPakTool.Core.Encryption;

namespace UEPakTool.Core.Pak;

public class PakFile : IContainerFile
{
    private readonly Stream _stream;
    private readonly PakExtractor _extractor;
    private readonly bool _leaveOpen;
    private readonly string _filePath;

    public PakFooter Footer { get; }
    public PakIndex Index { get; }

    string IContainerFile.ContainerPath => _filePath;

    byte[] IContainerFile.ExtractEntry(object entryTag) =>
        Extract((PakEntry)entryTag);

    string IContainerFile.GetEntryPath(object entryTag) =>
        ((PakEntry)entryTag).Filename;

    IReadOnlyList<object> IContainerFile.GetLocresEntryTags(string? targetLocale)
    {
        var entries = FindFiles("*.locres").ToList();
        if (!string.IsNullOrEmpty(targetLocale))
        {
            var filtered = entries.Where(e =>
                e.Filename.Contains($"/{targetLocale}/", StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0) entries = filtered;
        }
        return entries.Cast<object>().ToList();
    }

    private PakFile(Stream stream, PakFooter footer, PakIndex index,
        bool leaveOpen, string filePath, FAesKey? aesKey)
    {
        _stream = stream;
        _extractor = new PakExtractor(stream, footer.CompressionMethods, aesKey);
        Footer = footer;
        Index = index;
        _leaveOpen = leaveOpen;
        _filePath = filePath;
    }

    public static PakFile Open(string filePath, FAesKey? aesKey = null)
    {
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Open(stream, filePath, leaveOpen: false, aesKey);
    }

    public static PakFile Open(Stream stream, string filePath = "",
        bool leaveOpen = false, FAesKey? aesKey = null)
    {
        var footer = PakFooter.Read(stream);

        // ──── 阶段1：读取 PrimaryIndex ────
        stream.Seek(footer.IndexOffset, SeekOrigin.Begin);
        byte[] primaryIndexData = new byte[footer.IndexSize];
        stream.ReadExactly(primaryIndexData);

        if (footer.EncryptedIndex)
        {
            if (aesKey == null)
                throw new InvalidOperationException("PAK 索引已加密，需要提供 AES 密钥。");
            primaryIndexData = AesDecryptor.DecryptSafe(primaryIndexData, aesKey);
            if (!AesDecryptor.IsValidIndex(primaryIndexData))
                throw new InvalidDataException("AES 密钥无效：解密后的索引数据无法通过验证。");
        }

        var index = PakIndex.Read(primaryIndexData);

        // ──── 阶段2：读取 DirectoryIndex ────
        stream.Seek(index._directoryIndexOffset, SeekOrigin.Begin);
        byte[] directoryIndexData = new byte[index._directoryIndexSize];
        stream.ReadExactly(directoryIndexData);

        if (footer.EncryptedIndex)
        {
            directoryIndexData = AesDecryptor.DecryptSafe(directoryIndexData, aesKey!);
        }

        using var dirReader = new FArchiveReader(directoryIndexData);
        int directoryCount = dirReader.ReadInt32();

        index.Entries = new List<PakEntry>(index._entryCount);

        for (int d = 0; d < directoryCount; d++)
        {
            string dirName = dirReader.ReadFString();
            int fileCount = dirReader.ReadInt32();

            for (int f = 0; f < fileCount; f++)
            {
                string fileName = dirReader.ReadFString();
                int entryOffset = dirReader.ReadInt32();

                if (entryOffset == int.MinValue) continue; // 已删除

                // 拼接完整路径
                string fullPath;
                if (index.MountPoint.EndsWith('/') && dirName.StartsWith('/'))
                    fullPath = dirName.Length == 1
                        ? $"{index.MountPoint}{fileName}"
                        : $"{index.MountPoint}{dirName[1..]}{fileName}";
                else
                    fullPath = $"{index.MountPoint}{dirName}{fileName}";

                var entry = PakIndex.DecodeEntry(index._encodedEntries, entryOffset, fullPath);
                index.Entries.Add(entry);
            }
        }

        return new PakFile(stream, footer, index, leaveOpen, filePath, aesKey);
    }

    public IEnumerable<PakEntry> FindFiles(string pattern)
    {
        if (pattern == "*" || pattern == "*.*")
            return Index.Entries;

        if (pattern.StartsWith("*."))
        {
            var ext = pattern[1..];
            return Index.Entries.Where(e =>
                e.Filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        return Index.Entries.Where(e =>
            e.Filename.Equals(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public byte[] Extract(PakEntry entry)
    {
        return _extractor.Extract(entry);
    }

    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}
