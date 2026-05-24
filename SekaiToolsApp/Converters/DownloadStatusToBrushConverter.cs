using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SekaiToolsApp.ViewModels;

namespace SekaiToolsApp.Converters;

/// <summary>
/// 把 <see cref="DownloadStatus"/> 映射成下载列表条目边框颜色。
/// </summary>
public sealed class DownloadStatusToBrushConverter : IValueConverter
{
    public static readonly DownloadStatusToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DownloadStatus status) return Brushes.Transparent;
        return status switch
        {
            DownloadStatus.Pending => Brushes.LightGray,
            DownloadStatus.Downloading => Brushes.LightBlue,
            DownloadStatus.Done => Brushes.LightGreen,
            DownloadStatus.Failed => Brushes.LightPink,
            _ => Brushes.Transparent,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
