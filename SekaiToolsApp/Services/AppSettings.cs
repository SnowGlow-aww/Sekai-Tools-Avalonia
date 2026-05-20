using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace SekaiToolsApp.Services;

/// <summary>
/// 用户配置 POCO；序列化到 <c>%USERPROFILE%/SekaiTools/Data/setting.json</c>。
///
/// 为了在迁移期与原 WPF (<c>SekaiToolsGUI</c>) 共享同一份配置，字段名 / 默认值与
/// <c>SekaiToolsGUI/Model/Setting.cs</c> 完全对齐。
/// </summary>
public sealed class AppSettings
{
    public string AppVersion { get; set; } = "1.0.0";

    /// <summary>
    /// 0 = 跟随系统 / 1 = 浅色 / 2 = 深色。
    /// 注意：原 WPF 是 0=亮 / 1=暗 / 2=高对比度 / 3=系统；
    /// Avalonia 没有高对比度变体，迁移层在 <see cref="SettingsService"/> 里做映射。
    /// </summary>
    public int CurrentApplicationTheme { get; set; } = 0;

    public string[] CustomSpecialCharacters { get; set; } = [];

    public int ProxyType { get; set; } = 0;
    public string ProxyHost { get; set; } = "127.0.0.1";
    public int ProxyPort { get; set; } = 1080;
    public string DownloadDirectory { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = string.Empty;

    public int TypewriterFadeTime { get; set; } = 50;
    public int TypewriterCharTime { get; set; } = 80;

    public string DialogFontFamily { get; set; } = "思源黑体 CN Bold";
    public string BannerFontFamily { get; set; } = "思源黑体 Medium";
    public string MarkerFontFamily { get; set; } = "思源黑体 Medium";

    public bool ExportLine1 { get; set; } = true;
    public bool ExportLine2 { get; set; } = true;
    public bool ExportLine3 { get; set; } = true;
    public bool ExportCharacter { get; set; } = true;
    public bool ExportBannerMask { get; set; } = true;
    public bool ExportBannerText { get; set; } = true;
    public bool ExportMarkerMask { get; set; } = true;
    public bool ExportMarkerText { get; set; } = true;
    public bool ExportScreenComment { get; set; } = true;

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            AppVersion = AppVersion,
            CurrentApplicationTheme = CurrentApplicationTheme,
            CustomSpecialCharacters = (string[])CustomSpecialCharacters.Clone(),
            ProxyType = ProxyType,
            ProxyHost = ProxyHost,
            ProxyPort = ProxyPort,
            DownloadDirectory = DownloadDirectory,
            FfmpegPath = FfmpegPath,
            TypewriterFadeTime = TypewriterFadeTime,
            TypewriterCharTime = TypewriterCharTime,
            DialogFontFamily = DialogFontFamily,
            BannerFontFamily = BannerFontFamily,
            MarkerFontFamily = MarkerFontFamily,
            ExportLine1 = ExportLine1,
            ExportLine2 = ExportLine2,
            ExportLine3 = ExportLine3,
            ExportCharacter = ExportCharacter,
            ExportBannerMask = ExportBannerMask,
            ExportBannerText = ExportBannerText,
            ExportMarkerMask = ExportMarkerMask,
            ExportMarkerText = ExportMarkerText,
            ExportScreenComment = ExportScreenComment,
        };
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
