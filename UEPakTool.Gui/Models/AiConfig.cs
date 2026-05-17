namespace UEPakTool.Gui.Models;

public class AiConfig
{
    public string ApiUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "gpt-4o";
    public ApiFormat Format { get; set; } = ApiFormat.OpenAI;
    public bool PromptBeforeOverwrite { get; set; }
    public int BatchSize { get; set; } = 15;
    public string? SystemPrompt { get; set; }
    public string OodleDllPath { get; set; } = "";
}

public enum ApiFormat
{
    OpenAI,
    Anthropic
}
