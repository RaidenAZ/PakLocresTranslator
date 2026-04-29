namespace UEPakTool.Core.Ai;

public class AiTranslationResult
{
    public string Key { get; init; } = "";
    public string Translated { get; init; } = "";
}

public class AiConfig
{
    public string ApiUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "gpt-4o";
    public ApiFormat Format { get; set; } = ApiFormat.OpenAI;
}

public enum ApiFormat
{
    OpenAI,
    Anthropic
}
