namespace UEPakTool.Core.Ai;

public interface IAiClient
{
    Task<List<AiTranslationResult>> TranslateBatchAsync(
        List<(string Key, string Original)> entries,
        AiConfig config,
        List<(string Source, string Target)> glossary,
        string? systemPromptOverride = null,
        CancellationToken cancellation = default);
}
