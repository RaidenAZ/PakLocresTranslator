using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using UEPakTool.Core.Compression;
using UEPakTool.Core.Container;
using UEPakTool.Core.Encryption;
using UEPakTool.Core.IoStore;
using UEPakTool.Core.Pak;
using UEPakTool.Gui.Models;
using UEPakTool.Gui.Views;

namespace UEPakTool.Gui.ViewModels;

public partial class PakBrowserViewModel : ObservableObject
{
    [ObservableProperty]
    private string _pakPath = "";

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _statusText = "未加载容器";

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _locresCount;

    public ObservableCollection<PakEntryNode> LocresNodes { get; } = [];

    private IContainerFile? _container;
    private List<object> _locresEntryTags = [];

    public IRelayCommand? LoadToTranslationCommand { get; set; }

    [RelayCommand]
    private void OpenContainer()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择容器文件",
            Filter = "PAK 文件 (*.pak)|*.pak|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadContainer(dialog.FileName);
        }
    }

    private void LoadContainer(string path)
    {
        try
        {
            _container?.Dispose();
            _locresEntryTags.Clear();

            // 将 PAK/IoStore 所在目录加入 Oodle DLL 搜索路径（游戏通常自带）
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                OodleDllConfig.AddSearchDir(dir);
                CompressionFactory.TryEnableOodle();
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".utoc")
            {
                StatusText = "IoStore 暂不支持，请使用 PAK 文件";
                return;
            }
            LoadPak(path);
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
            MessageBox.Show($"加载容器失败:\n{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadPak(string path)
    {
        // 先读取 PAK 尾部，检查是否需要 AES 密钥
        FAesKey? aesKey = null;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var footer = PakFooter.Read(fs);
            if (footer.EncryptedIndex)
            {
                aesKey = PromptForAesKey();
                if (aesKey == null)
                {
                    StatusText = "已取消：需要 AES 密钥才能加载加密 PAK";
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            // 读取尾部失败，继续尝试直接加载（可能非 v11 PAK）
        }

        var pakFile = PakFile.Open(path, aesKey);
        _container = pakFile;
        PakPath = path;
        IsLoaded = true;
        TotalFiles = pakFile.Index.Entries.Count;
        _locresEntryTags = _container.GetLocresEntryTags().ToList();
        LocresCount = _locresEntryTags.Count;

        BuildPakTree(pakFile, _locresEntryTags.Cast<PakEntry>().ToList());
        StatusText = $"已加载: {Path.GetFileName(path)} — {TotalFiles:N0} 文件, {LocresCount} locres";
    }

    private static FAesKey? PromptForAesKey()
    {
        var dialog = new AesKeyDialog();
        var result = dialog.ShowDialog();
        if (result == true && !string.IsNullOrWhiteSpace(dialog.KeyInput))
        {
            try
            {
                return new FAesKey(dialog.KeyInput);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AES 密钥格式无效: {ex.Message}", "密钥错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }
        return null;
    }

    private void LoadIoStore(string path)
    {
        var ioFile = IoStoreFile.Open(path);
        _container = ioFile;
        PakPath = path;
        IsLoaded = true;
        TotalFiles = ioFile.TotalChunks;
        _locresEntryTags = _container.GetLocresEntryTags().ToList();
        LocresCount = _locresEntryTags.Count;

        BuildIoStoreTree(ioFile, _locresEntryTags);
        StatusText = $"已加载: {Path.GetFileName(path)} — {TotalFiles:N0} chunks, {LocresCount} locres";
    }

    private void BuildPakTree(PakFile pakFile, List<PakEntry> locresEntries)
    {
        LocresNodes.Clear();
        // 按 locale 分组（路径中倒数第二个目录是 locale）
        var groups = locresEntries
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
                    EntryTag = e,
                    IsChecked = false,
                    AllNodes = LocresNodes
                });
            }
            LocresNodes.Add(node);
        }
    }

    private void BuildIoStoreTree(IoStoreFile ioFile, List<object> entryTags)
    {
        LocresNodes.Clear();
        var groupNode = new PakEntryNode { Name = "LocRes Chunks", IsExpanded = true };
        for (int i = 0; i < entryTags.Count; i++)
        {
            int chunkIndex = (int)entryTags[i];
            groupNode.Children.Add(new PakEntryNode
            {
                Name = ioFile.GetChunkDisplayName(chunkIndex) + ".locres",
                FullPath = ioFile.GetChunkDisplayName(chunkIndex),
                EntryTag = entryTags[i],
                IsChecked = false,
                AllNodes = LocresNodes
            });
        }
        LocresNodes.Add(groupNode);
    }

    public List<object> GetSelectedEntryTags()
    {
        var selected = new List<object>();
        foreach (var node in LocresNodes)
        foreach (var child in node.Children)
            if (child.IsChecked && child.EntryTag != null)
                selected.Add(child.EntryTag);
        return selected;
    }

    public IContainerFile? GetContainer() => _container;
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
    public object? EntryTag { get; init; }
    public ObservableCollection<PakEntryNode> Children { get; } = [];

    public ObservableCollection<PakEntryNode>? AllNodes { get; init; }

    partial void OnIsCheckedChanged(bool value)
    {
        if (!value || EntryTag == null || AllNodes == null) return;

        foreach (var group in AllNodes)
        foreach (var child in group.Children)
            if (child != this)
                child.IsChecked = false;
    }
}
