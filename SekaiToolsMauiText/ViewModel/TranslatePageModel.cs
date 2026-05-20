using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsMauiText.Models;

namespace SekaiToolsMauiText.ViewModel;

public class TranslatePageModel : ViewModelBase
{
    public bool IsEmpty => Events.Length == 0;
    public bool HasEvents => Events.Length > 0;
    public bool CanUploadToPlatform => HasEvents && IsPlatformMode && IsAuthenticated && CurrentPlatformStoryId > 0;
    public bool HasSelectedTenant => SelectedTenant is not null;
    public bool HasSelectedStoryType => !string.IsNullOrWhiteSpace(SelectedStoryType);
    public bool HasSelectedStoryGroup => SelectedStoryGroup is not null;
    public bool HasSelectedStory => SelectedStory is not null;
    public bool HasSelectedTranslationVersion => SelectedTranslationVersion is not null;
    public string ModeText => IsPlatformMode ? "平台模式" : HasEvents ? "本地模式" : "未载入";

    public bool IsPlatformMode
    {
        get => GetProperty(false);
        private set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(CanUploadToPlatform));
            OnPropertyChanged(nameof(ModeText));
        }
    }

    public bool IsAuthenticated
    {
        get => GetProperty(false);
        private set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(CanUploadToPlatform));
        }
    }

    public string BaseUrl
    {
        get => GetProperty("http://localhost:8080");
        set => SetProperty(value);
    }

    public string Username
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string Password
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string SessionText
    {
        get => GetProperty("未连接 SekaiPlatform");
        private set => SetProperty(value);
    }

    public string CurrentDocumentTitle
    {
        get => GetProperty(string.Empty);
        private set => SetProperty(value);
    }

    public long CurrentPlatformStoryId
    {
        get => GetProperty(0L);
        private set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(CanUploadToPlatform));
        }
    }

    public string[] StoryTypes
    {
        get => GetProperty(Array.Empty<string>());
        private set => SetProperty(value);
    }

    public string SelectedStoryType
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasSelectedStoryType));
        }
    }

    public PlatformStoryGroup[] StoryGroups
    {
        get => GetProperty(Array.Empty<PlatformStoryGroup>());
        private set => SetProperty(value);
    }

    public PlatformStoryGroup? SelectedStoryGroup
    {
        get => GetProperty<PlatformStoryGroup?>(null);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasSelectedStoryGroup));
        }
    }

    public PlatformStory[] Stories
    {
        get => GetProperty(Array.Empty<PlatformStory>());
        private set => SetProperty(value);
    }

    public PlatformStory? SelectedStory
    {
        get => GetProperty<PlatformStory?>(null);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasSelectedStory));
        }
    }

    public PlatformTranslationVersion[] TranslationVersions
    {
        get => GetProperty(Array.Empty<PlatformTranslationVersion>());
        private set => SetProperty(value);
    }

    public PlatformTranslationVersion? SelectedTranslationVersion
    {
        get => GetProperty<PlatformTranslationVersion?>(null);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasSelectedTranslationVersion));
        }
    }

    public PlatformTenant[] Tenants
    {
        get => GetProperty(Array.Empty<PlatformTenant>());
        private set => SetProperty(value);
    }

    public PlatformTenant? SelectedTenant
    {
        get => GetProperty<PlatformTenant?>(null);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasSelectedTenant));
        }
    }

    public LineModel[] Events
    {
        get => GetProperty(Array.Empty<LineModel>());
        private set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasEvents));
            OnPropertyChanged(nameof(CanUploadToPlatform));
            OnPropertyChanged(nameof(ModeText));
        }
    }

    public string Result => string.Join("\n", Events.Select(item => item.Result));

    public void SetPlatformSession(PlatformAuthSession session)
    {
        IsAuthenticated = true;
        Tenants = session.Tenants.OrderBy(item => item.Name).ToArray();
        SelectedTenant = session.CurrentTenant ?? Tenants.FirstOrDefault();
        SessionText = BuildSessionText(session.User, session.CurrentTenant);
    }

    public void ClearPlatformSession()
    {
        IsAuthenticated = false;
        SessionText = "未连接 SekaiPlatform";
        Tenants = [];
        SelectedTenant = null;
        StoryTypes = [];
        SelectedStoryType = string.Empty;
        StoryGroups = [];
        SelectedStoryGroup = null;
        Stories = [];
        SelectedStory = null;
        TranslationVersions = [];
        SelectedTranslationVersion = null;
        OnPropertyChanged(nameof(HasSelectedTenant));
    }

    public void SetStoryTypes(IReadOnlyCollection<string> storyTypes)
    {
        StoryTypes = storyTypes.ToArray();
    }

    public void SetStoryGroups(IReadOnlyCollection<PlatformStoryGroup> storyGroups)
    {
        StoryGroups = storyGroups.ToArray();
        SelectedStoryGroup = StoryGroups.FirstOrDefault();
        Stories = [];
        SelectedStory = null;
        TranslationVersions = [];
        SelectedTranslationVersion = null;
    }

    public void SetStories(IReadOnlyCollection<PlatformStory> stories)
    {
        Stories = stories.ToArray();
        SelectedStory = Stories.FirstOrDefault();
        TranslationVersions = [];
        SelectedTranslationVersion = null;
    }

    public void SetTranslationVersions(IReadOnlyCollection<PlatformTranslationVersion> versions)
    {
        TranslationVersions = versions.ToArray();
        SelectedTranslationVersion = TranslationVersions.FirstOrDefault();
    }

    public void SetLocalStory(Story story, string? documentTitle = null)
    {
        CurrentPlatformStoryId = 0;
        CurrentDocumentTitle = documentTitle ?? string.Empty;
        IsPlatformMode = false;
        SetEvents(BuildLineModels(story.Events));
    }

    public void SetPlatformStory(PlatformStory story, IReadOnlyCollection<PlatformSourceLine> sourceLines)
    {
        var events = new List<BaseStoryEvent>();
        foreach (var sourceLine in sourceLines.OrderBy(item => item.LineNo))
        {
            BaseStoryEvent storyEvent = sourceLine.LineType == "dialogue"
                ? new DialogStoryEvent(
                    sourceLine.LineNo,
                    sourceLine.Text,
                    0,
                    sourceLine.Speaker ?? string.Empty,
                    false,
                    false)
                : new BannerStoryEvent(sourceLine.Text, sourceLine.LineNo, sourceLine.LineNo);
            events.Add(storyEvent);
        }

        CurrentPlatformStoryId = story.Id;
        CurrentDocumentTitle = story.Title;
        IsPlatformMode = true;
        SetEvents(BuildLineModels(events.ToArray(), sourceLines));
    }

    public void ApplyReference(Story story)
    {
        for (var i = 0; i < story.Events.Length; i++)
        {
            if (i >= Events.Length) break;
            var line = Events[i];
            var ev = story.Events[i];
            switch (line)
            {
                case LineDialogModel lineDialogModel when ev is DialogStoryEvent dialogStoryEvent:
                    if (lineDialogModel.OriginalCharacter != dialogStoryEvent.CharacterOriginal
                        || lineDialogModel.OriginalContent != dialogStoryEvent.BodyOriginal) continue;
                    lineDialogModel.CharacterReference = dialogStoryEvent.CharacterTranslated;
                    lineDialogModel.ContentReference = dialogStoryEvent.BodyTranslated;
                    break;
                case LineEffectModel lineEffectModel:
                    if (lineEffectModel.OriginalContent != ev.BodyOriginal) continue;
                    lineEffectModel.ContentReference = ev.BodyTranslated;
                    break;
            }
        }
    }

    public void ApplyPlatformTranslation(IReadOnlyCollection<PlatformTranslationLine> lines)
    {
        var lineMap = lines.ToDictionary(item => item.SourceLineId);
        foreach (var line in Events)
        {
            if (!line.SourceLineId.HasValue || !lineMap.TryGetValue(line.SourceLineId.Value, out var translated))
                continue;
            switch (line)
            {
                case LineDialogModel dialogModel:
                    dialogModel.TranslatedCharacter = string.IsNullOrWhiteSpace(translated.Speaker)
                        ? dialogModel.OriginalCharacter
                        : translated.Speaker;
                    dialogModel.TranslatedContent = translated.Text;
                    break;
                case LineEffectModel effectModel:
                    effectModel.TranslatedContent = translated.Text;
                    break;
            }
        }
    }

    public void ApplyPlatformReference(IReadOnlyCollection<PlatformTranslationLine> lines)
    {
        var lineMap = lines.ToDictionary(item => item.SourceLineId);
        foreach (var line in Events)
        {
            if (!line.SourceLineId.HasValue || !lineMap.TryGetValue(line.SourceLineId.Value, out var translated))
                continue;
            switch (line)
            {
                case LineDialogModel dialogModel:
                    dialogModel.CharacterReference = translated.Speaker ?? string.Empty;
                    dialogModel.ContentReference = translated.Text;
                    break;
                case LineEffectModel effectModel:
                    effectModel.ContentReference = translated.Text;
                    break;
            }
        }
    }

    public IReadOnlyCollection<PlatformCreateTranslationLineRequest> BuildPlatformTranslationLines()
    {
        return Events
            .Where(item => item.SourceLineId.HasValue && item.SourceLineNo.HasValue)
            .Select(item =>
            {
                return item switch
                {
                    LineDialogModel dialogModel => new PlatformCreateTranslationLineRequest(
                        item.SourceLineId!.Value,
                        item.SourceLineNo!.Value,
                        NormalizeTranslatedSpeaker(dialogModel),
                        dialogModel.TranslatedContent,
                        null),
                    LineEffectModel effectModel => new PlatformCreateTranslationLineRequest(
                        item.SourceLineId!.Value,
                        item.SourceLineNo!.Value,
                        null,
                        effectModel.TranslatedContent,
                        null),
                    _ => throw new InvalidOperationException("Unsupported line model.")
                };
            })
            .ToArray();
    }

    public void Clear()
    {
        ClearEventRegisters();
        Events = [];
        CurrentPlatformStoryId = 0;
        CurrentDocumentTitle = string.Empty;
        IsPlatformMode = false;
    }

    private static string BuildSessionText(PlatformUser user, PlatformTenant? currentTenant)
    {
        var userName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.QqId ?? $"#{user.Id}" : user.DisplayName;
        var tenantName = currentTenant?.Name ?? "未选择租户";
        return $"{userName} / {tenantName}";
    }

    private static string? NormalizeTranslatedSpeaker(LineDialogModel dialogModel)
    {
        return string.IsNullOrWhiteSpace(dialogModel.TranslatedCharacter) ||
               dialogModel.TranslatedCharacter == dialogModel.OriginalCharacter
            ? null
            : dialogModel.TranslatedCharacter;
    }

    private LineModel[] BuildLineModels(
        IReadOnlyList<BaseStoryEvent> events,
        IReadOnlyCollection<PlatformSourceLine>? sourceLines = null)
    {
        ClearEventRegisters();
        var sourceLineMap = sourceLines?.ToDictionary(item => item.LineNo);
        var lineModels = new List<LineModel>();
        foreach (var baseStoryEvent in events)
        {
            LineModel model = baseStoryEvent switch
            {
                DialogStoryEvent dialogStoryEvent => CreateDialogModel(dialogStoryEvent),
                _ => CreateEffectModel(baseStoryEvent)
            };

            if (sourceLineMap is not null && sourceLineMap.TryGetValue(baseStoryEvent.Index, out var sourceLine))
            {
                model.SourceLineId = sourceLine.Id;
                model.SourceLineNo = sourceLine.LineNo;
                model.SourceLineType = sourceLine.LineType;
            }

            lineModels.Add(model);
        }

        return lineModels.ToArray();
    }

    private LineDialogModel CreateDialogModel(DialogStoryEvent dialogStoryEvent)
    {
        var lineDialogModel = new LineDialogModel(dialogStoryEvent);
        lineDialogModel.CharacterTranslateChanged += OnCharacterTranslateChanged;
        lineDialogModel.ContentTranslateChanged += OnDialogContentTranslateChanged;
        return lineDialogModel;
    }

    private LineEffectModel CreateEffectModel(BaseStoryEvent baseStoryEvent)
    {
        var lineEffectModel = new LineEffectModel(baseStoryEvent);
        lineEffectModel.ContentTranslateChanged += OnEffectContentTranslateChanged;
        return lineEffectModel;
    }

    private void SetEvents(LineModel[] events)
    {
        Events = events;
    }

    private void ClearEventRegisters()
    {
        foreach (var lineModel in Events)
        {
            switch (lineModel)
            {
                case LineDialogModel dialogModel:
                    dialogModel.CharacterTranslateChanged -= OnCharacterTranslateChanged;
                    dialogModel.ContentTranslateChanged -= OnDialogContentTranslateChanged;
                    break;
                case LineEffectModel effectModel:
                    effectModel.ContentTranslateChanged -= OnEffectContentTranslateChanged;
                    break;
            }
        }
    }

    private void OnCharacterTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineDialogModel changedLine) return;
        changedLine.CharacterTranslateChangedEnabled = false;
        foreach (var line in Events.OfType<LineDialogModel>())
        {
            if (line.OriginalCharacter != changedLine.OriginalCharacter) continue;
            if (line == changedLine) continue;
            line.CharacterTranslateChangedEnabled = false;
            line.TranslatedCharacter = changedLine.TranslatedCharacter;
            line.CharacterTranslateChangedEnabled = true;
        }

        changedLine.CharacterTranslateChangedEnabled = true;
    }

    private void OnDialogContentTranslateChanged(object? sender, EventArgs args)
    {
        // Dialog content changes are per-line.
    }

    private void OnEffectContentTranslateChanged(object? sender, EventArgs args)
    {
        if (sender is not LineEffectModel changedLine) return;
        changedLine.ContentTranslateChangedEnabled = false;
        foreach (var line in Events.OfType<LineEffectModel>())
        {
            if (line.OriginalContent != changedLine.OriginalContent) continue;
            if (line == changedLine) continue;
            line.ContentTranslateChangedEnabled = false;
            line.TranslatedContent = changedLine.TranslatedContent;
            line.ContentTranslateChangedEnabled = true;
        }

        changedLine.ContentTranslateChangedEnabled = true;
    }
}
