namespace UEPakTool.Core.IoStore;

/// <summary>
/// IoStore chunk 类型枚举（对应 UE EIoChunkType）。
/// 值来自 UE 引擎源码 IoChunkId.h。
/// </summary>
public enum EIoChunkType : byte
{
    Invalid              = 0,
    ExportBundleData     = 1,
    BulkData             = 2,
    OptionalBulkData     = 3,
    MemoryMappedBulkData = 4,
    LoaderGlobalMeta     = 5,
    LoaderGlobalNames    = 6,
    LoaderGlobalNameHashes = 7,
    ContainerHeader      = 8,
    PackageHeader        = 9,
    PackageData          = 10,
    PackageDataMappings  = 11,
    PackageMap           = 12,
    PackageDataSmall     = 13,
    LoaderGlobalPackageData = 14,
    LoaderInitialLoadMeta = 15,
    /// <summary>本地化资源（locres 文件数据）</summary>
    Localization         = 16,
    ShaderCodeLibrary    = 17,
    ShaderCode           = 18,
    PackageResource      = 19,
    ExternalFile         = 20,
    DerivedData          = 21,
    EditorDerivedData    = 22,
    PackageStoreEntry    = 23,
}
