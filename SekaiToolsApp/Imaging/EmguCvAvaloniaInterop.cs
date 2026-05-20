using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsApp.Imaging;

/// <summary>
/// 把 Emgu.CV 的 <see cref="Mat"/> 渡到 Avalonia 的位图类型上，跨平台替代原 WPF
/// 的 <c>Mat.ToBitmapSource()</c>。
///
/// 单次显示请用 <see cref="ToAvaloniaBitmap"/>；视频/帧预览的高频场景请预先创建
/// <see cref="WriteableBitmap"/>，用 <see cref="WriteTo"/> 反复写入避免重复分配。
/// </summary>
public static class EmguCvAvaloniaInterop
{
    private static readonly Vector DefaultDpi = new(96, 96);

    /// <summary>
    /// 拷贝 <paramref name="source"/> 像素到一张新的 Avalonia <see cref="Bitmap"/>。
    /// 返回的 Bitmap 与原 Mat 解耦，可以在 Mat 释放后继续使用。
    /// </summary>
    public static Bitmap ToAvaloniaBitmap(Mat source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (source.IsEmpty) throw new ArgumentException("Mat is empty.", nameof(source));

        using var bgra = EnsureBgra(source);
        var size = new PixelSize(bgra.Width, bgra.Height);
        return new Bitmap(
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul,
            bgra.DataPointer,
            size,
            DefaultDpi,
            bgra.Step);
    }

    /// <summary>
    /// 把 <paramref name="source"/> 像素写入已有的 <see cref="WriteableBitmap"/>。
    /// 要求 <paramref name="target"/> 的尺寸与 <paramref name="source"/> 一致；尺寸不一致返回 false 由调用方重新创建。
    /// </summary>
    public static bool WriteTo(Mat source, WriteableBitmap target)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (source.IsEmpty) return false;

        if (target.PixelSize.Width != source.Width || target.PixelSize.Height != source.Height)
            return false;

        using var bgra = EnsureBgra(source);

        using var fb = target.Lock();
        var dst = fb.Address;
        var rowBytes = bgra.Width * 4;

        if (fb.RowBytes == bgra.Step)
        {
            unsafe
            {
                Buffer.MemoryCopy(
                    (void*)bgra.DataPointer,
                    (void*)dst,
                    fb.RowBytes * (long)bgra.Height,
                    (long)bgra.Step * bgra.Height);
            }
        }
        else
        {
            for (var y = 0; y < bgra.Height; y++)
            {
                unsafe
                {
                    Buffer.MemoryCopy(
                        (void*)(bgra.DataPointer + y * bgra.Step),
                        (void*)(dst + y * fb.RowBytes),
                        fb.RowBytes,
                        rowBytes);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 创建一个尺寸与 <paramref name="reference"/> 一致、像素格式 BGRA8888 的可写位图，
    /// 用于配合 <see cref="WriteTo"/> 做帧预览。
    /// </summary>
    public static WriteableBitmap CreateWriteableBitmap(Mat reference)
    {
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        var size = new PixelSize(reference.Width, reference.Height);
        return new WriteableBitmap(size, DefaultDpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
    }

    private static Mat EnsureBgra(Mat source)
    {
        // OpenCV 默认 BGR；只支持把常见格式转成 BGRA。
        switch (source.NumberOfChannels)
        {
            case 4:
                if (source.Depth == DepthType.Cv8U) return Clone(source);
                break;
            case 3:
            {
                var converted = new Mat();
                CvInvoke.CvtColor(source, converted, ColorConversion.Bgr2Bgra);
                return converted;
            }
            case 1:
            {
                var converted = new Mat();
                CvInvoke.CvtColor(source, converted, ColorConversion.Gray2Bgra);
                return converted;
            }
        }

        // Depth 不是 8U：先归一化到 8U。
        var temp = new Mat();
        source.ConvertTo(temp, DepthType.Cv8U);
        return EnsureBgra(temp);
    }

    private static Mat Clone(Mat source)
    {
        // Bitmap 构造函数要求像素数据存活，外部 Mat 释放后会引发悬挂指针，
        // 所以即便 source 已是 BGRA 也复制一份。
        var clone = new Mat();
        source.CopyTo(clone);
        return clone;
    }
}
