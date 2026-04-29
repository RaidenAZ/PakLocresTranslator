using System.Text.Json;
using System.Windows.Controls;
using Microsoft.Win32;
using UEPakTool.Gui.Models;
using UEPakTool.Gui.ViewModels;

namespace UEPakTool.Gui.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void RemoveGlossaryEntry_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is GlossaryEntry entry)
        {
            (DataContext as SettingsViewModel)?.RemoveGlossaryEntryCommand.Execute(entry);
        }
    }

    private void ImportGlossary_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "导入术语表", Filter = "JSON 文件 (*.json)|*.json" };
        if (dialog.ShowDialog() != true || DataContext is not SettingsViewModel vm) return;

        var json = System.IO.File.ReadAllText(dialog.FileName);
        var entries = JsonSerializer.Deserialize<List<GlossaryEntry>>(json);
        if (entries != null)
        {
            vm.GlossaryEntries.Clear();
            foreach (var entry in entries) vm.GlossaryEntries.Add(entry);
        }
    }

    private void ExportGlossary_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "导出术语表", Filter = "JSON 文件 (*.json)|*.json" };
        if (dialog.ShowDialog() != true || DataContext is not SettingsViewModel vm) return;

        var json = JsonSerializer.Serialize(vm.GlossaryEntries, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(dialog.FileName, json);
    }

    private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        (DataContext as SettingsViewModel)?.Save();
    }
}
