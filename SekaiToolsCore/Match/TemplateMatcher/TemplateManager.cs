using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SkiaSharp;

namespace SekaiToolsCore.Match.TemplateMatcher;

public enum TemplateUsage
{
    DialogNameTag,
    DialogContent,
    BannerContent,
    MarkerContent
}

/// <summary>
/// 使用 SkiaSharp 渲染文本模板图像，供模板匹配使用。
/// </summary>
public class TemplateManager(Size videoResolution, bool noScale = false)
{
    private const string MenuSignBase = "menu-107px.png";
    private const string DbFontBase = "FOT-RodinNTLGPro-DB.otf";
    private const string EbFontBase = "FOT-RodinNTLGPro-EB.otf";

    private readonly Dictionary<TemplateUsage, Dictionary<string, Mat>?> _template = new();
    private readonly Dictionary<string, SKTypeface> _typefaceCache = new();

    private Mat? _menuSign;

    public Mat GetMenuSign()
    {
        if (_menuSign != null) return _menuSign;
        var menuTemplatePath = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, MenuSignBase);
        if (!File.Exists(menuTemplatePath)) throw new FileNotFoundException();
        var menuTemplate = CvInvoke.Imread(menuTemplatePath, ImreadModes.Unchanged)!;
        var menuSize = GetMenuSignSize(videoResolution);

        CvInvoke.Resize(menuTemplate, menuTemplate, new Size(menuSize, menuSize));
        _menuSign = menuTemplate;
        return menuTemplate;
    }

    public static int GetMenuSignSize(Size videoSize)
    {
        const double standardRatio = 16.0 / 9.0;
        var ratio = videoSize.Width / (double)videoSize.Height;
        var menuSize = ratio switch
        {
            < standardRatio => (int)(videoSize.Width * 0.0417),
            _ => (int)(videoSize.Height * 0.0741)
        };

        return menuSize;
    }

    public static int GetFontSize(Size videoSize, double scale = 0.95)
    {
        const double standardRatio = 16.0 / 9.0;
        var ratio = videoSize.Width / (double)videoSize.Height;
        var size = ratio switch
        {
            < standardRatio => (int)(videoSize.Width * 0.024),
            _ => (int)(videoSize.Height * 0.043)
        };
        var result = (int)(size * scale);
        return result;
    }

    public int GetFontSize(double fontScale = 0.95)
    {
        var size = GetFontSize(videoResolution, fontScale);
        var scale = noScale ? 1 : 5;
        var result = size * scale;
        return result;
    }

    private static Mat CropByAlpha(Mat bgra)
    {
        using var alpha = new Mat();
        CvInvoke.ExtractChannel(bgra, alpha, 3);
        using var binary = new Mat();
        CvInvoke.Threshold(alpha, binary, 1, 255, ThresholdType.Binary);
        var rect = CvInvoke.BoundingRectangle(binary);
        if (rect.Width == 0 || rect.Height == 0)
            return bgra.Clone();
        return new Mat(bgra, rect).Clone();
    }

    private SKTypeface GetTypeface(string fontFilePath)
    {
        if (_typefaceCache.TryGetValue(fontFilePath, out var cached)) return cached;
        var typeface = SKTypeface.FromFile(fontFilePath)
                       ?? throw new InvalidOperationException($"Failed to load typeface: {fontFilePath}");
        _typefaceCache[fontFilePath] = typeface;
        return typeface;
    }

    private SKTypeface GetDbTypeface()
    {
        var fontFilePath = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, DbFontBase);
        return GetTypeface(fontFilePath);
    }

    private SKTypeface GetEbTypeface()
    {
        var fontFilePath = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, EbFontBase);
        return GetTypeface(fontFilePath);
    }

    private Mat CreateImageWithText(TemplateUsage usage, string text)
    {
        var (typeface, fontSizeScale) = usage switch
        {
            TemplateUsage.DialogNameTag => (GetEbTypeface(), 0.95),
            TemplateUsage.DialogContent or TemplateUsage.BannerContent or TemplateUsage.MarkerContent
                => (GetDbTypeface(), 0.925),
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null)
        };

        var fontSize = GetFontSize(fontSizeScale);

        var canvasWidth = (int)(text.Length * fontSize * 2);
        var canvasHeight = fontSize * 2;

        var info = new SKImageInfo(canvasWidth, canvasHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        bitmap.Erase(SKColors.Transparent);

        using (var canvas = new SKCanvas(bitmap))
        {
            using var textPaint = new SKPaint
            {
                Typeface = typeface,
                TextSize = fontSize,
                IsAntialias = true,
            };

            const float originX = 10f;
            var originY = 10f + fontSize;

            using var textPath = textPaint.GetTextPath(text, originX, originY);

            var (fillColor, strokeColor, strokeWidth, withStroke) = ResolvePaints(usage, fontSize);

            if (withStroke)
            {
                using var strokePaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeJoin = SKStrokeJoin.Round,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeWidth = strokeWidth,
                    Color = strokeColor,
                    IsAntialias = true,
                };
                canvas.DrawPath(textPath, strokePaint);
            }

            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = fillColor,
                IsAntialias = true,
            };
            canvas.DrawPath(textPath, fillPaint);
        }

        var skMat = SkBitmapToBgraMat(bitmap);

        if (usage == TemplateUsage.BannerContent)
        {
            using var cropped = CropByAlpha(skMat);
            skMat.Dispose();

            var extendPixel = (int)(fontSize / 16f);
            var extendSize = new Size(cropped.Width + extendPixel * 2, cropped.Height + extendPixel * 2);
            var expandedMat = new Mat(extendSize, cropped.Depth, cropped.NumberOfChannels);
            expandedMat.SetTo(new MCvScalar(0, 0, 0, 0));
            const int bannerGrayScale = 80;
            cropped.CopyTo(new Mat(expandedMat, new Rectangle(extendPixel, extendPixel, cropped.Width, cropped.Height)));

            using var bgMat = new Mat(expandedMat.Size, expandedMat.Depth, expandedMat.NumberOfChannels);
            bgMat.SetTo(new MCvScalar(bannerGrayScale, bannerGrayScale, bannerGrayScale, 255));
            CvInvoke.BitwiseOr(bgMat, expandedMat, expandedMat);
            return expandedMat;
        }

        var result = CropByAlpha(skMat);
        skMat.Dispose();
        return result;
    }

    private static (SKColor fill, SKColor stroke, float strokeWidth, bool withStroke) ResolvePaints(
        TemplateUsage usage, float fontSize)
    {
        const byte fillScale = 235;
        const byte grayScale = 64;
        var fill = new SKColor(fillScale, fillScale, fillScale, 255);
        var stroke = new SKColor(grayScale, grayScale, grayScale, 255);

        return usage switch
        {
            TemplateUsage.BannerContent => (fill, default, 0f, false),
            TemplateUsage.DialogNameTag => (fill, stroke, fontSize / 5f, true),
            _ => (fill, stroke, fontSize / 5f, true),
        };
    }

    private static Mat SkBitmapToBgraMat(SKBitmap bitmap)
    {
        var src = new Mat(bitmap.Height, bitmap.Width, DepthType.Cv8U, 4, bitmap.GetPixels(), bitmap.RowBytes);
        try
        {
            return src.Clone();
        }
        finally
        {
            src.Dispose();
        }
    }

    public Mat GetTemplate(TemplateUsage usage, string text)
    {
        var usageDict = _template.GetValueOrDefault(usage);
        if (usageDict == null) _template[usage] = usageDict = new Dictionary<string, Mat>();

        if (usageDict.TryGetValue(text, out var template)) return template;

        var mat = CreateImageWithText(usage, text);
        usageDict[text] = mat;
        return mat;
    }
}
