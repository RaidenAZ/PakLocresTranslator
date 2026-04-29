using System.Text.Json;

namespace UEPakTool.Core.Ai;

public static class ResponseParser
{
    public static List<AiTranslationResult> Parse(string content)
    {
        // 尝试从响应中提取 JSON 数组
        content = content.Trim();
        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');

        if (start < 0 || end < start)
            throw new FormatException($"无法在 AI 响应中找到 JSON 数组: {content[..Math.Min(200, content.Length)]}");

        var json = content[start..(end + 1)];
        var results = JsonSerializer.Deserialize<List<JsonElement>>(json)
            ?? throw new FormatException("JSON 数组解析为 null");

        return results.Select(e => new AiTranslationResult
        {
            Key = e.GetProperty("key").GetString() ?? "",
            Translated = e.GetProperty("translated").GetString() ?? ""
        }).ToList();
    }
}
