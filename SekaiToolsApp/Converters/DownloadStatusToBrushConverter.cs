using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SekaiToolsApp.ViewModels;

namespace SekaiToolsApp.Converters;

/// <summary>
/// 把 <see cref="DownloadStatus"/> 映射成下载列表条目边框颜色。
/// 颜色与原 WPF DownloadTask 一致（待下载灰 / 进行中蓝 / 完成绿 / 失败粉）。
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
