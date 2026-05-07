namespace UEPakTool.Core.Container;

/// <summary>
/// 容器文件统一接口 — 屏蔽 PAK 和 IoStore 的差异，
/// 让翻译流程只依赖此接口。
/// </summary>
public interface IContainerFile : IDisposable
{
    /// <summary>容器文件路径（用于 UI 显示）</summary>
    string ContainerPath { get; }

    /// <summary>
    /// 按条目标签提取二进制数据。
    /// PAK: entryTag is PakEntry; IoStore: entryTag is int (chunkIndex).
    /// </summary>
    byte[] ExtractEntry(object entryTag);

    /// <summary>
    /// 获取条目的显示路径。
    /// </summary>
    string GetEntryPath(object entryTag);

    /// <summary>
    /// 获取所有本地化相关的条目标签列表（.locres 文件 / Localization chunk）
    /// </summary>
    IReadOnlyList<object> GetLocresEntryTags(string? targetLocale = null);
}
