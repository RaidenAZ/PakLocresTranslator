using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using UEPakTool.Core.Pak;
using UEPakTool.Gui.Models;

namespace UEPakTool.Gui.ViewModels;

public partial class PakBrowserViewModel : ObservableObject
{
    [ObservableProperty]
    private string _pakPath = "";

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _statusText = "未加载 PAK";

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _locresCount;

    public ObservableCollection<PakEntryNode> LocresNodes { get; } = [];

    private PakFile? _pakFile;
    private List<PakEntry> _locresEntries = [];

    [RelayCommand]
    private void OpenPak()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 PAK 文件",
            Filter = "PAK 文件 (*.pak)|*.pak|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadPak(dialog.FileName);
        }
    }

    private void LoadPak(string path)
    {
        try
        {
            _pakFile?.Dispose();
            _pakFile = PakFile.Open(path);
            PakPath = path;
            IsLoaded = true;
            TotalFiles = _pakFile.Index.Entries.Count;
            _locresEntries = _pakFile.FindFiles("*.locres").ToList();
            LocresCount = _locresEntries.Count;

            // 构建树节点
            LocresNodes.Clear();
            var groups = _locresEntries
                .GroupBy(e =>
                {
                    var parts = e.Filename.Split('/');
                    return parts.Length >= 2 ? parts[^2] : "?";
                });

            foreach (var g in groups)
            {
                var node = new PakEntryNode { Name = g.Key, IsExpanded = true };
                foreach (var e in g)
                {
                    node.Children.Add(new PakEntryNode
                    {
                        Name = e.Filename.Split('/').Last(),
                        FullPath = e.Filename,
                        Entry = e,
                        IsChecked = false,
                        AllNodes = LocresNodes // 用于单选互斥
                    });
                }
                LocresNodes.Add(node);
            }

            StatusText = $"已加载: {Path.GetFileName(path)} — {TotalFiles:N0} 文件, {LocresCount} locres";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
    }

    public List<PakEntry> GetSelectedEntries()
    {
        var selected = new List<PakEntry>();
        foreach (var node in LocresNodes)
        foreach (var child in node.Children)
            if (child.IsChecked && child.Entry != null)
                selected.Add(child.Entry);
        return selected;
    }

    public PakFile? GetPakFile() => _pakFile;

    public IRelayCommand? LoadToTranslationCommand { get; set; }
}

public partial class PakEntryNode : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isChecked;

    public string? FullPath { get; init; }
    public PakEntry? Entry { get; init; }
    public ObservableCollection<PakEntryNode> Children { get; } = [];

    // 父级容器引用，用于实现单选互斥
    public ObservableCollection<PakEntryNode>? AllNodes { get; init; }

    partial void OnIsCheckedChanged(bool value)
    {
        if (!value || Entry == null || AllNodes == null) return;

        // 取消其他所有勾选
        foreach (var group in AllNodes)
        foreach (var child in group.Children)
            if (child != this)
                child.IsChecked = false;
    }
}
