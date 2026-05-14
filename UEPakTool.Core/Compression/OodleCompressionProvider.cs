using OodleDotNet;

namespace UEPakTool.Core.Compression;

/// <summary>
/// Oodle 解压 — 通过 Oodle.NET NuGet 包，自动适配
/// oo2core_9_win64.dll (旧) 和 oodle-data-shared.dll (新)。
/// </summary>
public class OodleCompressionProvider : ICompressionProvider
{
    public string MethodName { get; }
    public bool IsAvailable => _oodle != null;

    private readonly Oodle? _oodle;

    private static readonly string[] KnownDlls =
        ["oodle-data-shared.dll", "oo2core_9_win64.dll", "oo2core_8_win64.dll"];

    public OodleCompressionProvider(string methodName = "Oodle")
    {
        MethodName = methodName;
        _oodle = TryLoad();
        DebugLog.Write($"Oodle: found={(object?)_oodle != null}");
    }

    internal static string? FindDll()
    {
        // 1. EXE 同目录
        string exeDir = AppContext.BaseDirectory;
        foreach (var dll in KnownDlls)
        {
            string p = Path.Combine(exeDir, dll);
            if (File.Exists(p)) { DebugLog.Write($"Oodle: found DLL {p}"); return p; }
        }

        // 2. 用户指定路径
        string? userPath = OodleDllConfig.Path;
        if (!string.IsNullOrEmpty(userPath) && File.Exists(userPath))
        {
            DebugLog.Write($"Oodle: using user path {userPath}");
            return userPath;
        }

        // 3. 游戏目录向上搜索
        foreach (var pakDir in OodleDllConfig.ExtraSearchDirs)
        {
            string? found = SearchUpward(pakDir);
            if (found != null) return found;
        }
        return null;
    }

    private static string? SearchUpward(string dir)
    {
        for (int i = 0; i < 5 && dir != null; i++)
        {
            foreach (var dll in KnownDlls)
            {
                string p = Path.Combine(dir, dll);
                if (File.Exists(p)) { DebugLog.Write($"Oodle: found DLL in game dir {p}"); return p; }
            }
            foreach (var dll in KnownDlls)
            {
                string p = Path.Combine(dir, "Binaries", "Win64", dll);
                if (File.Exists(p)) { DebugLog.Write($"Oodle: found DLL in game binaries {p}"); return p; }
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    private static Oodle? TryLoad()
    {
        string? dllPath = FindDll();
        if (dllPath == null)
        {
            DebugLog.Write("Oodle: DLL not found");
            return null;
        }
        try
        {
            DebugLog.Write($"Oodle: loading {dllPath}");
            return new Oodle(dllPath);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"Oodle: load failed: {ex.Message}");
            return null;
        }
    }

    public byte[] Decompress(byte[] compressed, int compressedSize, int expectedUncompressedSize = 0)
    {
        if (_oodle == null)
            throw new InvalidOperationException("Oodle decompressor not available.");

        var src = compressed.AsSpan(0, compressedSize);

        // 先试精确大小
        if (expectedUncompressedSize > 0)
        {
            var dst = new byte[expectedUncompressedSize];
            nint result = _oodle.Decompress(src, dst);
            if (result > 0)
                return dst.AsSpan(0, (int)result).ToArray();
        }

        // 回退：渐进尝试常见块大小
        int[] sizes = [65536, 131072, 262144, 524288, 1048576];
        foreach (int size in sizes)
        {
            if (size == expectedUncompressedSize) continue;
            var dst = new byte[size];
            nint result = _oodle.Decompress(src, dst);
            if (result > 0 && result <= size)
                return dst.AsSpan(0, (int)result).ToArray();
        }

        throw new InvalidDataException("Oodle decompression failed: none of the tried buffer sizes worked.");
    }
}

public static class OodleDllConfig
{
    public static string? Path { get; set; }
    internal static readonly HashSet<string> ExtraSearchDirs = new(StringComparer.OrdinalIgnoreCase);
    public static void AddSearchDir(string directory)
    {
        if (!string.IsNullOrEmpty(directory))
            ExtraSearchDirs.Add(directory);
    }
}
