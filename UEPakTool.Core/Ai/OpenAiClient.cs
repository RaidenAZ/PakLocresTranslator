using System.IO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace UEPakTool.Core.Ai;

public class OpenAiClient
{
    private readonly HttpClient _http;

    public OpenAiClient(HttpClient http) => _http = http;

    /// <summary>
    /// 批量翻译，返回 key→translated 字典
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateBatchAsync(
        List<(string Key, string Original)> entries,
        AiConfig config,
        List<(string Source, string Target)> glossary,
        string? systemPromptOverride = null,
        CancellationToken cancellation = default)
    {
        var systemPrompt = PromptBuilder.BuildSystemPrompt(glossary, systemPromptOverride);
        var userPrompt = PromptBuilder.BuildUserPrompt(entries);

        var body = new
        {
            model = config.ModelName,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{config.ApiUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

        var response = await _http.SendAsync(request, cancellation);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(cancellation);
        // Write response to log file for debugging
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Logs");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, $"batch-response-{DateTime.Now:HHmmss}.txt"), raw);
        }
        catch { }

        // 先解析 API 响应，提取 choices[0].message.content
        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        return ParseBatchResponse(content, entries);
    }

    /// <summary>
    /// 逐条翻译（fallback 或小批量）
    /// </summary>
    public async Task<string> TranslateSingleAsync(
        string original,
        AiConfig config,
        CancellationToken cancellation = default)
    {
        var body = new
        {
            model = config.ModelName,
            messages = new object[]
            {
                new { role = "user", content = $"翻译为简体中文，只输出译文：\n{original}" }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{config.ApiUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

        var response = await _http.SendAsync(request, cancellation);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellation);
        return json.GetProperty("choices")[0].GetProperty("message")
            .GetProperty("content").GetString()!.Trim();
    }

    private static Dictionary<string, string> ParseBatchResponse(
        string content, List<(string Key, string Original)> entries)
    {
        var result = new Dictionary<string, string>();

        // 去掉 markdown 和空白
        var cleaned = content
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        // 尝试提取 JSON 数组
        var start = cleaned.IndexOf('[');
        var end = cleaned.LastIndexOf(']');
        if (start < 0 || end <= start)
            throw new FormatException($"响应中无 JSON 数组: {cleaned[..Math.Min(200, cleaned.Length)]}");

        var json = cleaned[start..(end + 1)];
        var list = JsonSerializer.Deserialize<List<JsonElement>>(json);
        if (list == null || list.Count == 0)
            throw new FormatException($"JSON 数组为空: {json[..Math.Min(200, json.Length)]}");

        foreach (var item in list)
        {
            if (!item.TryGetProperty("k", out var kEl) ||
                !item.TryGetProperty("t", out var tEl))
                continue;

            // k 可能是数字或字符串
            int idx;
            if (kEl.ValueKind == JsonValueKind.Number)
                idx = kEl.GetInt32();
            else if (!int.TryParse(kEl.GetString() ?? "", out idx))
                continue;

            var translated = tEl.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(translated)) continue;
            if (idx < 0 || idx >= entries.Count) continue;

            result[entries[idx].Key] = translated;
        }

        return result;
    }
}
