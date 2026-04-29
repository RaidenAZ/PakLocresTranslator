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
    private bool _isConnectionOk;

    [ObservableProperty]
    private string _connectionStatus = "";

    [ObservableProperty]
    private string _newGlossarySource = "";

    [ObservableProperty]
    private string _newGlossaryTarget = "";

    public ObservableCollection<GlossaryEntry> GlossaryEntries { get; } = [];

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // 关键属性变化时自动保存
    partial void OnApiUrlChanged(string value) => Save();
    partial void OnApiKeyChanged(string value) => Save();
    partial void OnModelNameChanged(string value) => Save();
    partial void OnApiFormatIndexChanged(int value) => Save();
    partial void OnPromptBeforeOverwriteChanged(bool value) => Save();

    public void Load()
    {
        var config = SettingsService.LoadAiConfig();
        ApiUrl = config.ApiUrl;
        ApiKey = config.ApiKey;
        ModelName = config.ModelName;
        ApiFormatIndex = config.Format == ApiFormat.OpenAI ? 0 : 1;
        PromptBeforeOverwrite = config.PromptBeforeOverwrite;

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
            Format = ApiFormatIndex == 0 ? ApiFormat.OpenAI : ApiFormat.Anthropic,
            PromptBeforeOverwrite = PromptBeforeOverwrite
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
        IsConnectionOk = false;
        ConnectionStatus = "测试中...";

        try
        {
            HttpResponseMessage response;

            if (ApiFormatIndex == 0) // OpenAI
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
            else // Anthropic
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
                IsConnectionOk = true;
                ConnectionStatus = "连接成功";
            }
            else
            {
                var errBody = await response.Content.ReadAsStringAsync();
                ConnectionStatus = $"失败: HTTP {(int)response.StatusCode}";
                await ShowErrorDialog("连接测试失败",
                    $"错误类型: HTTP {(int)response.StatusCode}\n详细信息: {errBody[..Math.Min(200, errBody.Length)]}\n建议: 请检查 API URL、Key 和 Model Name 是否正确。");
            }
        }
        catch (TaskCanceledException)
        {
            ConnectionStatus = "超时";
            await ShowErrorDialog("连接测试超时", "API 请求在 15 秒内未响应。\n建议: 请检查网络连接和 API URL 是否正确。");
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"异常: {ex.Message}";
            await ShowErrorDialog("连接测试异常", $"错误信息: {ex.Message}");
        }
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

    private static async Task ShowErrorDialog(string title, string message)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            System.Windows.MessageBox.Show(message, title,
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        });
    }
}
