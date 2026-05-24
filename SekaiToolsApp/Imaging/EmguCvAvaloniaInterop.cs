using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsApp.Imaging;

/// <summary>
/// Emgu.CV Mat 与 Avalonia Bitmap 之间的转换工具。
/// 单次显示用 <see cref="ToAvaloniaBitmap"/>；高频帧预览用 <see cref="WriteTo"/>。
/// </summary>
public static class EmguCvAvaloniaInterop
{
    private static readonly Vector DefaultDpi = new(96, 96);

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

    public static WriteableBitmap CreateWriteableBitmap(Mat reference)
    {
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        var size = new PixelSize(reference.Width, reference.Height);
        return new WriteableBitmap(size, DefaultDpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
    }

    private static Mat EnsureBgra(Mat source)
    {
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
        var clone = new Mat();
        source.CopyTo(clone);
        return clone;
    }
}
