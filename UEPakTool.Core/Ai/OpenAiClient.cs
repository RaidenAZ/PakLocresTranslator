using System.IO;
using System.Text;
using System.Text.Json;

namespace UEPakTool.Core.Ai;

public class OpenAiClient
{
    private readonly HttpClient _http;

    public OpenAiClient(HttpClient http) => _http = http;

    public async Task<Dictionary<string, string>> TranslateBatchAsync(
        List<(string Key, string Original)> entries,
        string apiUrl,
        string apiKey,
        string modelName,
        ApiFormat format,
        List<(string Source, string Target)> glossary,
        string? systemPromptOverride = null,
        CancellationToken cancellation = default)
    {
        var systemPrompt = PromptBuilder.BuildSystemPrompt(glossary, systemPromptOverride);
        var userPrompt = PromptBuilder.BuildUserPrompt(entries);

        var (endpoint, body) = format == ApiFormat.Anthropic
            ? BuildAnthropicRequest(modelName, systemPrompt, userPrompt)
            : BuildOpenAiRequest(modelName, systemPrompt, userPrompt);

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{apiUrl.TrimEnd('/')}{endpoint}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json")
        };

        if (format == ApiFormat.Anthropic)
        {
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
        else
        {
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        var response = await _http.SendAsync(request, cancellation);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(cancellation);
        LogResponse(raw);

        var content = ExtractContent(raw, format);
        return ParseBatchResponse(content, entries);
    }

    public async Task<string> TranslateSingleAsync(
        string original,
        string apiUrl,
        string apiKey,
        string modelName,
        ApiFormat format,
        CancellationToken cancellation = default)
    {
        var (endpoint, body) = format == ApiFormat.Anthropic
            ? BuildAnthropicSingleRequest(modelName, original)
            : BuildOpenAiSingleRequest(modelName, original);

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{apiUrl.TrimEnd('/')}{endpoint}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json")
        };

        if (format == ApiFormat.Anthropic)
        {
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
        else
        {
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        var response = await _http.SendAsync(request, cancellation);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(cancellation);
        return ExtractContent(raw, format).Trim();
    }

    // ──── OpenAI request builders ────

    private static (string endpoint, object body) BuildOpenAiRequest(
        string model, string system, string user) =>
        ("/chat/completions", new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            }
        });

    private static (string endpoint, object body) BuildOpenAiSingleRequest(
        string model, string original) =>
        ("/chat/completions", new
        {
            model,
            messages = new object[]
            {
                new { role = "user", content = $"翻译为简体中文，只输出译文：\n{original}" }
            }
        });

    // ──── Anthropic request builders ────

    private static (string endpoint, object body) BuildAnthropicRequest(
        string model, string system, string user) =>
        ("/messages", new
        {
            model,
            max_tokens = 4096,
            system,
            messages = new[] { new { role = "user", content = user } }
        });

    private static (string endpoint, object body) BuildAnthropicSingleRequest(
        string model, string original) =>
        ("/messages", new
        {
            model,
            max_tokens = 4096,
            messages = new[] { new { role = "user", content = $"翻译为简体中文，只输出译文：\n{original}" } }
        });

    // ──── Content extraction ────

    private static string ExtractContent(string raw, ApiFormat format)
    {
        using var doc = JsonDocument.Parse(raw);
        return format == ApiFormat.Anthropic
            ? doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString()!
            : doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;
    }

    // ──── Logging ────

    private static void LogResponse(string raw)
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Logs");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(logDir, $"batch-response-{DateTime.Now:HHmmss}.txt"), raw);
        }
        catch { }
    }

    // ──── JSON parsing ────

    private static Dictionary<string, string> ParseBatchResponse(
        string content, List<(string Key, string Original)> entries)
    {
        var result = new Dictionary<string, string>();
        var cleaned = content
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

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
