using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SekaiToolsPlatform.ViewModels;

namespace SekaiToolsApp.Converters;

public sealed class DiffPartKindToBrushConverter : IValueConverter
{
    public static DiffPartKindToBrushConverter Instance { get; } = new();

    private static readonly IBrush AddBrush = new SolidColorBrush(Color.FromArgb(0x24, 0x1F, 0x9D, 0x55));
    private static readonly IBrush RemoveBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xDC, 0x26, 0x26));
    private static readonly IBrush SameBrush = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DiffPartKind.Add => AddBrush,
            DiffPartKind.Remove => RemoveBrush,
            _ => SameBrush
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
