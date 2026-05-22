using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SekaiDataFetch;
using SekaiDataFetch.Item;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsApp.Services;
using SekaiToolsBase;
using SekaiToolsBase.DataList;
using SekaiToolsCore;

namespace SekaiToolsApp.ViewModels;

/// <summary>
/// 数据下载主页 ViewModel。
///
/// 对应原 WPF <c>SekaiToolsGUI/View/Download/DownloadPage.xaml.cs</c> 及其 5 个子 Tab。
/// 五种剧情类型共享一个候选列表 (<see cref="Candidates"/>) 与下载列表 (<see cref="Tasks"/>)；
/// 各 tab 拥有独立的过滤状态（Unit / Event / Special / Card / Action），切换 <see cref="StoryTypeIndex"/>
/// 时由 <see cref="RebuildCandidates"/> 路由到对应的构建方法。
///
/// 简化项（与上游 WPF 的差距）：
/// - Event/Action tab 上游有 31-角色 checkbox 网格 + 团 checkbox 关联；这里只保留主类型/区域过滤，
///   未保留角色过滤（用户群较小，过滤价值低；如有需要后续可作为 m1-download-charfilter 子任务补全）。
/// - Card tab 卡片"前篇/后篇" 显示在同一行；这里拆成两条独立 Candidate。
/// </summary>
public partial class DownloadPageViewModel : ViewModelBase
{
    private const string SourceListUrl = "https://config.g.xbb.moe/source.json";

    /// <summary>剧情类型下拉项；与 <see cref="StoryTypeIndex"/> 一一对应。</summary>
    public static readonly string[] StoryTypeNames =
    [
        "主线剧情",
        "活动剧情",
        "特殊剧情",
        "活动卡面",
        "特殊卡面",
        "初始卡面",
        "升级卡面",
        "初始地图对话",
        "升级地图对话",
        "追加地图对话",
        "主界面语音",
    ];

    /// <summary>活动剧情按 GameEvent.EventType 过滤。索引和上游 WPF EventStoryTab 的 BoxType 保持一致。</summary>
    public static readonly string[] EventTypeFilterNames =
    [
        "全部",
        "马拉松 (marathon)",
        "5v5 (cheerful_carnival)",
        "World Link (world_bloom)",
    ];

    [ObservableProperty] private SourceData[] _sources = SourceData.Default;
    [ObservableProperty] private int _currentSourceIndex;
    [ObservableProperty] private int _storyTypeIndex;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _progressText = string.Empty;

    public bool HasProgress => IsRefreshing || IsDownloading;

    // —— 各 Tab 的过滤状态 ——
    [ObservableProperty] private UnitOption? _selectedUnit;
    [ObservableProperty] private int _eventTypeFilter;
    [ObservableProperty] private string? _selectedSpecial;
    [ObservableProperty] private CharacterOption? _selectedCharacter;
    [ObservableProperty] private Area? _selectedArea;

    /// <summary>当前选中的源；越界时返回 null。</summary>
    public SourceData? CurrentSource =>
        CurrentSourceIndex >= 0 && CurrentSourceIndex < Sources.Length
            ? Sources[CurrentSourceIndex]
            : null;

    public bool IsBusy => IsRefreshing || IsDownloading;
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool IsUnitTab => StoryTypeIndex == 0;
    public bool IsEventTab => StoryTypeIndex == 1;
    public bool IsSpecialTab => StoryTypeIndex == 2;
    public bool IsCardTab => StoryTypeIndex is >= 3 and <= 6;
    public bool IsActionTab => StoryTypeIndex is >= 7 and <= 9;
    public bool IsGreetTab => StoryTypeIndex == 10;

    /// <summary>主线剧情的 6 个团（light_sound / idol / theme_park / street / school_refusal / piapro）。</summary>
    public ObservableCollection<UnitOption> Units { get; } = new();

    /// <summary>
    /// 活动 / 区域 tab 共用的 Banner 角色筛选：6 行（每行一个团 logo + 该团角色头像 checkbox）。
    /// 1..26 是 26 个原作主角，27..31 是 5 个延伸 mob 角色（Miku 等），与 WPF 上游 EventStoryTab 的网格 1:1。
    /// </summary>
    public ObservableCollection<BannerUnitRow> BannerUnits { get; } = new();

    /// <summary>特殊剧情下拉项（标题列表，由刷新后填充）。</summary>
    public ObservableCollection<string> SpecialTitles { get; } = new();

    /// <summary>角色剧情角色下拉项（id 1..26，固定 26 个原作主角）。</summary>
    public ObservableCollection<CharacterOption> Characters { get; } = new();

    /// <summary>地图对话区域下拉项（由刷新后填充）。</summary>
    public ObservableCollection<Area> Areas { get; } = new();

    /// <summary>当前 Tab 下展开的章节-话集合，可被加入 <see cref="Tasks"/>。</summary>
    public ObservableCollection<DownloadCandidate> Candidates { get; } = new();

    /// <summary>下载列表（右栏）。</summary>
    public ObservableCollection<DownloadTaskItem> Tasks { get; } = new();

    private readonly Dictionary<string, string> _sizeCache = new();
    private CancellationTokenSource? _sizeProbeCts;

    private readonly DownloadHistoryService _historyService = DownloadHistoryService.Instance;
    private bool _suppressHistoryPersistence;

    public DownloadPageViewModel()
    {
        foreach (var (key, name) in Constants.UnitName)
            Units.Add(new UnitOption(key, name));
        SelectedUnit = Units.FirstOrDefault();

        foreach (var (id, name) in Constants.CharacterIdToName)
        {
            // 只取 1..26 的原作主角（27..31 是延伸 Miku 类，没有独立角色剧情）。
            if (id < 1 || id > 26) continue;
            Characters.Add(new CharacterOption(id, name));
        }
        SelectedCharacter = Characters.FirstOrDefault();

        InitBannerUnits();

        ApplyProxyAndSource();

        LoadPersistedTasks();
        // List* 单例在首次访问时会从本地缓存自动加载；如有缓存则把过滤项一并填好，
        // 用户切到对应 Tab 不需要先点刷新就能看到上次的数据。
        ReloadSpecialTitles();
        ReloadAreas();
        RebuildCandidates();
    }

    /// <summary>
    /// 初始化 6 个团角色行（与 WPF EventStoryTab 网格一致），默认全部勾选。
    /// 每个 BannerUnitRow / CharacterCheckOption 都接到一个统一的回调：
    /// 角色 checkbox 变化 → 重新计算所属团的 IsAllChecked（不触发联级勾选）→ <see cref="RebuildCandidates"/>。
    /// 团 checkbox 变化 → 联级勾选/反选本团角色 → <see cref="RebuildCandidates"/>。
    /// </summary>
    private void InitBannerUnits()
    {
        // (UnitKey, [CharacterId...]). 顺序 1-4+27, 5-8+28, 9-12+29, 13-16+30, 17-20+31, 21-26。
        (string Key, int[] Ids)[] config =
        [
            ("light_sound",     new[] { 1, 2, 3, 4, 27 }),
            ("idol",            new[] { 5, 6, 7, 8, 28 }),
            ("street",          new[] { 9, 10, 11, 12, 29 }),
            ("theme_park",      new[] { 13, 14, 15, 16, 30 }),
            ("school_refusal",  new[] { 17, 18, 19, 20, 31 }),
            ("piapro",          new[] { 21, 22, 23, 24, 25, 26 }),
        ];

        foreach (var (unitKey, ids) in config)
        {
            var row = new BannerUnitRow(unitKey, $"avares://SekaiToolsApp/Assets/Unit/logo_{unitKey}.png")
            {
                IsChecked = true,
            };
            foreach (var id in ids)
            {
                var ch = new CharacterCheckOption(id, $"avares://SekaiToolsApp/Assets/Characters/chr_{id}.png")
                {
                    IsChecked = true,
                };
                ch.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName != nameof(CharacterCheckOption.IsChecked)) return;
                    if (_suspendBannerSync) return;
                    // 同步该团的"是否全选"状态（不再级联，避免环路）。
                    _suspendBannerSync = true;
                    row.IsChecked = row.Characters.All(c => c.IsChecked);
                    _suspendBannerSync = false;
                    RebuildCandidates();
                };
                row.Characters.Add(ch);
            }
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(BannerUnitRow.IsChecked)) return;
                if (_suspendBannerSync) return;
                _suspendBannerSync = true;
                foreach (var c in row.Characters) c.IsChecked = row.IsChecked;
                _suspendBannerSync = false;
                RebuildCandidates();
            };
            BannerUnits.Add(row);
        }
    }

    /// <summary>防止 BannerUnitRow 和 CharacterCheckOption 互相回调产生死循环。</summary>
    private bool _suspendBannerSync;

    /// <summary>当前所有勾选的角色 ID 集合（活动 / 区域 tab 过滤用）。</summary>
    private HashSet<int> IncludedCharacterIds()
    {
        var set = new HashSet<int>();
        foreach (var row in BannerUnits)
            foreach (var c in row.Characters)
                if (c.IsChecked) set.Add(c.CharacterId);
        return set;
    }

    /// <summary>
    /// 全选 / 全清 所有 Banner 角色（"全选" / "清除" 按钮调用）。
    /// </summary>
    public void SetAllBannerChecked(bool value)
    {
        _suspendBannerSync = true;
        foreach (var row in BannerUnits)
        {
            row.IsChecked = value;
            foreach (var c in row.Characters) c.IsChecked = value;
        }
        _suspendBannerSync = false;
        RebuildCandidates();
    }

    private void ApplyProxyAndSource()
    {
        // VM 不直接读 SettingsService.Current 之外的状态：每次刷新/下载前同步当前生效的代理。
        var proxy = BuildProxy();
        Fetcher.Instance.SetProxy(proxy);
        ListUnitStory.Instance.SetProxy(proxy);
        ListEventStory.Instance.SetProxy(proxy);
        ListSpecialStory.Instance.SetProxy(proxy);
        ListCardStory.Instance.SetProxy(proxy);
        ListActionStory.Instance.SetProxy(proxy);
        ListGreetStory.Instance.SetProxy(proxy);

        if (CurrentSource != null)
        {
            Fetcher.Instance.SetSource(CurrentSource);
            SourceList.Instance.SourceData = CurrentSource;
            ListUnitStory.Instance.SetSource(CurrentSource);
            ListEventStory.Instance.SetSource(CurrentSource);
            ListSpecialStory.Instance.SetSource(CurrentSource);
            ListCardStory.Instance.SetSource(CurrentSource);
            ListActionStory.Instance.SetSource(CurrentSource);
            ListGreetStory.Instance.SetSource(CurrentSource);
        }
    }

    private static Proxy BuildProxy()
    {
        var s = SettingsService.Instance.Current;
        // AppSettings.ProxyType: 0=None, 1=Http, 2=Socks5
        var type = s.ProxyType switch
        {
            1 => Proxy.Type.Http,
            2 => Proxy.Type.Socks5,
            _ => Proxy.Type.None,
        };
        return new Proxy(s.ProxyHost, s.ProxyPort, type);
    }

    partial void OnSourcesChanged(SourceData[] value)
    {
        OnPropertyChanged(nameof(CurrentSource));
        ApplyProxyAndSource();
    }

    partial void OnCurrentSourceIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentSource));
        ApplyProxyAndSource();
    }

    partial void OnStoryTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsUnitTab));
        OnPropertyChanged(nameof(IsEventTab));
        OnPropertyChanged(nameof(IsSpecialTab));
        OnPropertyChanged(nameof(IsCardTab));
        OnPropertyChanged(nameof(IsActionTab));
        OnPropertyChanged(nameof(IsGreetTab));
        ReloadAreasForCurrentFilter();
        RebuildCandidates();
    }

    partial void OnSelectedUnitChanged(UnitOption? value)
    {
        // 同步给所有 UnitOption.IsSelected，axaml 的 RadioButton.IsChecked 绑定 IsSelected
        // 来正确显示选中态（仅靠 SelectedUnit 没法让 RadioButton 显示勾选）。
        foreach (var u in Units) u.IsSelected = ReferenceEquals(u, value);
        RebuildCandidates();
    }
    partial void OnEventTypeFilterChanged(int value) => RebuildCandidates();
    partial void OnSelectedSpecialChanged(string? value) => RebuildCandidates();
    partial void OnSelectedCharacterChanged(CharacterOption? value) => RebuildCandidates();
    partial void OnSelectedAreaChanged(Area? value) => RebuildCandidates();

    partial void OnIsRefreshingChanged(bool value) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(HasProgress)); }
    partial void OnIsDownloadingChanged(bool value) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(HasProgress)); }
    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnSearchTextChanged(string value) => RebuildCandidates();

    /// <summary>从对应 List* 缓存重建当前 Tab 的候选列表（不发起网络请求）。</summary>
    public void RebuildCandidates()
    {
        Candidates.Clear();
        switch (StoryTypeIndex)
        {
            case 0: BuildUnitCandidates(); break;
            case 1: BuildEventCandidates(); break;
            case 2: BuildSpecialCandidates(); break;
            case 3: BuildCardCandidates("rarity_4"); break;
            case 4: BuildCardCandidates("rarity_birthday"); break;
            case 5: BuildCardCandidates("rarity_1", "rarity_2"); break;
            case 6: BuildCardCandidates("rarity_3"); break;
            case 7: BuildActionCandidates(ActionFilter.Initial); break;
            case 8: BuildActionCandidates(ActionFilter.Upgrade); break;
            case 9: BuildActionCandidates(ActionFilter.Additional); break;
            case 10: BuildGreetCandidates(); break;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            var toRemove = Candidates.Where(c => !c.BaseTitle.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var item in toRemove)
                Candidates.Remove(item);
        }

        _ = ProbeCandidateSizesAsync();
    }

    private async Task ProbeCandidateSizesAsync()
    {
        _sizeProbeCts?.Cancel();
        _sizeProbeCts = new CancellationTokenSource();
        var ct = _sizeProbeCts.Token;

        var candidates = Candidates.ToList();
        if (candidates.Count == 0 || CurrentSource == null) return;
        SourceList.Instance.SourceData = CurrentSource;

        using var http = new HttpClient(BuildHttpHandler()) { Timeout = TimeSpan.FromSeconds(10) };
        var semaphore = new SemaphoreSlim(6);

        var tasks = candidates.Select(async c =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested) return;
                var url = c.UrlBuilder();
                if (_sizeCache.TryGetValue(url, out var cached))
                {
                    c.SizeText = cached;
                    return;
                }
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                using var resp = await http.SendAsync(req, ct);
                if (resp.Content.Headers.ContentLength is { } len)
                {
                    var size = FormatSize(len);
                    _sizeCache[url] = size;
                    c.SizeText = size;
                    c.ContentLength = len;
                }
            }
            catch { /* ignore probe failures */ }
            finally { semaphore.Release(); }
        });

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { }
    }

    private void BuildUnitCandidates()
    {
        if (SelectedUnit == null) return;
        var data = ListUnitStory.Instance.Data;
        if (!data.TryGetValue(SelectedUnit.Key, out var unitSet)) return;

        foreach (var chapter in unitSet.Chapters.Reverse())
        {
            var ab = chapter.AssetBundleName;
            var chapterName = chapter.Name;
            foreach (var ep in chapter.Episodes.Reverse())
            {
                var scenarioId = ep.ScenarioId;
                var key = ep.Key;
                Candidates.Add(new DownloadCandidate(
                    title: chapterName + " - " + key,
                    urlBuilder: () => SourceList.Instance.UnitStory(scenarioId, ab)));
            }
        }
    }

    private static readonly string[][] EventTypeFilterMatrix =
    [
        ["marathon", "cheerful_carnival", "world_bloom"], // 全部
        ["marathon"],
        ["cheerful_carnival"],
        ["world_bloom"],
    ];

    /// <summary>world_bloom 模式下按团分组的角色 id：1-4+27 / 5-8+28 / ...，
    /// 与 WPF 上游 EventStoryTab.JudgeVisibility 一致。Banner ID 落入哪个团就 OR 该团角色勾选。</summary>
    private static readonly int[][] WorldBloomUnitGroups =
    [
        new[] { 1, 2, 3, 4, 27 },
        new[] { 5, 6, 7, 8, 28 },
        new[] { 9, 10, 11, 12, 29 },
        new[] { 13, 14, 15, 16, 30 },
        new[] { 17, 18, 19, 20, 31 },
        new[] { 21, 22, 23, 24, 25, 26 },
    ];

    /// <summary>
    /// 活动剧情 Banner 过滤：
    /// - marathon / cheerful_carnival：banner 单角色，检查该 ID 是否被勾选；
    /// - world_bloom：banner 是一个团，检查该团至少有一个角色被勾选；
    /// - 其他：放行。
    /// </summary>
    private static bool PassesBannerFilter(SekaiDataFetch.Item.EventStorySet set, HashSet<int> included)
    {
        var bannerId = set.EventStory.BannerGameCharacterUnitId;
        var type = set.GameEvent.EventType;
        if (type is "marathon" or "cheerful_carnival")
            return bannerId == 0 || included.Contains(bannerId);
        if (type == "world_bloom")
        {
            foreach (var group in WorldBloomUnitGroups)
            {
                if (!Array.Exists(group, id => id == bannerId)) continue;
                return Array.Exists(group, id => included.Contains(id));
            }
            return true;
        }
        return true;
    }

    /// <summary>地图对话 Banner 过滤：ActionSet.CharacterIds 与勾选集合有交集即通过。</summary>
    private static bool PassesBannerFilter(SekaiDataFetch.Item.AreaStorySet set, HashSet<int> included)
    {
        var ids = set.CharacterIds;
        if (ids.Length == 0) return true; // 无人物信息的对话不参与过滤
        foreach (var id in ids) if (included.Contains(id)) return true;
        return false;
    }

    private void BuildEventCandidates()
    {
        var sets = ListEventStory.Instance.Data;
        if (sets.Count == 0) return;
        var allowedTypes = EventTypeFilterMatrix[Math.Clamp(EventTypeFilter, 0, EventTypeFilterMatrix.Length - 1)];
        var included = IncludedCharacterIds();
        var filtered = sets
            .Where(s => allowedTypes.Contains(s.GameEvent.EventType))
            .Where(s => PassesBannerFilter(s, included))
            .OrderByDescending(s => s.EventStory.EventId)
            .ToList();

        foreach (var set in filtered)
        {
            var ab = set.EventStory.AssetBundleName;
            var eventName = set.GameEvent.Name;
            var eventId = set.EventStory.EventId;
            foreach (var ep in set.EventStory.EventStoryEpisodes)
            {
                var scenarioId = ep.ScenarioId;
                var title = $"No.{eventId} {eventName} - {ep.EpisodeNo} {ep.Title}";
                Candidates.Add(new DownloadCandidate(
                    title: title,
                    urlBuilder: () => SourceList.Instance.EventStory(scenarioId, ab)));
            }
        }
    }

    private void BuildSpecialCandidates()
    {
        if (string.IsNullOrEmpty(SelectedSpecial)) return;
        var data = ListSpecialStory.Instance.Data;
        if (!data.TryGetValue(SelectedSpecial!, out var set)) return;

        foreach (var ep in set.Episodes.Reverse())
        {
            var captured = ep;
            Candidates.Add(new DownloadCandidate(
                title: ep.Title,
                urlBuilder: () => SourceList.Instance.SpecialStory(captured)));
        }
    }

    private void BuildCardCandidates(params string[] rarityTypes)
    {
        if (SelectedCharacter == null) return;
        var data = ListCardStory.Instance.Data;
        if (data.Count == 0) return;

        var charId = SelectedCharacter.CharacterId;
        var matched = data
            .Where(d => d.Card.CharacterId == charId && rarityTypes.Contains(d.Card.CardRarityType))
            .OrderByDescending(d => d.Card.Id)
            .ToList();
        foreach (var set in matched)
        {
            var card = set.Card;
            var rarity = card.CardRarityType.Replace("rarity_", "") switch
            {
                "1" => "★1",
                "2" => "★2",
                "3" => "★3",
                "4" => "★4",
                "birthday" => "BD",
                _ => card.CardRarityType,
            };
            var nameBase = $"No.{card.Id} {rarity} {card.Prefix}";

            var first = set.FirstPart;
            var second = set.SecondPart;
            Candidates.Add(new DownloadCandidate(
                title: nameBase + " 前篇",
                urlBuilder: () => SourceList.Instance.MemberStory(first)));
            Candidates.Add(new DownloadCandidate(
                title: nameBase + " 後篇",
                urlBuilder: () => SourceList.Instance.MemberStory(second)));
        }
    }

    private enum ActionFilter { Initial, Upgrade, Additional }

    private void BuildActionCandidates(ActionFilter filter)
    {
        if (SelectedArea == null) return;
        var areaId = SelectedArea.Id;
        var area = ListActionStory.Instance.Areas.FirstOrDefault(a => a.Id == areaId);
        if (area == null) return;

        var included = IncludedCharacterIds();
        var sets = ListActionStory.Instance.Data
            .Where(d => d.ActionSet.AreaId == areaId)
            .Where(d => PassesBannerFilter(d, included))
            .Where(d => MatchesActionFilter(d, area, filter))
            .OrderByDescending(d => d.ActionSet.Id)
            .ToList();

        foreach (var set in sets)
        {
            var captured = set;
            var scenarioId = set.ActionSet.ScenarioId;
            var characters = set.CharacterIds.Length > 0
                ? "[角色 " + string.Join(", ", set.CharacterIds) + "]"
                : string.Empty;
            var title = $"对话 {set.ActionSet.Id} ({scenarioId}) {characters}".TrimEnd();
            Candidates.Add(new DownloadCandidate(
                title: title,
                urlBuilder: () => SourceList.Instance.ActionSet(captured)));
        }
    }

    private static bool MatchesActionFilter(AreaStorySet set, Area area, ActionFilter filter)
    {
        return filter switch
        {
            ActionFilter.Initial => area.IsBaseArea && !set.ActionSet.IsNextGrade,
            ActionFilter.Upgrade => area.IsBaseArea && set.ActionSet.IsNextGrade,
            ActionFilter.Additional => !area.IsBaseArea,
            _ => true,
        };
    }

    private void BuildGreetCandidates()
    {
        if (SelectedCharacter == null) return;
        var data = ListGreetStory.Instance.Data;
        if (data.Count == 0) return;

        var charId = SelectedCharacter.CharacterId;
        var matched = data.Where(d => d.CharacterId == charId).OrderByDescending(d => d.PublishedAt).ToList();
        foreach (var item in matched)
        {
            var serifPreview = item.Serif.Replace("\n", " ");
            if (serifPreview.Length > 40) serifPreview = serifPreview[..40] + "...";
            var title = $"[{item.Voice}] {serifPreview}";
            var captured = item;
            Candidates.Add(new DownloadCandidate(
                title: title,
                urlBuilder: () => BuildGreetVoiceUrl(captured)));
        }
    }

    private string BuildGreetVoiceUrl(SystemLive2d item)
    {
        if (CurrentSource == null) return "";
        var baseUrl = CurrentSource.StorageBaseUrl;
        if (baseUrl.Contains("sekai.best", StringComparison.OrdinalIgnoreCase))
            return baseUrl + $"sound/systemvoice/{item.AssetbundleName}/{item.Voice}.mp3";
        return baseUrl + $"startapp/sound/systemvoice/{item.AssetbundleName}/{item.Voice}.mp3";
    }

    /// <summary>
    /// 本地内置的 Moesekai (Exmeaning) JP/CN 数据源。
    /// Moesekai 不是传统 REST API，而是公共静态 master JSON + 资源镜像（见 external-api-moe.md）。
    /// 我们把它直接作为 SekaiTools 的一个数据源选项，让用户能用 Exmeaning 镜像下载剧本资源。
    /// </summary>
    private static readonly SourceData[] MoesekaiSources =
    [
        new()
        {
            SourceName = "Moesekai JP",
            SourceTemplate = "https://sekaimaster.exmeaning.com/master/{type}.json",
            StorageBaseUrl = "https://storage.exmeaning.com/sekai-jp-assets/",
            ActionSetTemplate = "scenario/actionset/{abName}/{scenarioId}.json",
            MemberStoryTemplate = "character/member/{abName}/{scenarioId}.json",
            EventStoryTemplate = "event_story/{abName}/scenario/{scenarioId}.json",
            SpecialStoryTemplate = "scenario/special/{abName}/{scenarioId}.json",
            UnitStoryTemplate = "scenario/unitstory/{abName}/{scenarioId}.json",
        },
    ];

    private static readonly SourceData MoesekaiCn = new()
    {
        SourceName = "Moesekai CN",
        SourceTemplate = "https://sekaimaster-cn.exmeaning.com/master/{type}.json",
        StorageBaseUrl = "https://storage.exmeaning.com/sekai-cn-assets/",
        ActionSetTemplate = "scenario/actionset/{abName}/{scenarioId}.json",
        MemberStoryTemplate = "character/member/{abName}/{scenarioId}.json",
        EventStoryTemplate = "event_story/{abName}/scenario/{scenarioId}.json",
        SpecialStoryTemplate = "scenario/special/{abName}/{scenarioId}.json",
        UnitStoryTemplate = "scenario/unitstory/{abName}/{scenarioId}.json",
    };

    /// <summary>
    /// 拉取远端 <c>source.json</c>，失败时回退到 <see cref="SourceData.Default"/>。
    /// 调用方负责 UI 线程编排，本方法可在 Task.Run 中执行。
    /// 拉到的列表会再附加上 <see cref="MoesekaiSources"/>（去重，按 SourceName 区分），
    /// 这样 Moesekai 数据源在没有更新远端 source.json 的情况下也能直接使用。
    /// </summary>
    public async Task<SourceData[]> FetchSourceListAsync(CancellationToken ct = default)
    {
        ApplyProxyAndSource();
        SourceData[] baseList;
        try
        {
            // 这里直接用本地 HttpClient (30s) 替代 Fetcher.Fetch，避免后者 10s 超时静默回退到 "{}"。
            using var http = new HttpClient(BuildHttpHandler()) { Timeout = TimeSpan.FromSeconds(30) };
            var resp = await http.GetAsync(SourceListUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                var list = JsonSerializer.Deserialize<SourceData[]>(json);
                baseList = list is { Length: > 0 } ? list : SourceData.Default;
            }
            else
            {
                baseList = SourceData.Default;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DownloadPageViewModel] FetchSourceList failed: {ex.Message}");
            baseList = SourceData.Default;
        }

        // Moesekai JP 排最前，Haruki 排在 Sekai Best 前，Moesekai CN 排最后（去重）
        var builtinNames = MoesekaiSources.Select(s => s.SourceName).Append(MoesekaiCn.SourceName).ToHashSet();
        var others = baseList.Where(s => !builtinNames.Contains(s.SourceName)).ToList();
        var haruki = others.Where(s => s.SourceName.Contains("Haruki", StringComparison.OrdinalIgnoreCase)).ToList();
        var rest = others.Where(s => !s.SourceName.Contains("Haruki", StringComparison.OrdinalIgnoreCase)).ToList();
        var merged = MoesekaiSources
            .Concat(haruki)
            .Concat(rest)
            .Append(MoesekaiCn)
            .ToArray();
        return merged;
    }

    /// <summary>最近一次 Refresh 的诊断信息（URL / HTTP / cache size），由 UI 显示在状态栏排查 0 条问题。</summary>
    public string LastDiagnostics { get; private set; } = string.Empty;

    /// <summary>
    /// 触发当前 Tab 对应的 List* 远端刷新（**仅刷新数据**，不操作 ObservableCollection）。
    /// 调用方需要在 UI 线程显式调用 <see cref="ApplyAfterRefresh"/> 或 <see cref="RebuildCandidates"/>。
    ///
    /// 实现说明：之所以**不**直接调 <see cref="BaseListStory.Refresh"/>，是因为 SekaiDataFetch 的
    /// <c>Fetcher.Fetch</c> 内部有几个会"静默吞错"的坑：
    /// <list type="bullet">
    /// <item><c>HttpClient.Timeout</c> 写死 10s，对 GitHub Pages / Exmeaning 等慢路径很容易超时；</item>
    /// <item><c>catch (HttpRequestException)</c> 不覆盖 <c>TaskCanceledException</c>，超时时会冒到外层；</item>
    /// <item>重试 5 次仍失败时<b>不抛异常</b>，而是默默返回 <c>"{}"</c>，写入 cache 文件就是 2 字节，</item>
    /// <item>下次 Load 反序列化失败 → ClearCache → Data 永远是空 → "上游数据共 0 条"。</item>
    /// </list>
    /// 修法：用本地 <see cref="HttpClient"/>（30s 超时、全异常 catch）取代 Fetcher.Fetch，
    /// 直接写 cache 文件，再反射调子类的 <c>Load()</c> 重新加载到 <c>Data</c>。
    /// </summary>
    public async Task RefreshCurrentTabAsync()
    {
        ApplyProxyAndSource();

        BaseListStory listInstance = StoryTypeIndex switch
        {
            0 => ListUnitStory.Instance,
            1 => ListEventStory.Instance,
            2 => ListSpecialStory.Instance,
            >= 3 and <= 6 => ListCardStory.Instance,
            >= 7 and <= 9 => ListActionStory.Instance,
            10 => ListGreetStory.Instance,
            _ => throw new InvalidOperationException("未知 StoryTypeIndex: " + StoryTypeIndex),
        };

        var probe = new List<string>();
        probe.Add($"源: {CurrentSource?.SourceName ?? "?"}");

        try
        {
            await FetchAndCacheViaReflectionAsync(listInstance, probe, (current, total, name) =>
            {
                Progress = (double)current / total;
                ProgressText = $"({current}/{total}) {name}";
            });
        }
        catch (Exception ex)
        {
            probe.Add($"刷新抛错: {ex.GetType().Name}: {ex.Message}");
            LastDiagnostics = string.Join(" | ", probe);
            throw;
        }

        LastDiagnostics = string.Join(" | ", probe);
    }

    /// <summary>
    /// 用反射读取 <see cref="BaseListStory"/> 子类上的 <c>[SourcePath]</c> / <c>[CachePath]</c> 属性，
    /// 然后用本地 HttpClient 直接 fetch + write cache，最后反射调 <c>Load()</c>。
    /// 直接使用选中源的 URL（Moesekai JP 等 JP 源本身就是日文数据）。
    /// </summary>
    private static async Task FetchAndCacheViaReflectionAsync(BaseListStory listInstance, List<string> probe, Action<int, int, string>? onProgress = null)
    {
        var type = listInstance.GetType();
        var props = type.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        var sources = new Dictionary<string, string>();
        var caches = new Dictionary<string, string>();
        foreach (var p in props)
        {
            var srcAttr = p.GetCustomAttribute<SourcePathAttribute>();
            var cacheAttr = p.GetCustomAttribute<CachePathAttribute>();
            if (srcAttr != null && p.GetValue(null) is string s) sources[srcAttr.Key] = s;
            if (cacheAttr != null && p.GetValue(null) is string c) caches[cacheAttr.Key] = c;
        }

        var keys = sources.Keys.Intersect(caches.Keys).OrderBy(k => k).ToList();
        if (keys.Count == 0)
        {
            probe.Add($"反射: {type.Name} 上没找到匹配的 SourcePath/CachePath 对");
            return;
        }

        using var http = new HttpClient(BuildHttpHandler()) { Timeout = TimeSpan.FromSeconds(120) };
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            onProgress?.Invoke(i + 1, keys.Count, key);
            var url = sources[key];
            var cachePath = caches[key];
            try
            {
                var resp = await http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    probe.Add($"{key}: HTTP {(int)resp.StatusCode}");
                    continue;
                }
                var body = await resp.Content.ReadAsStringAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                await File.WriteAllTextAsync(cachePath, body);
                var size = FormatSize(body.Length);
                onProgress?.Invoke(i + 1, keys.Count, $"{key} ({size})");
                probe.Add($"{key}: {body.Length}B");
            }
            catch (Exception ex)
            {
                probe.Add($"{key}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // 写完缓存后反射调 protected Load() 重新加载到 Data 字典
        var loadMethod = type.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        loadMethod?.Invoke(listInstance, null);
    }

    /// <summary>
    /// 刷新完成后在 UI 线程统一调用：填充 Special/Action 下拉项 + 重建候选列表。
    /// </summary>
    public void ApplyAfterRefresh()
    {
        ReloadSpecialTitles();
        ReloadAreas();
        RebuildCandidates();
    }

    /// <summary>当前 Tab 对应数据集中已有多少条记录（用于 UI 状态文案，便于排查）。</summary>
    public int CurrentTabDataCount => StoryTypeIndex switch
    {
        0 => SelectedUnit != null && ListUnitStory.Instance.Data.TryGetValue(SelectedUnit.Key, out var us)
                ? us.Chapters.Sum(c => c.Episodes.Length)
                : 0,
        1 => ListEventStory.Instance.Data.Count,
        2 => ListSpecialStory.Instance.Data.Count,
        >= 3 and <= 6 => ListCardStory.Instance.Data.Count,
        >= 7 and <= 9 => ListActionStory.Instance.Data.Count,
        10 => ListGreetStory.Instance.Data.Count,
        _ => 0,
    };

    /// <summary>从 Special 缓存重新填充下拉项；若用户已选择且仍存在，保持选中。</summary>
    public void ReloadSpecialTitles()
    {
        var preserved = SelectedSpecial;
        SpecialTitles.Clear();
        foreach (var key in ListSpecialStory.Instance.Data.Keys)
            SpecialTitles.Add(key);
        if (preserved != null && SpecialTitles.Contains(preserved))
            SelectedSpecial = preserved;
        else
            SelectedSpecial = SpecialTitles.FirstOrDefault();
    }

    /// <summary>从 Action 缓存重新填充区域下拉项；若用户已选择且仍存在，保持选中。</summary>
    public void ReloadAreas()
    {
        var preservedId = SelectedArea?.Id;
        Areas.Clear();
        foreach (var area in ListActionStory.Instance.Areas.OrderBy(a => a.Id))
            Areas.Add(area);
        SelectedArea = preservedId.HasValue
            ? Areas.FirstOrDefault(a => a.Id == preservedId.Value) ?? Areas.FirstOrDefault()
            : Areas.FirstOrDefault();
    }

    private void ReloadAreasForCurrentFilter()
    {
        if (!IsActionTab) return;
        var filter = StoryTypeIndex switch
        {
            7 => ActionFilter.Initial,
            8 => ActionFilter.Upgrade,
            _ => ActionFilter.Additional,
        };
        var preservedId = SelectedArea?.Id;
        Areas.Clear();
        var allAreas = ListActionStory.Instance.Areas;
        var filtered = filter switch
        {
            ActionFilter.Initial or ActionFilter.Upgrade => allAreas.Where(a => a.IsBaseArea),
            ActionFilter.Additional => allAreas.Where(a => !a.IsBaseArea),
            _ => allAreas,
        };
        foreach (var area in filtered.OrderBy(a => a.Id))
            Areas.Add(area);
        SelectedArea = preservedId.HasValue
            ? Areas.FirstOrDefault(a => a.Id == preservedId.Value) ?? Areas.FirstOrDefault()
            : Areas.FirstOrDefault();
    }

    /// <summary>把候选项加入下载列表（去重：同 url 不重复添加）。</summary>
    public void EnqueueCandidate(DownloadCandidate candidate)
    {
        if (candidate is null) return;
        if (CurrentSource is null) return;
        SourceList.Instance.SourceData = CurrentSource;

        var url = candidate.UrlBuilder();
        if (Tasks.Any(t => t.Url == url)) return;

        var tag = CurrentSource.SourceName + " - " + candidate.Title;
        Tasks.Add(new DownloadTaskItem(tag, url, candidate.ContentLength));
        OnPropertyChanged(nameof(TasksSummary));
        PersistHistory();
    }

    public void EnqueueAllCandidates()
    {
        foreach (var c in Candidates.ToList())
            EnqueueCandidate(c);
    }

    public void ClearTasks()
    {
        Tasks.Clear();
        OnPropertyChanged(nameof(TasksSummary));
        PersistHistory();
    }

    public string TasksSummary
    {
        get
        {
            var count = Tasks.Count;
            var totalBytes = Tasks.Sum(t => t.ContentLength);
            var saveDir = SettingsService.Instance.Current.DownloadDirectory;
            if (string.IsNullOrWhiteSpace(saveDir))
                saveDir = Path.Combine(ResourceManager.DataBaseDir, "Scripts");

            var parts = new List<string> { $"共 {count} 项" };
            if (totalBytes > 0)
                parts.Add($"总计 {FormatSize(totalBytes)}");

            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(saveDir));
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    parts.Add($"可用 {FormatSize(drive.AvailableFreeSpace)}");
                }
            }
            catch { /* ignore */ }

            return string.Join(" | ", parts);
        }
    }

    /// <summary>串行下载 <see cref="Tasks"/> 中所有未完成项；HttpClient 按当前代理设置构建。</summary>
    public async Task DownloadAllAsync(CancellationToken ct = default)
    {
        ApplyProxyAndSource();
        var saveDir = SettingsService.Instance.Current.DownloadDirectory;
        if (string.IsNullOrWhiteSpace(saveDir))
            saveDir = Path.Combine(ResourceManager.DataBaseDir, "Scripts");
        Directory.CreateDirectory(saveDir);

        using var http = new HttpClient(BuildHttpHandler())
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        var items = Tasks.ToList();
        var total = items.Count;
        var done = 0;

        foreach (var task in items)
        {
            if (ct.IsCancellationRequested) break;
            if (task.Status == DownloadStatus.Done) { done++; continue; }

            Progress = (double)done / total;
            ProgressText = $"({done}/{total}) {task.Tag}";
            task.Status = DownloadStatus.Downloading;
            try
            {
                var content = await http.GetStringAsync(task.Url, ct);
                var path = Path.Combine(saveDir, Path.GetFileName(task.Url));
                await File.WriteAllTextAsync(path, content, ct);
                task.SavePath = path;
                task.Status = DownloadStatus.Done;
                ProgressText = $"({done}/{total}) {task.Tag} ({FormatSize(content.Length)})";
                PersistHistory();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                task.LastError = AnnotateNetworkError(ex);
                task.Status = DownloadStatus.Failed;
                PersistHistory();
            }
            done++;
        }

        Progress = 1.0;
        ProgressText = $"完成 {done}/{total}";
        PersistHistory();
    }

    /// <summary>
    /// 把网络异常文案翻译为对用户更友好的提示，并在识别到代理相关错误时附加排查建议。
    /// 检测启发式：消息含 "127.0.0.1"/"积极拒绝"/"目标计算机"/"refused"/"proxy"/"timed out" 等 → 提示检查代理。
    /// </summary>
    private static string AnnotateNetworkError(Exception ex)
    {
        var msg = ex.Message;
        var inner = ex.InnerException?.Message ?? string.Empty;
        var combined = msg + " | " + inner;
        var looksLikeProxy = combined.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) >= 0
                             || combined.IndexOf("积极拒绝", StringComparison.OrdinalIgnoreCase) >= 0
                             || combined.IndexOf("目标计算机", StringComparison.OrdinalIgnoreCase) >= 0
                             || combined.IndexOf("refused", StringComparison.OrdinalIgnoreCase) >= 0
                             || combined.IndexOf("proxy", StringComparison.OrdinalIgnoreCase) >= 0
                             || combined.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;

        var s = SettingsService.Instance.Current;
        var proxyDesc = s.ProxyType switch
        {
            1 => $"当前代理: http://{s.ProxyHost}:{s.ProxyPort}",
            2 => $"当前代理: socks5://{s.ProxyHost}:{s.ProxyPort}",
            _ => "当前代理: 系统默认",
        };

        return looksLikeProxy
            ? $"{msg}\n   {proxyDesc}\n   请到 设置 → 代理 检查代理地址是否可用，或改为'系统默认'。"
            : msg;
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1_048_576 => $"{bytes / 1048576.0:F1}MB",
            >= 1024 => $"{bytes / 1024.0:F1}KB",
            _ => $"{bytes}B",
        };
    }

    /// <summary>
    /// 按当前 <see cref="AppSettings.ProxyType"/> 构造 HttpMessageHandler。
    ///
    /// 语义：
    /// <list type="bullet">
    /// <item><b>ProxyType=0（系统默认）</b>：使用 .NET 的 <c>HttpClientHandler</c> 默认行为，
    /// 在 Windows 上等价于跟随 IE / WinHTTP 系统代理；在 Linux/macOS 上读 <c>http_proxy</c> 等环境变量。</item>
    /// <item><b>ProxyType=1</b>：显式 http 代理。</item>
    /// <item><b>ProxyType=2</b>：显式 socks5 代理（用 SocketsHttpHandler）。</item>
    /// </list>
    /// 如用户想完全直连，需在系统层清掉代理设置；本应用不强制禁用，避免破坏用户的全局代理预期。
    /// </summary>
    internal static HttpMessageHandler BuildHttpHandler()
    {
        var s = SettingsService.Instance.Current;
        return s.ProxyType switch
        {
            1 => new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(s.ProxyHost, s.ProxyPort),
                UseProxy = true,
            },
            2 => new SocketsHttpHandler
            {
                Proxy = new System.Net.WebProxy(s.ProxyHost, s.ProxyPort),
                UseProxy = true,
            },
            _ => new HttpClientHandler(),
        };
    }

    private void LoadPersistedTasks()
    {
        _suppressHistoryPersistence = true;
        try
        {
            foreach (var snapshot in _historyService.Load())
                Tasks.Add(DownloadTaskItem.FromSnapshot(snapshot));
        }
        finally
        {
            _suppressHistoryPersistence = false;
        }
    }

    private void PersistHistory()
    {
        if (_suppressHistoryPersistence) return;
        _historyService.Save(Tasks.Select(item => item.ToSnapshot()));
    }
}

/// <summary>主线剧情中的某个团：键（light_sound 等）+ 中文显示名。
/// <see cref="IsSelected"/> 由 <see cref="DownloadPageViewModel.OnSelectedUnitChanged"/> 单向同步，
/// 用来给主线 tab 的 RadioButton.IsChecked 提供反向绑定（否则 SelectedUnit 初始化时 UI 不显示选中）。</summary>
public sealed partial class UnitOption : ObservableObject
{
    public string Key { get; }
    public string DisplayName { get; }
    /// <summary>团 logo avares:// URI（logo_{key}.png）；主线 RadioButton 渲染该图片。</summary>
    public string LogoUri { get; }
    [ObservableProperty] private bool _isSelected;

    public UnitOption(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
        LogoUri = $"avares://SekaiToolsApp/Assets/Unit/logo_{key}.png";
    }

    public override string ToString() => DisplayName;
}

/// <summary>角色剧情下的某个角色：CharacterId + 中文名。</summary>
public sealed class CharacterOption(int characterId, string displayName)
{
    public int CharacterId { get; } = characterId;
    public string DisplayName { get; } = displayName;
    public string DisplayWithId => $"{CharacterId:D2} - {DisplayName}";
    public override string ToString() => DisplayWithId;
}

/// <summary>
/// 活动 / 区域 tab 的 Banner 角色筛选：单个角色 checkbox。
/// </summary>
public sealed partial class CharacterCheckOption : ObservableObject
{
    public int CharacterId { get; }
    /// <summary>角色头像 avares:// URI（CharacterId → chr_{id}.png）。</summary>
    public string ImageUri { get; }
    [ObservableProperty] private bool _isChecked;

    public CharacterCheckOption(int characterId, string imageUri)
    {
        CharacterId = characterId;
        ImageUri = imageUri;
    }
}

/// <summary>
/// 活动 / 区域 tab 的 Banner 角色筛选：6 个团 row，含团 logo + 该团角色 checkbox 集合。
/// </summary>
public sealed partial class BannerUnitRow : ObservableObject
{
    public string UnitKey { get; }
    /// <summary>团 logo avares:// URI（logo_{unitKey}.png）。</summary>
    public string LogoUri { get; }
    /// <summary>团总 checkbox：勾上 = 本团角色全选；取消 = 本团全清。</summary>
    [ObservableProperty] private bool _isChecked;

    public ObservableCollection<CharacterCheckOption> Characters { get; } = new();

    public BannerUnitRow(string unitKey, string logoUri)
    {
        UnitKey = unitKey;
        LogoUri = logoUri;
    }
}

/// <summary>当前 Tab 选项下的一条候选下载项：标题 + URL 计算函数（延迟到加入任务时再求值）。</summary>
public sealed partial class DownloadCandidate(string title, Func<string> urlBuilder) : ObservableObject
{
    public string BaseTitle { get; } = title;
    public Func<string> UrlBuilder { get; } = urlBuilder;
    public long ContentLength { get; set; }
    [ObservableProperty] private string _sizeText = string.Empty;
    public string Title => string.IsNullOrEmpty(SizeText) ? BaseTitle : $"{BaseTitle} ({SizeText})";
    partial void OnSizeTextChanged(string value) => OnPropertyChanged(nameof(Title));
}

/// <summary>下载列表条目的持久化快照。</summary>
public sealed class DownloadTaskSnapshot
{
    public string Tag { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
    public string? SavePath { get; set; }
    public string? LastError { get; set; }
}

public enum DownloadStatus
{
    Pending,
    Downloading,
    Done,
    Failed,
}

/// <summary>下载列表中的一项；可在下载过程中变更状态以便 UI 颜色切换。</summary>
public partial class DownloadTaskItem : ObservableObject
{
    public DownloadTaskItem(string tag, string url, long contentLength = 0)
    {
        _tag = tag;
        _url = url;
        ContentLength = contentLength;
    }

    public long ContentLength { get; set; }
    [ObservableProperty] private string _tag;
    [ObservableProperty] private string _url;
    [ObservableProperty] private DownloadStatus _status = DownloadStatus.Pending;
    [ObservableProperty] private string? _savePath;
    [ObservableProperty] private string? _lastError;

    public bool IsPending => Status == DownloadStatus.Pending;
    public bool IsDownloading => Status == DownloadStatus.Downloading;
    public bool IsDone => Status == DownloadStatus.Done;
    public bool IsFailed => Status == DownloadStatus.Failed;
    public bool HasError => !string.IsNullOrEmpty(LastError);
    public bool HasSavePath => !string.IsNullOrWhiteSpace(SavePath);

    public string StatusText => Status switch
    {
        DownloadStatus.Pending => "待下载",
        DownloadStatus.Downloading => "下载中…",
        DownloadStatus.Done => "已完成",
        DownloadStatus.Failed => "失败",
        _ => string.Empty,
    };

    partial void OnStatusChanged(DownloadStatus value)
    {
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnSavePathChanged(string? value) => OnPropertyChanged(nameof(HasSavePath));
    partial void OnLastErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public static DownloadTaskItem FromSnapshot(DownloadTaskSnapshot snapshot)
    {
        return new DownloadTaskItem(snapshot.Tag, snapshot.Url)
        {
            Status = snapshot.Status,
            SavePath = snapshot.SavePath,
            LastError = snapshot.LastError,
        };
    }

    public DownloadTaskSnapshot ToSnapshot()
    {
        return new DownloadTaskSnapshot
        {
            Tag = Tag,
            Url = Url,
            Status = Status,
            SavePath = SavePath,
            LastError = LastError,
        };
    }
}
