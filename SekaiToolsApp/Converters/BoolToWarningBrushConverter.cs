using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SekaiToolsApp.Converters;

/// <summary>
/// <c>true → 红, false → 灰</c>。供翻译页 <c>LineDialogModel.TooLong</c> 绑定行数计数颜色用。
/// 颜色取自 Fluent palette，避免硬写黑/红，可被主题覆盖；这里直接给 SystemAccent + Gray。
/// </summary>
public sealed class BoolToWarningBrushConverter : IValueConverter
{
    public static BoolToWarningBrushConverter Instance { get; } = new();

    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x1B, 0x1B));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? WarningBrush : NormalBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
