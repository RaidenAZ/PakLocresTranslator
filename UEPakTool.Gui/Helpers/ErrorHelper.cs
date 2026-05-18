using System.Windows.Controls;
using System.Windows.Media;
using UEPakTool.Core;
using Wpf.Ui.Controls;

namespace UEPakTool.Gui.Helpers;

public static class ErrorHelper
{
    /// <summary>
    /// 向用户展示 Fluent Design 错误对话框并写入调试日志。
    /// </summary>
    public static void Show(string context, Exception ex)
    {
        DebugLog.Write($"ERROR [{context}]: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new System.Windows.Thickness(0, 8, 0, 16)
        };

        content.Children.Add(new SymbolIcon
        {
            Symbol = SymbolRegular.ErrorCircle24,
            FontSize = 32,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)),
            Margin = new System.Windows.Thickness(0, 0, 16, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Top
        });

        content.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"{context}\n{ex.Message}",
            TextWrapping = System.Windows.TextWrapping.Wrap,
            MaxWidth = 420
        });

        var msgBox = new MessageBox
        {
            Title = "发生错误",
            Content = content,
            CloseButtonText = "确定"
        };
        msgBox.ShowDialogAsync().GetAwaiter().GetResult();
    }

    /// <summary>仅写入调试日志（不弹窗）。</summary>
    public static void Log(string context, Exception ex)
    {
        DebugLog.Write($"WARN [{context}]: {ex.GetType().Name}: {ex.Message}");
    }
}
