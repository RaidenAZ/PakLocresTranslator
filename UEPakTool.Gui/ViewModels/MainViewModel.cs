using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UEPakTool.Core;
using UEPakTool.Gui.Views;

namespace UEPakTool.Gui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private UserControl _currentPage = null!;

    [ObservableProperty]
    private int _selectedNavIndex;

    public PakBrowserViewModel PakBrowser { get; } = new();
    public TranslationViewModel Translation { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    private readonly PakBrowserPage _pakBrowserPage;
    private readonly TranslationPage _translationPage;
    private readonly SettingsPage _settingsPage;

    public MainViewModel()
    {
        _pakBrowserPage = new PakBrowserPage { DataContext = PakBrowser };
        _translationPage = new TranslationPage { DataContext = Translation };
        _settingsPage = new SettingsPage { DataContext = Settings };

        PakBrowser.LoadToTranslationCommand = new RelayCommand(LoadToTranslation);

        CurrentPage = _pakBrowserPage;
    }

    private void LoadToTranslation()
    {
        try
        {
            var container = PakBrowser.GetContainer();
            var selected = PakBrowser.GetSelectedEntryTags();
            if (container == null || selected.Count == 0) return;

            Translation.LoadFromContainer(container, selected);
            SelectedNavIndex = 1;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"LoadToTranslation EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            System.Windows.MessageBox.Show($"加载翻译失败: {ex}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    partial void OnSelectedNavIndexChanged(int value)
    {
        CurrentPage = value switch
        {
            0 => _pakBrowserPage,
            1 => _translationPage,
            2 => _settingsPage,
            _ => _pakBrowserPage
        };
    }
}
