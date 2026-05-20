using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsApp.ViewModels.LineCards;

/// <summary>
/// 地点角标行卡片 VM。和横幅结构一致，多了一个 Index 字段（角标顺序）。
/// 对应原 WPF <c>MarkerLineModel</c>。
/// </summary>
public class MarkerLineCardViewModel(MarkerBaseFrameSet set) : LineCardViewModelBase
{
    public MarkerBaseFrameSet Set { get; } = set;
    public override BaseFrameSet FrameSet => Set;

    public int Index => Set.Data.Index;

    public string StartTime => Set.StartTime();
    public string EndTime => Set.EndTime();

    public string OriginalContent => Set.Data.BodyOriginal;
    public string TranslatedContent => Set.Data.BodyTranslated;

    public string DisplayContent => string.IsNullOrEmpty(TranslatedContent)
        ? OriginalContent
        : TranslatedContent;
}
