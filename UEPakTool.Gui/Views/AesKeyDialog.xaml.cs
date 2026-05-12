using System.Windows;
using Wpf.Ui.Controls;

namespace UEPakTool.Gui.Views;

public partial class AesKeyDialog : FluentWindow
{
    public string? KeyInput { get; private set; }

    public AesKeyDialog()
    {
        InitializeComponent();
        KeyTextBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var input = KeyTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            System.Windows.MessageBox.Show("请输入 AES 密钥。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        KeyInput = input;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
