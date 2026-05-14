using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using UEPakTool.Core;
using UEPakTool.Core.Ai;
using UEPakTool.Core.Container;
using UEPakTool.Core.LocRes;
using UEPakTool.Gui.Models;
using UEPakTool.Gui.Services;

namespace UEPakTool.Gui.ViewModels;

public partial class TranslationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _translatedCount;

    [ObservableProperty]
    private int _matchCount;

    [ObservableProperty]
    private int _currentMatchIndex;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private int _highlightedIndex = -1;

    [ObservableProperty]
    private int _translationProgress;

    public ObservableCollection<TranslationRecord> Records { get; } = [];

    public event Action<TranslationRecord>? RecordSourceChanged;

    private IContainerFile? _container;
    private List<object> _entryTags = [];
    private List<int> _matchIndices = [];
    private CancellationTokenSource? _translateCts;

    partial void OnSearchTextChanged(string value)
    {
        Search(value);
    }

    public void LoadFromContainer(IContainerFile container, List<object> entryTags, string targetLocale = "zh")
    {
        _container = container;
        _entryTags = entryTags;
        Records.Clear();

        // 选第一个条目加载到翻译表格
        object? targetTag = null;
        foreach (var tag in entryTags)
        {
            var path = container.GetEntryPath(tag);
            if (!string.IsNullOrEmpty(targetLocale) && path.Contains($"/{targetLocale}/"))
            {
                targetTag = tag;
                break;
            }
        }
        targetTag ??= entryTags.FirstOrDefault();

        if (targetTag == null) return;

        byte[] data = container.ExtractEntry(targetTag);
        var locres = LocResFile.Load(data);

        foreach (var entry in locres.Entries)
        {
            var record = new TranslationRecord
            {
                Namespace = entry.Namespace,
                Key = entry.Key,
                Original = entry.LocalizedString,
                SourceStringHash = entry.SourceStringHash
            };
            record.SetInitialValue(entry.LocalizedString);
            record.PropertyChanged += OnRecordChanged;
            Records.Add(record);
        }

        UpdateStats();
    }

    private void OnRecordChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TranslationRecord.Translated)
            or nameof(TranslationRecord.Source))
        {
            UpdateStats();

            if (e.PropertyName == nameof(TranslationRecord.Source) && sender is TranslationRecord record)
                RecordSourceChanged?.Invoke(record);
        }
    }

    [RelayCommand]
    private void ExportJson()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出翻译表 (JSON)",
            Filter = "JSON 文件 (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        // 按 namespace 分组，与 FModel 导出格式兼容
        var groups = Records.GroupBy(r => r.Namespace);
        using var doc = new Utf8JsonWriter(File.Create(dialog.FileName), new JsonWriterOptions { Indented = true });
        doc.WriteStartObject();
        foreach (var g in groups)
        {
            doc.WritePropertyName(g.Key);
            doc.WriteStartObject();
            foreach (var r in g)
                doc.WriteString(r.Key, r.Translated);
            doc.WriteEndObject();
        }
        doc.WriteEndObject();

        StatusText = $"已导出 {Records.Count} 条到 {Path.GetFileName(dialog.FileName)}";
    }

    [RelayCommand]
    private void ImportJson()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入翻译表 (JSON)",
            Filter = "JSON 文件 (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        int count = ImportJsonFile(dialog.FileName);
        UpdateStats();
        StatusText = $"已导入 {count} 条翻译";
    }

    private int ImportJsonFile(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        int count = 0;
        bool hasRecords = Records.Count > 0;

        foreach (var nsProp in doc.RootElement.EnumerateObject())
        {
            string ns = nsProp.Name;
            foreach (var keyProp in nsProp.Value.EnumerateObject())
            {
                string key = keyProp.Name;
                string value = keyProp.Value.GetString() ?? "";

                if (hasRecords)
                {
                    // 合并模式：匹配已有记录
                    var record = Records.FirstOrDefault(r =>
                        r.Namespace == ns && r.Key == key)
                        ?? Records.FirstOrDefault(r => r.Key == key);

                    if (record != null && !string.IsNullOrWhiteSpace(value))
                    {
                        record.Translated = value;
                        record.Source = TranslationSource.ManuallyEntered;
                        count++;
                    }
                }
                else
                {
                    // 独立模式：从 JSON 直接创建记录
                    var record = new TranslationRecord
                    {
                        Namespace = ns,
                        Key = key,
                        Original = value,
                        SourceStringHash = 0
                    };
                    record.SetInitialValue(value);
                    record.PropertyChanged += OnRecordChanged;
                    Records.Add(record);
                    count++;
                }
            }
        }

        return count;
    }

    public void Search(string text)
    {
        _matchIndices.Clear();
        CurrentMatchIndex = 0;
        HighlightedIndex = -1;

        if (string.IsNullOrWhiteSpace(text))
        {
            MatchCount = 0;
            return;
        }

        for (int i = 0; i < Records.Count; i++)
        {
            if (Records[i].DisplayKey.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                Records[i].Original.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (Records[i].Translated?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                _matchIndices.Add(i);
            }
        }

        MatchCount = _matchIndices.Count;
        if (_matchIndices.Count > 0)
        {
            CurrentMatchIndex = 1;
            HighlightedIndex = _matchIndices[0];
        }
    }

    public int? GetNextMatchIndex()
    {
        if (_matchIndices.Count == 0) return null;
        CurrentMatchIndex = CurrentMatchIndex >= _matchIndices.Count ? 1 : CurrentMatchIndex + 1;
        HighlightedIndex = _matchIndices[CurrentMatchIndex - 1];
        return HighlightedIndex;
    }

    public int? GetPrevMatchIndex()
    {
        if (_matchIndices.Count == 0) return null;
        CurrentMatchIndex = CurrentMatchIndex <= 1 ? _matchIndices.Count : CurrentMatchIndex - 1;
        HighlightedIndex = _matchIndices[CurrentMatchIndex - 1];
        return HighlightedIndex;
    }

    [RelayCommand]
    private async Task TranslateAll()
    {
        if (IsTranslating) return;

        IsTranslating = true;
        TranslationProgress = 0;
        StatusText = "准备翻译...";

        try
        {
            _translateCts = new CancellationTokenSource();
            var config = SettingsService.LoadAiConfig();

            if (string.IsNullOrWhiteSpace(config.ApiUrl))
            {
                MessageBox.Show("请先在设置页面配置 API URL。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var glossary = SettingsService.LoadGlossary()
                .Select(g => (g.Source, g.Target)).ToList();

            var aiConfig = new Core.Ai.AiConfig
            {
                ApiUrl = config.ApiUrl,
                ApiKey = config.ApiKey,
                ModelName = config.ModelName,
                Format = config.Format == Gui.Models.ApiFormat.OpenAI
                    ? Core.Ai.ApiFormat.OpenAI : Core.Ai.ApiFormat.Anthropic
            };

            // 筛选要翻译的条目
            var toTranslate = new List<(string Key, string Original)>();
            foreach (var r in Records)
            {
                if (r.Source is TranslationSource.ManuallyEntered or TranslationSource.ManuallyEdited)
                    continue;
                if (r.Source == TranslationSource.AiGenerated && config.PromptBeforeOverwrite)
                    continue;
                toTranslate.Add((r.DisplayKey, r.Original));
            }

            if (toTranslate.Count == 0)
            {
                StatusText = "没有需要翻译的条目";
                return;
            }

            var dispatcher = Application.Current.Dispatcher;
            var token = _translateCts.Token;
            const int batchSize = 15;

            var done = await Task.Run(async () =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                var client = new Core.Ai.OpenAiClient(http);
                int count = 0;

                for (int batchStart = 0; batchStart < toTranslate.Count; batchStart += batchSize)
                {
                    token.ThrowIfCancellationRequested();

                    var batch = toTranslate.Skip(batchStart).Take(batchSize).ToList();
                    var batchNum = batchStart / batchSize + 1;
                    var totalBatches = (toTranslate.Count + batchSize - 1) / batchSize;

                    dispatcher.Invoke(() =>
                    {
                        StatusText = $"翻译中... 第{batchNum}/{totalBatches}批 ({batchStart + 1}-{batchStart + batch.Count}/{toTranslate.Count})";
                        TranslationProgress = (int)(batchStart * 100.0 / toTranslate.Count);
                    });

                    Dictionary<string, string> batchResults;
                    try
                    {
                        batchResults = await client.TranslateBatchAsync(
                            batch, aiConfig, glossary, token);
                    }
                    catch (FormatException fe)
                    {
                        // JSON 解析失败，降级为逐条
                        System.Diagnostics.Debug.WriteLine($"Batch parse failed: {fe.Message}");
                        batchResults = new Dictionary<string, string>();
                        foreach (var (k, o) in batch)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                var t = await client.TranslateSingleAsync(o, aiConfig, token);
                                t = GlossaryPostProcessor.ApplySingle(t, glossary);
                                if (!string.IsNullOrWhiteSpace(t)) batchResults[k] = t;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Single fallback failed: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Batch HTTP error: {ex.Message}");
                        batchResults = new Dictionary<string, string>();
                    }

                    // 后处理 + 写回
                    foreach (var kv in batchResults)
                    {
                        var translated = GlossaryPostProcessor.ApplySingle(kv.Value, glossary);
                        if (string.IsNullOrWhiteSpace(translated)) continue;

                        dispatcher.Invoke(() =>
                        {
                            var record = Records.FirstOrDefault(r => r.DisplayKey == kv.Key);
                            if (record != null)
                            {
                                record.Translated = translated;
                                record.Source = TranslationSource.AiGenerated;
                            }
                        });
                        count++;
                    }

                    var progressDone = Math.Min(batchStart + batchSize, toTranslate.Count);
                    dispatcher.Invoke(() =>
                    {
                        TranslationProgress = (int)(progressDone * 100.0 / toTranslate.Count);
                    });
                }

                return count;
            }, token);

            dispatcher.Invoke(() =>
            {
                StatusText = $"翻译完成：{done} 条";
            });
        }
        catch (OperationCanceledException)
        {
            StatusText = "翻译已取消";
        }
        catch (HttpRequestException ex)
        {
            StatusText = $"网络错误: {ex.Message}";
            MessageBox.Show($"API 请求失败：{ex.Message}", "翻译错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            StatusText = $"翻译异常: {ex.Message}";
            MessageBox.Show($"翻译过程出错：{ex.Message}", "翻译错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsTranslating = false;
            _translateCts?.Dispose();
            _translateCts = null;
        }
    }

    [RelayCommand]
    private void CancelTranslate()
    {
        _translateCts?.Cancel();
    }

    [RelayCommand]
    private void SaveLocresFiles()
    {
        if (Records.Count == 0) return;

        // 独立模式：CSV/JSON 导入，无容器
        if (_container == null)
        {
            SaveStandaloneLocres();
            return;
        }

        // 容器模式：PAK/IoStore 加载
        var dialog = new SaveFileDialog
        {
            Title = "选择输出目录（随便输入文件名，选中后点保存）",
            Filter = "所有文件|*.*",
            FileName = "choose_this_folder"
        };
        if (dialog.ShowDialog() != true) return;
        string outputDir = Path.GetDirectoryName(dialog.FileName)!;

        try
        {
            int count = 0;

            foreach (var tag in _entryTags)
            {
                var entryPath = _container.GetEntryPath(tag);

                byte[] originalData = _container.ExtractEntry(tag);
                var locres = LocResFile.Load(originalData);

                bool modified = false;
                foreach (var e in locres.Entries)
                {
                    var record = Records.FirstOrDefault(r =>
                        r.Namespace == e.Namespace && r.Key == e.Key);
                    if (record != null && record.Translated != e.LocalizedString)
                    {
                        e.LocalizedString = record.Translated;
                        modified = true;
                    }
                }

                if (!modified) continue;

                byte[] newData = locres.Save();

                string fileName = entryPath.Contains('/')
                    ? entryPath.Split('/').Last()
                    : entryPath + ".locres";

                string outPath = Path.Combine(outputDir, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.WriteAllBytes(outPath, newData);
                count++;
            }

            StatusText = $"已保存 {count} 个 locres 到 {outputDir}";
            MessageBox.Show($"成功保存 {count} 个 locres 文件。\n输出目录: {outputDir}", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveStandaloneLocres()
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存 locres 文件",
            Filter = "locres 文件 (*.locres)|*.locres",
            FileName = "Game.locres"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var locres = new LocResFile
            {
                Version = LocResFile.LocResVersion.Optimized_CityHash64_UTF16,
                Entries = Records.Select(r => new LocResEntry
                {
                    Namespace = r.Namespace,
                    Key = r.Key,
                    LocalizedString = r.Translated,
                    SourceStringHash = r.SourceStringHash
                }).ToList()
            };

            byte[] data = locres.Save();
            File.WriteAllBytes(dialog.FileName, data);
            StatusText = $"已保存 {Path.GetFileName(dialog.FileName)}";

            MessageBox.Show($"已保存 {locres.Entries.Count} 条翻译。\n{dialog.FileName}", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateStats()
    {
        TotalCount = Records.Count;
        TranslatedCount = Records.Count(r => r.IsTranslated);
        StatusText = $"共 {TotalCount} 条 | 已翻译 {TranslatedCount} | 待翻译 {TotalCount - TranslatedCount}";
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        int start = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            else if (line[i] == ',' && !inQuotes)
            {
                result.Add(line[start..i].Trim('"'));
                start = i + 1;
            }
        }
        result.Add(line[start..].Trim('"'));
        return result.ToArray();
    }
}
