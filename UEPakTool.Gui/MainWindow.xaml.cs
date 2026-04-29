using System.Windows;
using UEPakTool.Gui.ViewModels;
using Wpf.Ui.Controls;

namespace UEPakTool.Gui;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Settings.Load();
            }
        };
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not NavigationViewItem item) return;

        // 设置活跃状态指示器
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
