using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SekaiToolsApp.Services;
using SekaiToolsPlatform.Models;
using SekaiToolsPlatform.Services;
using SekaiToolsPlatform.ViewModels;

namespace SekaiToolsApp.Views.Pages;

/// <summary>
/// 翻译工作台 Avalonia 视图。
///
/// 取代了之前的 PlaceholderPage 占位实现，UI 1:1 平移自
/// SekaiToolsMauiText.View.Translate.TranslatePage，同时把数据层指向
/// SekaiToolsPlatform 项目里的 PlatformSessionService / PlatformStoryService /
/// LocalTranslationWorkspaceService（M2.1 新建）。
///
/// 当前版本未引入 DI 容器：三个 service 在 ctor 内由共享 <see cref="JsonFilePlatformPreferences"/>
/// 创建一次。等以后整体改 DI（M3 之后）再统一抽出 ServiceProvider。
/// </summary>
public partial class TranslatePageView : UserControl
{
    private readonly TranslatePageModel _viewModel = new();
    private readonly SekaiPlatformClient _platformClient;
    private readonly PlatformSessionService _sessionService;
    private readonly PlatformStoryService _storyService;
    private readonly LocalTranslationWorkspaceService _localWorkspaceService = new();
    private readonly TranslateRecoveryService _recoveryService = TranslateRecoveryService.Instance;
    private readonly DispatcherTimer _recoveryTimer = new()
    {
        Interval = TimeSpan.FromSeconds(30),
    };

    private bool _sessionInitialized;
    private bool _recoveryRestoreChecked;
    private bool _recoverySaveInProgress;
    private string _scriptPath = string.Empty;
    private string _translationPath = string.Empty;

    public TranslatePageView()
    {
        _platformClient = new SekaiPlatformClient(JsonFilePlatformPreferences.Instance);
        _sessionService = new PlatformSessionService(_platformClient);
        _storyService = new PlatformStoryService(_platformClient);

        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.BaseUrl = _sessionService.GetBaseUrl();

        Loaded += OnLoaded;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _recoveryTimer.Tick += OnRecoveryTimerTick;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _recoveryTimer.Start();
        if (_sessionInitialized) return;
        _sessionInitialized = true;
        try
        {
            await Task.Yield();
            await TryRestoreRecoveryAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await RefreshSessionAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 初始会话刷新超时不算失败：UI 先可用，用户后续可手动刷新 / 登录。
        }
        catch
        {
            // 启动时若没有有效会话（首次使用 / 服务器不可达）静默忽略，UI 通过 SessionText 提示用户。
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _recoveryTimer.Stop();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TranslatePageModel.HasEvents) ||
            e.PropertyName == nameof(TranslatePageModel.IsEmpty))
        {
            UpdateLocalToolbarVisibility();
        }
    }

    private void UpdateLocalToolbarVisibility()
    {
        var canUseLocalTranslation = _viewModel.HasEvents && !string.IsNullOrWhiteSpace(_scriptPath);
        var loadTranslationBtn = this.FindControl<Button>("LoadTranslationButton");
        var loadReviewBtn = this.FindControl<Button>("LoadReviewButton");
        if (loadTranslationBtn is not null) loadTranslationBtn.IsVisible = canUseLocalTranslation;
        if (loadReviewBtn is not null) loadReviewBtn.IsVisible = canUseLocalTranslation;
    }

    #region 会话管理

    private async Task RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await _sessionService.RefreshSessionAsync(_viewModel.BaseUrl, cancellationToken);
        _viewModel.SetPlatformSession(session);
        if (session.CurrentTenant is not null)
        {
            await LoadStoryTypesAsync(cancellationToken);
        }
    }

    private async Task LoadStoryTypesAsync(CancellationToken cancellationToken = default)
    {
        if (_viewModel.SelectedTenant is null)
        {
            _viewModel.SetStoryTypes(Array.Empty<string>());
            return;
        }

        var storyTypes = await _storyService.GetStoryTypesAsync(cancellationToken);
        _viewModel.SetStoryTypes(storyTypes);
        if (!string.IsNullOrWhiteSpace(_viewModel.SelectedStoryType) || storyTypes.Count == 0) return;
        _viewModel.SelectedStoryType = storyTypes[0];
    }

    private async Task LoadStoryGroupsAsync()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedStoryType))
        {
            await ShowErrorAsync("请先选择剧情类型");
            return;
        }

        var groups = await _storyService.GetStoryGroupsAsync(_viewModel.SelectedStoryType);
        _viewModel.SetStoryGroups(groups);
        if (_viewModel.SelectedStoryGroup is not null)
        {
            await LoadStoriesAsync();
        }
    }

    private async Task LoadStoriesAsync()
    {
        if (_viewModel.SelectedStoryGroup is null)
        {
            await ShowErrorAsync("请先选择剧情集");
            return;
        }

        var stories = await _storyService.GetStoriesAsync(_viewModel.SelectedStoryGroup.Id);
        _viewModel.SetStories(stories);
    }

    private async Task LoadTranslationVersionsAsync(long? preferredTranslationVersionId = null)
    {
        if (_viewModel.SelectedStory is null) return;
        var versions = await _storyService.GetTranslationVersionsAsync(_viewModel.SelectedStory.Id);
        _viewModel.SetTranslationVersions(versions, preferredTranslationVersionId);
    }

    private async void OnPlatformLoginClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var session = await _sessionService.LoginAsync(
                _viewModel.BaseUrl, _viewModel.Username, _viewModel.Password);
            _viewModel.SetPlatformSession(session);
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

    private async void OnRefreshSessionClicked(object? sender, RoutedEventArgs e)
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

    private async void OnSwitchTenantClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTenant is null)
        {
            await ShowErrorAsync("请先选择租户");
            return;
        }
        try
        {
            var session = await _sessionService.SwitchTenantAsync(_viewModel.SelectedTenant.Id);
            _viewModel.SetPlatformSession(session);
            await LoadStoryTypesAsync();
            await ShowInfoAsync("租户已切换");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void OnPlatformLogoutClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _sessionService.LogoutAsync();
        }
        catch
        {
            // 后端 logout 失败不影响前端状态清理：本地照样登出。
        }
        _viewModel.ClearPlatformSession();
        await ShowInfoAsync("已登出 SekaiPlatform");
    }

    #endregion

    #region 平台剧情

    private async void OnRefreshStoryTypesClicked(object? sender, RoutedEventArgs e)
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

    private async void OnLoadStoryGroupsClicked(object? sender, RoutedEventArgs e)
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

    private async void OnStoryGroupChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedStoryGroup is null) return;
        try
        {
            await LoadStoriesAsync();
        }
        catch
        {
            // 选 group 触发的隐式拉取，失败留给用户手动操作时再提示。
        }
    }

    private async void OnStoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedStory is null) return;
        try
        {
            await LoadTranslationVersionsAsync();
        }
        catch
        {
            // 同上。
        }
    }

    private async void OnLoadPlatformStoryClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedStory is null)
        {
            await ShowErrorAsync("请先选择剧情");
            return;
        }
        try
        {
            var story = _viewModel.SelectedStory;
            var sourceLines = await _storyService.GetStorySourceLinesAsync(story.Id);
            _viewModel.SetPlatformStory(story, sourceLines);
            await LoadTranslationVersionsAsync();
            _scriptPath = string.Empty;
            _translationPath = string.Empty;
            UpdateLocalToolbarVisibility();
            _recoveryService.Clear();
            await ShowInfoAsync($"已载入平台剧情：{story.Title}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void OnLoadPlatformTranslationClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTranslationVersion is null)
        {
            await ShowErrorAsync("请先选择平台译文版本");
            return;
        }
        try
        {
            var lines = await _storyService.GetTranslationLinesAsync(_viewModel.SelectedTranslationVersion.Id);
            _viewModel.ApplyPlatformTranslation(lines);
            _recoveryService.Clear();
            await ShowInfoAsync("已载入平台译文版本");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void OnLoadPlatformReferenceClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTranslationVersion is null)
        {
            await ShowErrorAsync("请先选择平台译文版本");
            return;
        }
        try
        {
            var lines = await _storyService.GetTranslationLinesAsync(_viewModel.SelectedTranslationVersion.Id);
            _viewModel.ApplyPlatformReference(lines);
            _recoveryService.Clear();
            await ShowInfoAsync("已载入平台对照版本");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    #endregion

    #region 本地

    private async void OnLoadScriptClicked(object? sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync("剧本文件", new[] { "*.json", "*.asset" });
        if (path is null) return;

        try
        {
            var story = _localWorkspaceService.LoadStory(path);
            _scriptPath = path;
            _viewModel.SetLocalStory(story, Path.GetFileName(path));
            _translationPath = string.Empty;
            UpdateLocalToolbarVisibility();
            _recoveryService.Clear();
            await ShowInfoAsync("成功载入剧本");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void OnLoadTranslationClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsEmpty || string.IsNullOrWhiteSpace(_scriptPath))
        {
            await ShowErrorAsync("请先载入本地剧本");
            return;
        }
        var path = await PickFileAsync("翻译文件", new[] { "*.txt" });
        if (path is null) return;
        try
        {
            _translationPath = path;
            var story = _localWorkspaceService.LoadStoryWithTranslation(_scriptPath, path);
            _viewModel.SetLocalStory(story, Path.GetFileName(_scriptPath));
            UpdateLocalToolbarVisibility();
            _recoveryService.Clear();
            await ShowInfoAsync("成功载入翻译");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void OnLoadReviewClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsEmpty || string.IsNullOrWhiteSpace(_scriptPath))
        {
            await ShowErrorAsync("请先载入本地剧本");
            return;
        }
        var path = await PickFileAsync("对照翻译文件", new[] { "*.txt" });
        if (path is null) return;
        try
        {
            _viewModel.ApplyReference(_localWorkspaceService.LoadReferenceStory(_scriptPath, path));
            _recoveryService.Clear();
            await ShowInfoAsync("成功载入对照翻译");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        _viewModel.Clear();
        _scriptPath = string.Empty;
        _translationPath = string.Empty;
        UpdateLocalToolbarVisibility();
        _recoveryService.Clear();
    }

    #endregion

    #region 保存 / 上传

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsEmpty)
        {
            await ShowErrorAsync("请先载入剧情");
            return;
        }

        var defaultName = !string.IsNullOrWhiteSpace(_translationPath)
            ? Path.GetFileName(_translationPath)
            : !string.IsNullOrWhiteSpace(_scriptPath)
                ? Path.GetFileNameWithoutExtension(_scriptPath) + ".txt"
                : SanitizeFileName(_viewModel.CurrentDocumentTitle) + ".txt";

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存翻译文件",
            SuggestedFileName = defaultName,
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("文本文件") { Patterns = ["*.txt"] },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] },
            ],
        });
        var local = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(local)) return;

        try
        {
            await _localWorkspaceService.SaveTranslationAsync(local, _viewModel.Result);
            _viewModel.MarkWorkspaceSaved();
            _translationPath = local;
            _recoveryService.Clear();
            await ShowInfoAsync($"翻译文件已保存到：\n{local}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"保存失败：{ex.Message}");
        }
    }

    private async void OnUploadClicked(object? sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanUploadToPlatform)
        {
            await ShowErrorAsync("当前内容不可上传到平台");
            return;
        }

        var title = await ShowPromptAsync("上传到 SekaiPlatform", "请输入本次翻译版本标题：",
            _viewModel.CurrentDocumentTitle);
        if (title is null) return;

        try
        {
            var request = new PlatformCreateTranslationVersionRequest(
                title, _viewModel.BuildPlatformTranslationLines());
            var result = await _storyService.CreateTranslationVersionAsync(
                _viewModel.CurrentPlatformStoryId, request);
            _viewModel.MarkWorkspaceSaved();
            _recoveryService.Clear();
            await LoadTranslationVersionsAsync(result.Version.Id);
            await ShowInfoAsync($"已上传到平台，生成版本 v{result.Version.VersionNo}，共 {result.LineCount} 行");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    #endregion

    #region 工具方法

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "translation";
        foreach (var c in Path.GetInvalidFileNameChars()) input = input.Replace(c, '_');
        return input;
    }

    private async Task<string?> PickFileAsync(string title, string[] patterns)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"选择{title}",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(title) { Patterns = patterns },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] },
            ],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private async Task ShowInfoAsync(string message) => await ShowMessageAsync("成功", message);
    private async Task ShowErrorAsync(string message) => await ShowMessageAsync("错误", message);

    private async Task ShowMessageAsync(string title, string message)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var ok = new Button
        {
            Content = "确定",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Right,
            Classes = { "accent" },
            IsDefault = true,
        };
        ok.Click += (_, _) => dialog.Close();
        stack.Children.Add(ok);
        dialog.Content = stack;
        await dialog.ShowDialog(owner);
    }

    private async Task<string?> ShowPromptAsync(string title, string message, string initialValue)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return null;

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var textBox = new TextBox { Text = initialValue };
        string? result = null;
        var ok = new Button { Content = "确定", Classes = { "accent" }, IsDefault = true };
        var cancel = new Button { Content = "取消", IsCancel = true };
        ok.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) { result = textBox.Text; dialog.Close(); }
        };

        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        stack.Children.Add(textBox);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        stack.Children.Add(buttons);
        dialog.Content = stack;
        await dialog.ShowDialog(owner);
        return result;
    }

    private async Task TryRestoreRecoveryAsync()
    {
        if (_recoveryRestoreChecked) return;
        _recoveryRestoreChecked = true;

        var recovery = _recoveryService.Load();
        if (recovery is null)
        {
            if (_recoveryService.HasRecovery)
                _recoveryService.Clear();
            return;
        }

        var restore = await ShowRecoveryPromptAsync(recovery);
        if (!restore)
        {
            _recoveryService.Clear();
            return;
        }

        try
        {
            if (string.Equals(recovery.Mode, TranslateRecoveryService.PlatformMode, StringComparison.OrdinalIgnoreCase) &&
                recovery.PlatformStory is not null &&
                recovery.SourceLines.Length > 0)
            {
                _viewModel.SetPlatformStory(recovery.PlatformStory, recovery.SourceLines);
                _viewModel.ApplyPlatformTranslation(recovery.TranslationLines);
                _viewModel.MarkWorkspaceDirty();
                _scriptPath = string.Empty;
                _translationPath = string.Empty;
                UpdateLocalToolbarVisibility();
                await SaveRecoveryAsync();
                await ShowInfoAsync("已恢复上次未保存的平台翻译");
                return;
            }

            if (string.Equals(recovery.Mode, TranslateRecoveryService.LocalMode, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(recovery.ScriptPath) &&
                File.Exists(recovery.ScriptPath))
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"sekaitools-translate-recovery-{Guid.NewGuid():N}.txt");
                try
                {
                    await File.WriteAllTextAsync(tempPath, recovery.LocalResult ?? string.Empty);
                    var story = _localWorkspaceService.LoadStoryWithTranslation(recovery.ScriptPath, tempPath);
                    _viewModel.SetLocalStory(story, recovery.DocumentTitle ?? Path.GetFileName(recovery.ScriptPath));
                    _viewModel.MarkWorkspaceDirty();
                    _scriptPath = recovery.ScriptPath;
                    _translationPath = recovery.TranslationPath ?? string.Empty;
                    UpdateLocalToolbarVisibility();
                    await SaveRecoveryAsync();
                    await ShowInfoAsync("已恢复上次未保存的本地翻译");
                    return;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch
                    {
                        // 临时恢复文件清理失败不影响主流程。
                    }
                }
            }

            _recoveryService.Clear();
            await ShowErrorAsync("恢复失败：恢复文件缺少必要上下文。");
        }
        catch (Exception ex)
        {
            _recoveryService.Clear();
            await ShowErrorAsync("恢复失败：" + ex.Message);
        }
    }

    private async Task<bool> ShowRecoveryPromptAsync(TranslateRecoveryData recovery)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return false;

        var dialog = new Window
        {
            Title = "恢复未保存的更改",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var modeText = string.Equals(recovery.Mode, TranslateRecoveryService.PlatformMode, StringComparison.OrdinalIgnoreCase)
            ? "平台模式"
            : "本地模式";
        var detail = string.IsNullOrWhiteSpace(recovery.DocumentTitle)
            ? modeText
            : $"{modeText} · {recovery.DocumentTitle}";

        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = $"检测到上次未保存的翻译（{detail}，{recovery.SavedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}）。是否恢复？",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var discard = new Button { Content = "丢弃", IsCancel = true };
        var restore = new Button { Content = "恢复", Classes = { "accent" }, IsDefault = true };
        var result = false;

        discard.Click += (_, _) => dialog.Close();
        restore.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        buttons.Children.Add(discard);
        buttons.Children.Add(restore);
        stack.Children.Add(buttons);
        dialog.Content = stack;
        await dialog.ShowDialog(owner);
        return result;
    }

    private Task SaveRecoveryAsync()
    {
        if (_recoverySaveInProgress) return Task.CompletedTask;
        if (_viewModel.IsEmpty || !_viewModel.IsWorkspaceDirty) return Task.CompletedTask;

        _recoverySaveInProgress = true;
        try
        {
            _recoveryService.Save(_viewModel, _scriptPath, _translationPath);
        }
        catch
        {
            // recovery 只是兜底缓存，写失败不影响主流程。
        }
        finally
        {
            _recoverySaveInProgress = false;
        }

        return Task.CompletedTask;
    }

    private async void OnRecoveryTimerTick(object? sender, EventArgs e)
    {
        await SaveRecoveryAsync();
    }

    #endregion
}
