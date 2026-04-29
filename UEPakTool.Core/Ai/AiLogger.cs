using System.IO;
using System.Text;

namespace UEPakTool.Core.Ai;

public static class AiLogger
{
    private static readonly string LogDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Config", "Logs");

    public static StreamWriter OpenLog()
    {
        Directory.CreateDirectory(LogDir);
        var path = Path.Combine(LogDir,
            $"translation-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        return new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true };
    }

    public static void Log(StreamWriter writer, string text)
    {
        writer.Write(text);
    }

    public static void CloseLog(StreamWriter writer, string fullResponse)
    {
        writer.WriteLine();
        writer.WriteLine("=== 完整响应 ===");
        writer.WriteLine(fullResponse);
        writer.Close();
    }
}
