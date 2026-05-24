using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsApp.ViewModels.LineCards;

/// <summary>
/// 横幅行卡片 VM。横幅没有翻译角色字段，只展示原文 / 译文及起止时间。
/// </summary>
public class BannerLineCardViewModel(BannerBaseFrameSet set) : LineCardViewModelBase
{
    public BannerBaseFrameSet Set { get; } = set;
    public override BaseFrameSet FrameSet => Set;

    public string StartTime => Set.StartTime();
    public string EndTime => Set.EndTime();

    public string OriginalContent => Set.Data.BodyOriginal;
    public string TranslatedContent => Set.Data.BodyTranslated;

    /// <summary>翻译为空时仍展示原文，避免空卡片。</summary>
    public string DisplayContent => string.IsNullOrEmpty(TranslatedContent)
        ? OriginalContent
        : TranslatedContent;
}
