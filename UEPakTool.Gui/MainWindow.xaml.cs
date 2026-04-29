using System.IO;
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
            }
            // 初始选中第一个导航项
            if (NavView.MenuItems.Count > 0 && NavView.MenuItems[0] is NavigationViewItem first)
                first.IsActive = true;
        };
    }

    private void SetAppIcon()
    {
        try
        {
            var pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
            if (!File.Exists(pngPath)) return;
            Icon = new BitmapImage(new Uri(pngPath));
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
