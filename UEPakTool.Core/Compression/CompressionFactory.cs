namespace UEPakTool.Core.Compression;

/// <summary>
/// 压缩方法注册表。按名称字符串索引 ICompressionProvider。
/// 始终可用：Zlib (Deflate), Zstd, LZ4（纯 C# 实现，无需外部 DLL）。
/// 条件可用：Oodle / Kraken / Mermaid / Selkie / Leviathan（需要 oo2core DLL）。
/// </summary>
public static class CompressionFactory
{
    private static readonly Dictionary<string, ICompressionProvider> _providers
        = new(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;
    private static bool _oodleAttempted;
    private static bool _oodleAvailable;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // 内置：Deflate/Zlib
        _providers["Zlib"] = new DeflateCompressionProvider();

        // 内置：Zstd（纯 C#）
        var zstd = new ZstdCompressionProvider();
        _providers[zstd.MethodName] = zstd;

        // 内置：LZ4（纯 C#）
        var lz4 = new Lz4CompressionProvider();
        _providers[lz4.MethodName] = lz4;
        _providers["LZ4HC"] = lz4;

        TryEnableOodle();
    }

    /// <summary>
    /// 尝试启用 Oodle。可多次调用——当新增搜索路径（如打开新的 PAK 目录）
    /// 后可能找到之前没找到的 DLL。
    /// </summary>
    public static void TryEnableOodle()
    {
        if (_oodleAttempted && _oodleAvailable) return;

        var oodle = new OodleCompressionProvider("Oodle");
        if (oodle.IsAvailable)
        {
            _providers[oodle.MethodName] = oodle;
            _providers["Kraken"] = new OodleCompressionProvider("Kraken");
            _providers["Mermaid"] = new OodleCompressionProvider("Mermaid");
            _providers["Selkie"] = new OodleCompressionProvider("Selkie");
            _providers["Leviathan"] = new OodleCompressionProvider("Leviathan");
            _oodleAvailable = true;
        }
        _oodleAttempted = true;
    }

    /// <summary>按名称获取 provider。未找到返回 null。</summary>
    public static ICompressionProvider? GetProvider(string methodName)
    {
        Initialize();
        _providers.TryGetValue(methodName, out var p);
        return p;
    }

    /// <summary>注册自定义 provider。</summary>
    public static void Register(ICompressionProvider provider)
    {
        _providers[provider.MethodName] = provider;
    }

    /// <summary>检查某方法是否可用。</summary>
    public static bool IsAvailable(string methodName)
    {
        return GetProvider(methodName)?.IsAvailable ?? false;
    }

    /// <summary>Oodle 是否已启用。</summary>
    public static bool OodleAvailable => _oodleAvailable;
}
