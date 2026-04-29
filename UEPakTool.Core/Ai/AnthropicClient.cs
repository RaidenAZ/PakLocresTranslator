using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace UEPakTool.Core.Ai;

public class AnthropicClient : IAiClient
{
    private readonly HttpClient _http;

    public AnthropicClient(HttpClient http) => _http = http;

    async Task<List<AiTranslationResult>> IAiClient.TranslateBatchAsync(
        List<(string Key, string Original)> entries,
        AiConfig config,
        List<(string Source, string Target)> glossary,
        string? systemPromptOverride,
        CancellationToken cancellation)
    {
        var systemPrompt = systemPromptOverride ?? PromptBuilder.BuildSystemPrompt(glossary);
        var userPrompt = PromptBuilder.BuildUserPrompt(entries);

        var body = new
        {
            model = config.ModelName,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userPrompt } }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{config.ApiUrl.TrimEnd('/')}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _http.SendAsync(request, cancellation);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellation);
        var content = json.GetProperty("content")[0].GetProperty("text").GetString()!;

        return ResponseParser.Parse(content);
    }
}
