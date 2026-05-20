using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SekaiToolsApp.Converters;

/// <summary>
/// 把字符串形式的 <c>avares://...</c> URI 转换为 <see cref="Bitmap"/>，
/// 用于 <c>Image.Source="{Binding SomeUri}"</c> 之类的绑定。
///
/// Avalonia 11 中 <c>Image.Source</c> 期望 <see cref="Avalonia.Media.IImage"/>，
/// XAML 直接赋值字符串时会走 <c>ImageAssetTypeConverter</c> 解析，但 Binding 不会调用该 TypeConverter，
/// 因此需要自己提供一个 <see cref="IValueConverter"/>。
///
/// Bitmap 加载结果按 URI 缓存到一个静态字典，避免每次 RebuildCandidates / 重新可视化时反复读流。
/// </summary>
public sealed class AvaresUriToBitmapConverter : IValueConverter
{
    public static AvaresUriToBitmapConverter Instance { get; } = new();

    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return null;
        if (Cache.TryGetValue(s, out var cached)) return cached;
        try
        {
            using var stream = AssetLoader.Open(new Uri(s));
            var bmp = new Bitmap(stream);
            Cache[s] = bmp;
            return bmp;
        }
        catch
        {
            // 资源加载失败（路径错 / 资源未嵌入），返回 null 让 Image 显示空白而不是崩溃。
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
