using UEPakTool.Core.Archive;

namespace UEPakTool.Core.Pak;

public class PakIndex
{
    public string MountPoint { get; private set; } = "";
    public List<PakEntry> Entries { get; internal set; } = [];
    public long PathHashSeed { get; private set; }

    public static PakIndex Read(byte[] indexData)
    {
        using var reader = new FArchiveReader(indexData);

        var index = new PakIndex
        {
            MountPoint = reader.ReadFString(),
        };

        int entryCount = reader.ReadInt32();
        index.PathHashSeed = reader.ReadInt64();

        // 注意: ReadBoolean 在 UE4 中序列化为 int32 (4字节)，不是 byte！
        bool hasPathHashIndex = reader.ReadInt32() != 0;

        // 跳过 PathHashIndex 位置信息 (36 bytes): Offset(int64) + Size(int64) + Hash(20)
        reader.ReadBytes(36);

        bool hasDirectoryIndex = reader.ReadInt32() != 0;

        long directoryIndexOffset = reader.ReadInt64();
        long directoryIndexSize = reader.ReadInt64();

        // 跳过 DirectoryIndex Hash (20 bytes)
        reader.ReadBytes(20);

        int encodedPakEntriesSize = reader.ReadInt32();
        if (encodedPakEntriesSize < 0 || encodedPakEntriesSize > indexData.Length)
            throw new InvalidDataException(
                $"EncodedPakEntriesSize 异常: {encodedPakEntriesSize} " +
                $"(MountPoint=\"{index.MountPoint}\", EntryCount={entryCount}, " +
                $"Seed=0x{index.PathHashSeed:X}, HasPathHash={hasPathHashIndex}, " +
                $"HasDirIndex={hasDirectoryIndex}, DirOffset=0x{directoryIndexOffset:X}, " +
                $"DirSize={directoryIndexSize}, Pos={reader.Position})");

        byte[] encodedPakEntries = reader.ReadBytes(encodedPakEntriesSize);

        // 末尾标记
        int trailing = reader.ReadInt32();

        // ──── 读取 DirectoryIndex ────
        // DirectoryIndex 不包含在 indexData 中，需要单独传入
        // 这里暂时返回空，由 PakFile 负责读取和合并
        index.Entries = new List<PakEntry>(entryCount);

        // 存储解码所需的数据，后续在 PakFile 中完成
        index._encodedEntries = encodedPakEntries;
        index._directoryIndexOffset = directoryIndexOffset;
        index._directoryIndexSize = directoryIndexSize;
        index._entryCount = entryCount;

        return index;
    }

    // 内部存储（供 PakFile 完成两阶段解析）
    internal byte[] _encodedEntries = [];
    internal long _directoryIndexOffset;
    internal long _directoryIndexSize;
    internal int _entryCount;

    /// <summary>
    /// 从 EncodedPakEntries 的指定偏移处解码一个 FPakEntry
    /// </summary>
    internal static PakEntry DecodeEntry(byte[] encodedData, int offset, string filename)
    {
        int pos = offset;

        // 读取 bitfield
        uint bitfield = BitConverter.ToUInt32(encodedData, pos);
        pos += 4;

        // 解析 CompressionBlockSize
        uint compressionBlockSize;
        if ((bitfield & 0x3f) == 0x3f)
        {
            compressionBlockSize = BitConverter.ToUInt32(encodedData, pos);
            pos += 4;
        }
        else
        {
            compressionBlockSize = (bitfield & 0x3f) << 11;
        }

        // CompressionMethodIndex (bits 23-28)
        int compressionMethodIndex = (int)((bitfield >> 23) & 0x3f);

        // Offset
        long entryOffset;
        if ((bitfield & (1u << 31)) != 0)
        {
            entryOffset = BitConverter.ToUInt32(encodedData, pos);
            pos += 4;
        }
        else
        {
            entryOffset = BitConverter.ToInt64(encodedData, pos);
            pos += 8;
        }

        // UncompressedSize
        long uncompressedSize;
        if ((bitfield & (1u << 30)) != 0)
        {
            uncompressedSize = BitConverter.ToUInt32(encodedData, pos);
            pos += 4;
        }
        else
        {
            uncompressedSize = BitConverter.ToInt64(encodedData, pos);
            pos += 8;
        }

        // CompressedSize
        long compressedSize;
        if (compressionMethodIndex != 0)
        {
            if ((bitfield & (1u << 29)) != 0)
            {
                compressedSize = BitConverter.ToUInt32(encodedData, pos);
                pos += 4;
            }
            else
            {
                compressedSize = BitConverter.ToInt64(encodedData, pos);
                pos += 8;
            }
        }
        else
        {
            compressedSize = uncompressedSize;
        }

        // Encrypted flag (bit 22)
        bool encrypted = (bitfield & (1u << 22)) != 0;

        // CompressionBlocksCount (bits 6-21)
        int compressionBlocksCount = (int)((bitfield >> 6) & 0xffff);

        // 构建 CompressionBlocks
        var blocks = new List<CompressionBlock>(compressionBlocksCount);

        // 计算 StructSize
        int structSize = 8 * 3 + 4 * 2 + 1 + 20; // = 53
        if (compressionMethodIndex != 0)
            structSize += 4 + compressionBlocksCount * 2 * 8;

        if (compressionBlocksCount == 1 && !encrypted)
        {
            blocks.Add(new CompressionBlock
            {
                CompressedStart = entryOffset + structSize,
                CompressedEnd = entryOffset + structSize + compressedSize
            });
        }
        else if (compressionBlocksCount > 0)
        {
            long blockOffset = entryOffset + structSize;

            for (int i = 0; i < compressionBlocksCount; i++)
            {
                long blockSize = BitConverter.ToUInt32(encodedData, pos);
                pos += 4;
                blocks.Add(new CompressionBlock
                {
                    CompressedStart = blockOffset,
                    CompressedEnd = blockOffset + blockSize
                });
                blockOffset += blockSize;
            }
        }

        return new PakEntry
        {
            Filename = filename,
            Offset = entryOffset,
            UncompressedSize = uncompressedSize,
            CompressedSize = compressedSize,
            CompressionMethodIndex = compressionMethodIndex,
            CompressionBlocks = blocks,
            Encrypted = encrypted,
            CompressionBlockSize = compressionBlockSize,
            StructSize = structSize
        };
    }
}
