using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SekaiToolsApp.Converters;

/// <summary>
/// 把 <c>LineDialogModel.Icon</c> 的相对文件名 (如 "chr_3.png") 拼成
/// <c>avares://SekaiToolsApp/Assets/Characters/...</c> 并加载为 <see cref="Bitmap"/>。
///
/// 复用 AvaresUriToBitmapConverter 的缓存策略，避免每次 list 重新可视化都读流。
/// 空字符串 / 加载失败一律返回 null，让 Image 显示空白而不是崩溃。
/// </summary>
public sealed class IconNameToBitmapConverter : IValueConverter
{
    public static IconNameToBitmapConverter Instance { get; } = new();

    private const string AssetRoot = "avares://SekaiToolsApp/Assets/Characters/";

    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return null;
        if (Cache.TryGetValue(s, out var cached)) return cached;
        try
        {
            using var stream = AssetLoader.Open(new Uri(AssetRoot + s));
            var bmp = new Bitmap(stream);
            Cache[s] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
