using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UEPakTool.Gui.Models;
using UEPakTool.Gui.Services;

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
    private int _apiFormatIndex; // 0=OpenAI, 1=Anthropic

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
    private System.Windows.Media.Brush _connectionDotColor =
        System.Windows.Media.Brushes.Gray;

    [ObservableProperty]
    private string _newGlossarySource = "";

    [ObservableProperty]
    private string _newGlossaryTarget = "";

    public ObservableCollection<GlossaryEntry> GlossaryEntries { get; } = [];
    public List<int> BatchSizeOptions { get; } = [5, 10, 15, 20, 30, 50];
    public string DefaultSystemPrompt => Core.Ai.PromptBuilder.GetDefaultTemplate();

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

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
        var config = SettingsService.LoadAiConfig();
        ApiUrl = config.ApiUrl;
        ApiKey = config.ApiKey;
        ModelName = config.ModelName;
        ApiFormatIndex = config.Format == Core.Ai.ApiFormat.OpenAI ? 0 : 1;
        PromptBeforeOverwrite = config.PromptBeforeOverwrite;
        BatchSize = config.BatchSize > 0 ? config.BatchSize : 15;
        SystemPrompt = config.SystemPrompt ?? "";
        OodleDllPath = config.OodleDllPath;
        UEPakTool.Core.Compression.OodleDllConfig.Path = config.OodleDllPath;

        var glossary = SettingsService.LoadGlossary();
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
            Format = ApiFormatIndex == 0
                ? Core.Ai.ApiFormat.OpenAI : Core.Ai.ApiFormat.Anthropic,
            PromptBeforeOverwrite = PromptBeforeOverwrite,
            BatchSize = BatchSize,
            SystemPrompt = string.IsNullOrWhiteSpace(SystemPrompt) ? null : SystemPrompt
        };
        SettingsService.SaveAiConfig(config);

        var glossary = GlossaryEntries.Select(g => new GlossaryEntry
        {
            Source = g.Source,
            Target = g.Target,
        }).ToList();
        SettingsService.SaveGlossary(glossary);
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        ConnectionDotColor = System.Windows.Media.Brushes.Gray;
        ConnectionStatus = "测试中...";

        try
        {
            HttpResponseMessage response;

            if (ApiFormatIndex == 0)
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
                ConnectionDotColor = System.Windows.Media.Brushes.Green;
                ConnectionStatus = "连接成功";
            }
            else
            {
                var errBody = await response.Content.ReadAsStringAsync();
                ConnectionDotColor = System.Windows.Media.Brushes.OrangeRed;
                ConnectionStatus = $"失败 HTTP {(int)response.StatusCode}";
                ShowError($"HTTP {(int)response.StatusCode}", errBody);
            }
        }
        catch (TaskCanceledException)
        {
            ConnectionDotColor = System.Windows.Media.Brushes.OrangeRed;
            ConnectionStatus = "超时";
            ShowError("连接超时", "API 请求在 30 秒内未响应");
        }
        catch (Exception ex)
        {
            ConnectionDotColor = System.Windows.Media.Brushes.OrangeRed;
            ConnectionStatus = "异常";
            ShowError("连接异常", ex.Message);
        }
    }

    private static void ShowError(string title, string detail)
    {
        var msg = detail.Length > 500 ? detail[..500] + "..." : detail;
        System.Windows.MessageBox.Show($"错误: {title}\n\n{msg}", "连接测试失败",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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

}
