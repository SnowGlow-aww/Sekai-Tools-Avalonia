using System.Drawing;
using SkiaSharp;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace SekaiToolsCore.Utils;

public static class UtilFunc
{
    public static string Remains(this TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalSeconds >= 1)
            return $"{timeSpan.Seconds}s";
        return $"{timeSpan.Milliseconds}ms";
    }

    public static IEnumerable<T> Contact<T>(params IEnumerable<T>[] arrays)
    {
        return arrays.SelectMany(x => x);
    }

    public static bool IsSorted<T>(this IEnumerable<T> enumerable, bool strictIncreasing = true) where T : IComparable
    {
        var comparer = Comparer<T>.Default;
        T previous = default!;
        var first = true;
        var sign = strictIncreasing ? 1 : 0;
        foreach (var item in enumerable)
        {
            if (!first)
            {
                if (sign == 0) sign = int.Sign(comparer.Compare(previous, item));
                else if (sign != int.Sign(comparer.Compare(previous, item)))
                    return false;
            }

            first = false;
            previous = item;
        }

        return true;
    }

    public static T Middle<T>(T a, T b, T c) where T : IComparable
    {
        if (a.CompareTo(b) < 0)
        {
            if (b.CompareTo(c) < 0)
                return b;
            return a.CompareTo(c) < 0 ? c : a;
        }


        if (a.CompareTo(c) < 0)
            return a;
        return b.CompareTo(c) < 0 ? c : b;
    }

    public static Rectangle FromCenter(Point center, Size size)
    {
        return new Rectangle(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height);
    }

    public static Point Center(this Rectangle rect)
    {
        return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    }

    public static Point Center(this Size size)
    {
        return new Point(size.Width / 2, size.Height / 2);
    }

    public static Point Resize(this Point point, double ratio)
    {
        return new Point((int)(point.X * ratio), (int)(point.Y * ratio));
    }

    public static Size Resize(this Size size, double ratio)
    {
        return new Size((int)(size.Width * ratio), (int)(size.Height * ratio));
    }

    public static void MatRemoveErrorInf(this Mat mat)
    {
        using Mat positiveInf = new(mat.Size, mat.Depth, 1);
        using Mat negativeInf = new(mat.Size, mat.Depth, 1);

        positiveInf.SetTo(new MCvScalar(1));
        negativeInf.SetTo(new MCvScalar(0));

        using (var mask = new Mat(mat.Size, mat.Depth, 1))
        {
            CvInvoke.Compare(mat, positiveInf, mask, CmpType.GreaterEqual);
            mat.SetTo(new MCvScalar(0), mask);
        }

        using (var mask = new Mat(mat.Size, mat.Depth, 1))
        {
            CvInvoke.Compare(mat, negativeInf, mask, CmpType.LessEqual);
            mat.SetTo(new MCvScalar(0), mask);
        }

        // NaN != NaN is the only reliable way to detect NaN (IEEE 754)
        using (var mask = new Mat(mat.Size, mat.Depth, 1))
        {
            CvInvoke.Compare(mat, mat, mask, CmpType.NotEqual);
            mat.SetTo(new MCvScalar(0), mask);
        }
    }

    public static IEnumerable<string> GetFontFamilyNames()
    {
        // 原 GDI+ 的 InstalledFontCollection 仅在 Windows 受支持。
        // SKFontManager 是 SkiaSharp 在各平台对系统字体的统一接口。
        return SKFontManager.Default.FontFamilies;
    }

}

public static class RectangleExtensions
{
    public static void Extend(this ref Rectangle rect, int x, int y)
    {
        rect.X -= x;
        rect.Y -= y;
        rect.Width += x * 2;
        rect.Height += y * 2;
    }

    public static void Extend(this ref Rectangle rect, double ratio)
    {
        switch (ratio)
        {
            case < 0:
                return;
            case < 1:
                ratio = 1 + ratio;
                break;
        }

        var x = (int)(rect.Width * ratio);
        var y = (int)(rect.Height * ratio);
        rect.Extend(x, y);
    }

    public static void Limit(this ref Rectangle rect, Rectangle limit)
    {
        rect = Rectangle.Intersect(rect, limit);
    }
}