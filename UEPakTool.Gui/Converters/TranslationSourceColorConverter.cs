using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UEPakTool.Gui.Models;

namespace UEPakTool.Gui.Converters;

public class TranslationSourceColorConverter : IValueConverter
{
    private static readonly SolidColorBrush EmptyBrush = new(Colors.White);
    private static readonly SolidColorBrush AiBrush = new(Color.FromRgb(0xE3, 0xF2, 0xFD));       // 浅蓝
    private static readonly SolidColorBrush ManualBrush = new(Color.FromRgb(0xE8, 0xF5, 0xE9));    // 浅绿
    private static readonly SolidColorBrush EditedBrush = new(Color.FromRgb(0xFF, 0xF3, 0xE0));     // 浅橙

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TranslationSource source)
        {
            return source switch
            {
                TranslationSource.Empty => EmptyBrush,
                TranslationSource.AiGenerated => AiBrush,
                TranslationSource.ManuallyEntered => ManualBrush,
                TranslationSource.ManuallyEdited => EditedBrush,
                _ => EmptyBrush
            };
        }
        return EmptyBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
