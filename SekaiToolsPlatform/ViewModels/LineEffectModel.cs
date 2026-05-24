using SekaiToolsBase.Story.StoryEvent;

namespace SekaiToolsPlatform.ViewModels;

/// <summary>
/// 旁白 / Banner / Marker 等只有正文没有发言人的行。
/// </summary>
public class LineEffectModel : LineModel
{
    private readonly BaseStoryEvent _baseStoryEvent;

    public LineEffectModel(BaseStoryEvent eBaseStoryEvent)
    {
        _baseStoryEvent = eBaseStoryEvent;
        OriginalContent = _baseStoryEvent.BodyOriginal;
        TranslatedContent = _baseStoryEvent.BodyTranslated;
        RefreshContentDiff();
    }

    public string OriginalContent
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string TranslatedContent
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            RefreshContentDiff();
            if (ContentTranslateChangedEnabled)
                ContentTranslateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ContentReference
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasContentReference));
            OnPropertyChanged(nameof(HasReferenceDiffView));
            RefreshContentDiff();
        }
    }

    public bool HasContentReference => !string.IsNullOrEmpty(ContentReference);
    public bool HasReferenceDiffView => ShowReferenceDiff && HasContentReference;
    public bool ContentTranslateChangedEnabled { get; set; } = true;
    public event EventHandler? ContentTranslateChanged;
    public DiffPartModel[] ContentReferenceDiffParts
    {
        get => GetProperty(Array.Empty<DiffPartModel>());
        private set => SetProperty(value);
    }

    public DiffPartModel[] TranslatedContentDiffParts
    {
        get => GetProperty(Array.Empty<DiffPartModel>());
        private set => SetProperty(value);
    }

    public override string Result =>
        string.IsNullOrWhiteSpace(TranslatedContent) ? OriginalContent : TranslatedContent;

    public BaseStoryEvent Export()
    {
        switch (_baseStoryEvent)
        {
            case BannerStoryEvent:
                return ExportBanner();
            case MarkerStoryEvent:
                return ExportMarker();
            default:
                var e = (BaseStoryEvent)_baseStoryEvent.Clone();
                e.BodyTranslated = TranslatedContent;
                return e;
        }
    }

    private BannerStoryEvent ExportBanner()
    {
        var banner = (BannerStoryEvent)_baseStoryEvent.Clone();
        banner.BodyTranslated = TranslatedContent;
        return banner;
    }

    private void RefreshContentDiff()
    {
        var (referParts, translatedParts) = TextDiffBuilder.Build(ContentReference, TranslatedContent);
        ContentReferenceDiffParts = referParts;
        TranslatedContentDiffParts = translatedParts;
    }

    protected override void OnShowReferenceDiffChanged()
    {
        OnPropertyChanged(nameof(HasReferenceDiffView));
    }

    private MarkerStoryEvent ExportMarker()
    {
        var marker = (MarkerStoryEvent)_baseStoryEvent.Clone();
        marker.BodyTranslated = TranslatedContent;
        return marker;
    }
}
