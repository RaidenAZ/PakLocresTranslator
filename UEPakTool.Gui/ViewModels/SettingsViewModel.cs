using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UEPakTool.Gui.Models;
using UEPakTool.Gui.Services;
using Wpf.Ui.Controls;

namespace UEPakTool.Gui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _apiUrl = "https://api.openai.com/v1";

    [ObservableProperty]
    private string _apiKey = "";

    [ObservableProperty]
    private string _modelName = "gpt-4o";

    [ObservableProperty]
    private int _apiFormatIndex; // 0=OpenAI Chat, 1=OpenAI Responses, 2=Anthropic

    [ObservableProperty]
    private bool _promptBeforeOverwrite;

    [ObservableProperty]
    private int _batchSize = 15;

    [ObservableProperty]
    private string _systemPrompt = "";

    [ObservableProperty]
    private string _oodleDllPath = "";

    [ObservableProperty]
    private string _connectionStatus = "";

    [ObservableProperty]
    private string _connectionDetail = "";

    [ObservableProperty]
    private bool _connectionResultVisible;

    [ObservableProperty]
    private InfoBarSeverity _connectionSeverity = InfoBarSeverity.Success;

    [ObservableProperty]
    private string _newGlossarySource = "";

    [ObservableProperty]
    private string _newGlossaryTarget = "";

    public ObservableCollection<GlossaryEntry> GlossaryEntries { get; } = [];
    public List<int> BatchSizeOptions { get; } = [5, 10, 15, 20, 30, 50];
    public string DefaultSystemPrompt => Core.Ai.PromptBuilder.GetDefaultTemplate();

    private readonly ISettingsService _settings = new SettingsService();
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    [RelayCommand]
    private void ResetPrompt()
    {
        SystemPrompt = "";
    }

    // 关键属性变化时自动保存
    partial void OnApiUrlChanged(string value) => Save();
    partial void OnApiKeyChanged(string value) => Save();
    partial void OnModelNameChanged(string value) => Save();
    partial void OnApiFormatIndexChanged(int value) => Save();
    partial void OnPromptBeforeOverwriteChanged(bool value) => Save();
    partial void OnBatchSizeChanged(int value) => Save();
    partial void OnSystemPromptChanged(string value) => Save();
    partial void OnOodleDllPathChanged(string value)
    {
        UEPakTool.Core.Compression.OodleDllConfig.Path = value;
        Save();
    }

    public void Load()
    {
        var config = _settings.LoadAiConfig();
        ApiUrl = config.ApiUrl;
        ApiKey = config.ApiKey;
        ModelName = config.ModelName;
        ApiFormatIndex = FormatToIndex(config.Format);
        PromptBeforeOverwrite = config.PromptBeforeOverwrite;
        BatchSize = config.BatchSize > 0 ? config.BatchSize : 15;
        SystemPrompt = config.SystemPrompt ?? "";
        OodleDllPath = config.OodleDllPath;
        UEPakTool.Core.Compression.OodleDllConfig.Path = config.OodleDllPath;

        var glossary = _settings.LoadGlossary();
        GlossaryEntries.Clear();
        foreach (var g in glossary) GlossaryEntries.Add(g);

        GlossaryEntries.CollectionChanged += (_, _) => Save();
    }

    public void Save()
    {
        var config = new AiConfig
        {
            ApiUrl = ApiUrl,
            ApiKey = ApiKey,
            ModelName = ModelName,
            Format = IndexToFormat(ApiFormatIndex),
            PromptBeforeOverwrite = PromptBeforeOverwrite,
            BatchSize = BatchSize,
            SystemPrompt = string.IsNullOrWhiteSpace(SystemPrompt) ? null : SystemPrompt,
            OodleDllPath = OodleDllPath
        };
        _settings.SaveAiConfig(config);

        var glossary = GlossaryEntries.Select(g => new GlossaryEntry
        {
            Source = g.Source,
            Target = g.Target,
        }).ToList();
        _settings.SaveGlossary(glossary);
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        ConnectionResultVisible = false;

        try
        {
            HttpResponseMessage response;

            var format = IndexToFormat(ApiFormatIndex);
            if (format == Core.Ai.ApiFormat.OpenAI)
            {
                var body = new
                {
                    model = ModelName,
                    messages = new[] { new { role = "user", content = "Say OK" } },
                    max_tokens = 5
                };
                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
                response = await _http.PostAsync($"{ApiUrl.TrimEnd('/')}/chat/completions", content);
            }
            else if (format == Core.Ai.ApiFormat.OpenAIResponses)
            {
                var body = new
                {
                    model = ModelName,
                    input = "Say OK",
                    max_output_tokens = 16
                };
                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
                response = await _http.PostAsync($"{ApiUrl.TrimEnd('/')}/responses", content);
            }
            else
            {
                var body = new
                {
                    model = ModelName,
                    max_tokens = 5,
                    messages = new[] { new { role = "user", content = "Say OK" } }
                };
                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
                _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                response = await _http.PostAsync($"{ApiUrl.TrimEnd('/')}/messages", content);
            }

            if (response.IsSuccessStatusCode)
            {
                ConnectionStatus = "连接成功";
                ConnectionDetail = $"API 响应正常";
                ConnectionSeverity = InfoBarSeverity.Success;
            }
            else
            {
                ConnectionStatus = $"连接失败";
                ConnectionDetail = $"HTTP {(int)response.StatusCode}";
                ConnectionSeverity = InfoBarSeverity.Error;
            }
        }
        catch (TaskCanceledException)
        {
            ConnectionStatus = "连接超时";
            ConnectionDetail = "请求在 10 秒内未响应，请检查 API URL 或网络";
            ConnectionSeverity = InfoBarSeverity.Error;
        }
        catch (Exception ex)
        {
            ConnectionStatus = "连接异常";
            ConnectionDetail = ex.Message;
            ConnectionSeverity = InfoBarSeverity.Error;
        }

        ConnectionResultVisible = true;
    }


    [RelayCommand]
    private void AddGlossaryEntry()
    {
        if (string.IsNullOrWhiteSpace(NewGlossarySource) || string.IsNullOrWhiteSpace(NewGlossaryTarget))
            return;

        GlossaryEntries.Add(new GlossaryEntry
        {
            Source = NewGlossarySource.Trim(),
            Target = NewGlossaryTarget.Trim(),
        });
        NewGlossarySource = "";
        NewGlossaryTarget = "";
    }

    [RelayCommand]
    private void RemoveGlossaryEntry(GlossaryEntry? entry)
    {
        if (entry != null) GlossaryEntries.Remove(entry);
    }

    private static Core.Ai.ApiFormat IndexToFormat(int index) =>
        index switch
        {
            1 => Core.Ai.ApiFormat.OpenAIResponses,
            2 => Core.Ai.ApiFormat.Anthropic,
            _ => Core.Ai.ApiFormat.OpenAI
        };

    private static int FormatToIndex(Core.Ai.ApiFormat format) =>
        format switch
        {
            Core.Ai.ApiFormat.OpenAIResponses => 1,
            Core.Ai.ApiFormat.Anthropic => 2,
            _ => 0
        };

}
