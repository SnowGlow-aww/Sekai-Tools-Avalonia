using SekaiToolsBase.Story;
using SekaiToolsMauiText.Models;
using SekaiToolsMauiText.Services;
using SekaiToolsMauiText.ViewModel;

namespace SekaiToolsMauiText.View.Translate;

public partial class TranslatePage : ContentPage
{
    private readonly PlatformSessionService _platformSessionService;
    private readonly PlatformStoryService _platformStoryService;
    private readonly LocalTranslationWorkspaceService _localWorkspaceService;
    private bool _sessionInitialized;
    private string _scriptPath = "";
    private string _translationPath = "";

    public TranslatePage(
        TranslatePageModel viewModel,
        PlatformSessionService platformSessionService,
        PlatformStoryService platformStoryService,
        LocalTranslationWorkspaceService localWorkspaceService)
    {
        _platformSessionService = platformSessionService;
        _platformStoryService = platformStoryService;
        _localWorkspaceService = localWorkspaceService;
        InitializeComponent();
        BindingContext = viewModel;
        ViewModel.BaseUrl = _platformSessionService.GetBaseUrl();
    }

    private TranslatePageModel ViewModel => (TranslatePageModel)BindingContext;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_sessionInitialized) return;
        _sessionInitialized = true;
        try
        {
            await RefreshSessionAsync();
        }
        catch
        {
            // ignore initial session failure
        }
    }

    private void UpdateToolbarVisibility()
    {
        var hasEvents = !ViewModel.IsEmpty;
        var canUseLocalTranslation = hasEvents && !string.IsNullOrWhiteSpace(_scriptPath);
        LoadTranslationButton.IsVisible = canUseLocalTranslation;
        LoadReviewButton.IsVisible = canUseLocalTranslation;
    }

    private async Task RefreshSessionAsync()
    {
        var session = await _platformSessionService.RefreshSessionAsync(ViewModel.BaseUrl);
        ViewModel.SetPlatformSession(session);
        if (session.CurrentTenant is not null)
        {
            await LoadStoryTypesAsync();
        }
    }

    private async Task LoadStoryTypesAsync()
    {
        if (ViewModel.SelectedTenant is null)
        {
            ViewModel.SetStoryTypes(Array.Empty<string>());
            return;
        }

        var storyTypes = await _platformStoryService.GetStoryTypesAsync();
        ViewModel.SetStoryTypes(storyTypes);
        if (!string.IsNullOrWhiteSpace(ViewModel.SelectedStoryType) || storyTypes.Count == 0) return;
        ViewModel.SelectedStoryType = storyTypes[0];
    }

    private async Task LoadStoryGroupsAsync()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.SelectedStoryType))
        {
            await ShowErrorAsync("请先选择剧情类型");
            return;
        }

        var groups = await _platformStoryService.GetStoryGroupsAsync(ViewModel.SelectedStoryType);
        ViewModel.SetStoryGroups(groups);
        if (ViewModel.SelectedStoryGroup is not null)
        {
            await LoadStoriesAsync();
        }
    }

    private async Task LoadStoriesAsync()
    {
        if (ViewModel.SelectedStoryGroup is null)
        {
            await ShowErrorAsync("请先选择剧情集");
            return;
        }

        var stories = await _platformStoryService.GetStoriesAsync(ViewModel.SelectedStoryGroup.Id);
        ViewModel.SetStories(stories);
    }

    private async Task LoadTranslationVersionsAsync()
    {
        if (ViewModel.SelectedStory is null) return;
        var versions = await _platformStoryService.GetTranslationVersionsAsync(ViewModel.SelectedStory.Id);
        ViewModel.SetTranslationVersions(versions);
    }

    private async void PlatformLoginButton_OnClick(object sender, EventArgs e)
    {
        try
        {
            var session = await _platformSessionService.LoginAsync(
                ViewModel.BaseUrl,
                ViewModel.Username,
                ViewModel.Password);
            ViewModel.SetPlatformSession(session);
            if (session.CurrentTenant is not null)
            {
                await LoadStoryTypesAsync();
            }
            await ShowInfoAsync("已连接 SekaiPlatform");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void RefreshSessionButton_OnClick(object sender, EventArgs e)
    {
        try
        {
            await RefreshSessionAsync();
            await ShowInfoAsync("会话已刷新");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void SwitchTenantButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.SelectedTenant is null)
        {
            await ShowErrorAsync("请先选择租户");
            return;
        }

        try
        {
            var session = await _platformSessionService.SwitchTenantAsync(ViewModel.SelectedTenant.Id);
            ViewModel.SetPlatformSession(session);
            await LoadStoryTypesAsync();
            await ShowInfoAsync("租户已切换");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void PlatformLogoutButton_OnClick(object sender, EventArgs e)
    {
        try
        {
            await _platformSessionService.LogoutAsync();
        }
        catch
        {
            // ignore logout failure and clear local session state
        }

        ViewModel.ClearPlatformSession();
        await ShowInfoAsync("已登出 SekaiPlatform");
    }

    private async void RefreshStoryTypesButton_OnClick(object sender, EventArgs e)
    {
        try
        {
            await LoadStoryTypesAsync();
            await ShowInfoAsync("剧情类型已刷新");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void LoadStoryGroupsButton_OnClick(object sender, EventArgs e)
    {
        try
        {
            await LoadStoryGroupsAsync();
            await ShowInfoAsync("剧情集已加载");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void StoryGroupPicker_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedStoryGroup is null) return;
        try
        {
            await LoadStoriesAsync();
        }
        catch
        {
            // interactive refresh only
        }
    }

    private async void StoryPicker_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedStory is null) return;
        try
        {
            await LoadTranslationVersionsAsync();
        }
        catch
        {
            // interactive refresh only
        }
    }

    private async void LoadPlatformStoryButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.SelectedStory is null)
        {
            await ShowErrorAsync("请先选择剧情");
            return;
        }

        try
        {
            var story = ViewModel.SelectedStory;
            var sourceLines = await _platformStoryService.GetStorySourceLinesAsync(story.Id);
            ViewModel.SetPlatformStory(story, sourceLines);
            await LoadTranslationVersionsAsync();
            _scriptPath = "";
            _translationPath = "";
            UpdateToolbarVisibility();
            await ShowInfoAsync($"已载入平台剧情：{story.Title}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void LoadPlatformTranslationButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.SelectedTranslationVersion is null)
        {
            await ShowErrorAsync("请先选择平台译文版本");
            return;
        }

        try
        {
            var lines = await _platformStoryService.GetTranslationLinesAsync(ViewModel.SelectedTranslationVersion.Id);
            ViewModel.ApplyPlatformTranslation(lines);
            await ShowInfoAsync("已载入平台译文版本");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void LoadPlatformReferenceButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.SelectedTranslationVersion is null)
        {
            await ShowErrorAsync("请先选择平台译文版本");
            return;
        }

        try
        {
            var lines = await _platformStoryService.GetTranslationLinesAsync(ViewModel.SelectedTranslationVersion.Id);
            ViewModel.ApplyPlatformReference(lines);
            await ShowInfoAsync("已载入平台对照版本");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void LoadScriptButton_OnClick(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择剧本文件",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".json", ".asset"] },
                    { DevicePlatform.Android, ["application/json", "*/*"] },
                    { DevicePlatform.iOS, ["public.json", "public.data"] },
                    { DevicePlatform.MacCatalyst, ["public.json", "public.data"] }
                })
            });

            if (result == null) return;

            Story story;
            try
            {
                story = _localWorkspaceService.LoadStory(result.FullPath);
                _scriptPath = result.FullPath;
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
                return;
            }

            ViewModel.SetLocalStory(story, Path.GetFileName(result.FullPath));
            _translationPath = "";
            UpdateToolbarVisibility();
            await ShowInfoAsync("成功载入剧本");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void LoadTranslationButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.IsEmpty || string.IsNullOrWhiteSpace(_scriptPath))
        {
            await ShowErrorAsync("请先载入本地剧本");
            return;
        }

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择翻译文件",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".txt"] },
                    { DevicePlatform.Android, ["text/plain"] },
                    { DevicePlatform.iOS, ["public.plain-text"] },
                    { DevicePlatform.MacCatalyst, ["public.plain-text"] }
                })
            });

            if (result == null) return;

            _translationPath = result.FullPath;
            ViewModel.SetLocalStory(
                _localWorkspaceService.LoadStoryWithTranslation(_scriptPath, result.FullPath),
                Path.GetFileName(_scriptPath));
            UpdateToolbarVisibility();
            await ShowInfoAsync("成功载入翻译");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void LoadReviewButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.IsEmpty || string.IsNullOrWhiteSpace(_scriptPath))
        {
            await ShowErrorAsync("请先载入本地剧本");
            return;
        }

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择对照翻译文件",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".txt"] },
                    { DevicePlatform.Android, ["text/plain"] },
                    { DevicePlatform.iOS, ["public.plain-text"] },
                    { DevicePlatform.MacCatalyst, ["public.plain-text"] }
                })
            });

            if (result == null) return;

            ViewModel.ApplyReference(_localWorkspaceService.LoadReferenceStory(_scriptPath, result.FullPath));
            await ShowInfoAsync("成功载入对照翻译");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void ResetButton_OnClick(object sender, EventArgs e)
    {
        ViewModel.Clear();
        _scriptPath = "";
        _translationPath = "";
        UpdateToolbarVisibility();
    }

    private async void SaveButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.IsEmpty)
        {
            await ShowErrorAsync("请先载入剧情");
            return;
        }

        var defaultName = !string.IsNullOrWhiteSpace(_translationPath)
            ? _translationPath
            : !string.IsNullOrWhiteSpace(_scriptPath)
                ? Path.ChangeExtension(_scriptPath, ".txt")
                : $"{SanitizeFileName(ViewModel.CurrentDocumentTitle)}.txt";

        var filePath = await DisplayPromptAsync(
            "保存翻译文件",
            "文件将保存到（可修改路径）：",
            "保存",
            "取消",
            initialValue: defaultName);

        if (filePath == null) return;

        try
        {
            await _localWorkspaceService.SaveTranslationAsync(filePath, ViewModel.Result);
            _translationPath = filePath;
            await ShowInfoAsync($"翻译文件已保存到：\n{filePath}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"保存失败：{ex.Message}");
        }
    }

    private async void UploadButton_OnClick(object sender, EventArgs e)
    {
        if (!ViewModel.CanUploadToPlatform)
        {
            await ShowErrorAsync("当前内容不可上传到平台");
            return;
        }

        var title = await DisplayPromptAsync(
            "上传到 SekaiPlatform",
            "请输入本次翻译版本标题：",
            "上传",
            "取消",
            initialValue: ViewModel.CurrentDocumentTitle);
        if (title == null) return;

        try
        {
            var request = new PlatformCreateTranslationVersionRequest(
                title,
                ViewModel.BuildPlatformTranslationLines());
            var result = await _platformStoryService.CreateTranslationVersionAsync(ViewModel.CurrentPlatformStoryId, request);
            await LoadTranslationVersionsAsync();
            await ShowInfoAsync($"已上传到平台，生成版本 v{result.Version.VersionNo}，共 {result.LineCount} 行");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private Task ShowInfoAsync(string message)
    {
        return DisplayAlert("成功", message, "确定");
    }

    private Task ShowErrorAsync(string message)
    {
        return DisplayAlert("错误", message, "确定");
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "translation";
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }

        return input;
    }
}
