using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        var pakFile = PakBrowser.GetPakFile();
        var selected = PakBrowser.GetSelectedEntries();
        if (pakFile == null || selected.Count == 0) return;

        Translation.LoadFromPak(pakFile, selected);
        SelectedNavIndex = 1;
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
