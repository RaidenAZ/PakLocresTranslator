using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace UEPakTool.Core;

public static class DebugLog
{
    private static readonly string LogPath = GetLogPath();
    private static readonly StreamWriter _writer;
    private static readonly object _lock = new();

    private static string GetLogPath()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(exeDir))
            return Path.Combine(exeDir, "uepaktool_debug.log");
        return Path.Combine(AppContext.BaseDirectory, "uepaktool_debug.log");
    }

    static DebugLog()
    {
        _writer = new StreamWriter(LogPath, append: false) { AutoFlush = false };
        Write("=== UEPakTool Debug Log Started ===");
    }

    public static void Write(string message,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var src = Path.GetFileNameWithoutExtension(file);
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var msg = $"[{ts}] [{src}:{line}] {message}";
        Debug.WriteLine(msg);
        lock (_lock)
        {
            _writer.WriteLine(msg);
            _writer.Flush(); // 保证崩溃前日志落盘
        }
    }
}
