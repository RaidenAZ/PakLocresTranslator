using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UEPakTool.Gui.Converters;

public class BoolToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly SolidColorBrush Gray = new(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Green : Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
