using System.Runtime.InteropServices;

namespace UEPakTool.Core.Compression;

/// <summary>
/// Oodle 解压 — P/Invoke oo2core DLL。DLL 随 EXE 捆绑发布。
/// </summary>
public class OodleCompressionProvider : ICompressionProvider
{
    public string MethodName { get; }
    public bool IsAvailable => _decompress != null;

    private readonly OodleLZ_DecompressDelegate? _decompress;

    private enum OodleLZ_FuzzSafe { No = 0, Yes = 1 }
    private enum OodleLZ_CheckCRC { No = 0, Yes = 1 }
    private enum OodleLZ_Verbosity { None = 0 }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long OodleLZ_DecompressDelegate(
        byte[] compressed, long compressedSize,
        byte[] output, long outputSize,
        int fuzzSafe, int checkCrc, int verbosity,
        nint dictBase, long dictSize, nint threadPhase);

    private static readonly string[] KnownDlls =
        ["oo2core_9_win64.dll", "oo2core_8_win64.dll"];

    public OodleCompressionProvider(string methodName = "Oodle")
    {
        MethodName = methodName;
        _decompress = TryLoad();
    }

    internal static string? FindDll()
    {
        // 1. EXE 同目录（捆绑发布）
        string exeDir = AppContext.BaseDirectory;
        foreach (var dll in KnownDlls)
        {
            string p = Path.Combine(exeDir, dll);
            if (File.Exists(p)) return p;
        }

        // 2. 用户指定路径
        string? userPath = OodleDllConfig.Path;
        if (!string.IsNullOrEmpty(userPath) && File.Exists(userPath))
            return userPath;

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
                if (File.Exists(p)) return p;
            }
            foreach (var dll in KnownDlls)
            {
                string p = Path.Combine(dir, "Binaries", "Win64", dll);
                if (File.Exists(p)) return p;
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    private static OodleLZ_DecompressDelegate? TryLoad()
    {
        string? dllPath = FindDll();
        if (dllPath == null) return null;
        try
        {
            var handle = NativeLibrary.Load(dllPath);
            if (NativeLibrary.TryGetExport(handle, "OodleLZ_Decompress", out var addr) && addr != nint.Zero)
                return Marshal.GetDelegateForFunctionPointer<OodleLZ_DecompressDelegate>(addr);
        }
        catch { }
        return null;
    }

    public byte[] Decompress(byte[] compressed, int compressedSize)
    {
        if (_decompress == null)
            throw new InvalidOperationException("Oodle decompressor not available.");

        // 尝试几个常见的 block 大小；UE 通常用 64KB-256KB
        int[] sizes = [65536, 131072, 262144, 524288, 1048576];
        foreach (int size in sizes)
        {
            var output = new byte[size];
            long result = _decompress(compressed, compressedSize, output, size,
                (int)OodleLZ_FuzzSafe.No, (int)OodleLZ_CheckCRC.No,
                (int)OodleLZ_Verbosity.None, nint.Zero, 0, nint.Zero);

            if (result > 0 && result < size)
                return output.AsSpan(0, (int)result).ToArray();
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
