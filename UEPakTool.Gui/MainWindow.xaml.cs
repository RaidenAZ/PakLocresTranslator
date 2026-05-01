using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using UEPakTool.Gui.ViewModels;
using Wpf.Ui.Controls;

namespace UEPakTool.Gui;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SetAppIcon();
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Settings.Load();
                vm.PropertyChanged += OnMainVmPropertyChanged;
            }
            if (NavView.MenuItems.Count > 0 && NavView.MenuItems[0] is NavigationViewItem first)
                first.IsActive = true;
        };
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SelectedNavIndex)) return;
        if (sender is not MainViewModel vm) return;

        var targetTag = vm.SelectedNavIndex switch
        {
            0 => "pak",
            1 => "translation",
            2 => "settings",
            _ => null
        };
        if (targetTag == null) return;

        // 取消所有项的激活
        foreach (var mi in NavView.MenuItems)
            if (mi is NavigationViewItem ni) ni.IsActive = false;
        foreach (var fi in NavView.FooterMenuItems)
            if (fi is NavigationViewItem ni) ni.IsActive = false;

        // 激活与目标 Tag 匹配的项
        foreach (var mi in NavView.MenuItems)
            if (mi is NavigationViewItem ni && ni.Tag?.ToString() == targetTag)
                ni.IsActive = true;
        foreach (var fi in NavView.FooterMenuItems)
            if (fi is NavigationViewItem ni && ni.Tag?.ToString() == targetTag)
                ni.IsActive = true;
    }

    private void SetAppIcon()
    {
        try
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/Icon128.png"));
        }
        catch { /* ignore */ }
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not NavigationViewItem item) return;

        foreach (var mi in NavView.MenuItems)
            if (mi is NavigationViewItem ni) ni.IsActive = false;
        foreach (var fi in NavView.FooterMenuItems)
            if (fi is NavigationViewItem ni) ni.IsActive = false;
        item.IsActive = true;

        var tag = item.Tag?.ToString() ?? "";
        vm.SelectedNavIndex = tag switch
        {
            "pak" => 0,
            "translation" => 1,
            "settings" => 2,
            _ => vm.SelectedNavIndex
        };
    }
}
