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
/// 渲染 OCR 文本模板（由原 GDI+ 实现切到 SkiaSharp，跨平台可运行）。
///
/// 公共 API 与原 GDI+ 版本保持一致：
/// <list type="bullet">
///   <item><see cref="GetMenuSign"/> / <see cref="GetMenuSignSize"/></item>
///   <item><see cref="GetFontSize(double)"/> / <see cref="GetFontSize(System.Drawing.Size,double)"/></item>
///   <item><see cref="GetTemplate"/></item>
/// </list>
///
/// 背景：原实现依赖 <c>System.Drawing.Common</c> 的 <c>Bitmap</c>/<c>Graphics</c>/<c>Font</c>/<c>GraphicsPath</c>，
/// 仅在 Windows 上可用。<see cref="SkiaSharp"/> 渲染的字形抗锯齿规则与 GDI+ 不完全一致，
/// 模板匹配阈值在 <c>TemplateMatcher</c> 中已有冗余，可吸收差异。
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
        // BGRA -> 用 alpha 通道找文字包围盒，比 BGR2GRAY 更准确，且不会触发 OpenCV
        // 在 4 通道输入上对 BGR2GRAY 的歧义。
        using var alpha = new Mat();
        CvInvoke.ExtractChannel(bgra, alpha, 3);
        using var binary = new Mat();
        CvInvoke.Threshold(alpha, binary, 1, 255, ThresholdType.Binary);
        var rect = CvInvoke.BoundingRectangle(binary);
        if (rect.Width == 0 || rect.Height == 0)
        {
            // 整张全透明：回退为整图，避免后续切片崩溃。
            return bgra.Clone();
        }
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
        using var skFont = new SKFont(typeface, fontSize);

        // 画布尺寸：与原 GDI+ 实现一致，给字符串保留充足横向空间。
        var canvasWidth = (int)(text.Length * fontSize * 2);
        var canvasHeight = fontSize * 2;

        var info = new SKImageInfo(canvasWidth, canvasHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        bitmap.Erase(SKColors.Transparent);

        using (var canvas = new SKCanvas(bitmap))
        {
            // 文本基线坐标 (10, 10 + size)：与原实现 path.AddString(... new Point(10,10)) 的语义一致。
            // GDI+ 中 AddString 的起点是字形外接框左上角，SkiaSharp.DrawText 用基线 y，
            // 因此 y 偏移为 10 + ascender；近似值取 fontSize 即可（实际包围盒在 CropByAlpha 后裁掉空白）。
            const float originX = 10f;
            var originY = 10f + fontSize;

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
                canvas.DrawText(text, originX, originY, skFont, strokePaint);
            }

            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = fillColor,
                IsAntialias = true,
            };
            canvas.DrawText(text, originX, originY, skFont, fillPaint);
        }

        // SKBitmap (BGRA8888 Unpremul) -> Emgu Mat (BGRA, 8U, 4ch)
        var skMat = SkBitmapToBgraMat(bitmap);

        if (usage == TemplateUsage.BannerContent)
        {
            // 与原实现保持：裁到内容包围盒后扩 padding，再用横幅深灰底填充透明区域。
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
            // DialogContent / MarkerContent
            _ => (fill, stroke, fontSize / 5f, true),
        };
    }

    /// <summary>
    /// SKBitmap (BGRA8888) 复制到独立的 Emgu Mat，避免引用 SKBitmap 即将释放的内存。
    /// </summary>
    private static Mat SkBitmapToBgraMat(SKBitmap bitmap)
    {
        // SkiaSharp 在 BGRA8888 上每行字节 = bitmap.RowBytes，可能含末尾 padding。
        // OpenCV Mat 用 step 描述行步长，可以直接对齐。
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
