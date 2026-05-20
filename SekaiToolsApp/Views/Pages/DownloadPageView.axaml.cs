using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SekaiDataFetch.Source;
using SekaiToolsApp.Services;
using SekaiToolsApp.ViewModels;

namespace SekaiToolsApp.Views.Pages;

/// <summary>
/// 数据下载主页面 code-behind（M1 MVP）。
///
/// 仅承担 UI 编排（线程切换、对话框、点击路由），数据/网络逻辑全部下沉到
/// <see cref="DownloadPageViewModel"/>。Event/Special/Card/Action 4 个标签页留给后续子任务。
/// </summary>
public partial class DownloadPageView : UserControl
{
    private readonly DownloadPageViewModel _vm = new();
    private CancellationTokenSource? _downloadCts;

    public DownloadPageView()
    {
        InitializeComponent();
        DataContext = _vm;

        // 进入页面后异步拉取远端 source.json，失败回退到默认源。
        _ = InitializeSourceListAsync();
    }

    private async Task InitializeSourceListAsync()
    {
        try
        {
            _vm.StatusMessage = "正在获取数据源列表…";
            var list = await _vm.FetchSourceListAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.Sources = list;
                _vm.CurrentSourceIndex = 0;
                _vm.StatusMessage = string.Empty;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.StatusMessage = "数据源加载失败：" + ex.Message;
            });
        }
    }

    private async void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy) return;

        _vm.IsRefreshing = true;
        var tabName = DownloadPageViewModel.StoryTypeNames[_vm.StoryTypeIndex];
        _vm.StatusMessage = $"正在刷新 {tabName}（首次需要拉取远端，可能需要数十秒）…";
        try
        {
            // 远端拉取放后台线程；UI 重建（ObservableCollection）必须回主线程。
            await Task.Run(() => _vm.RefreshCurrentTabAsync());
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.ApplyAfterRefresh();
                _vm.StatusMessage =
                    $"{tabName} 刷新完成：上游数据共 {_vm.CurrentTabDataCount} 条，过滤后展示 {_vm.Candidates.Count} 条。\n" +
                    $"诊断: {_vm.LastDiagnostics}";
            });
        }
        catch (Exception ex)
        {
            // 把内层异常摘要也带上，便于排查源地址 / 反序列化 / 网络代理问题。
            var inner = ex.InnerException;
            var detail = inner != null ? $" → {inner.GetType().Name}: {inner.Message}" : string.Empty;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.StatusMessage = $"刷新失败：{ex.GetType().Name}: {ex.Message}{detail}\n诊断: {_vm.LastDiagnostics}";
            });
            Console.Error.WriteLine("[DownloadPage] Refresh exception: " + ex);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.IsRefreshing = false;
            });
        }
    }

    private void OnUnitRadioChecked(object? sender, RoutedEventArgs e)
    {
        // RadioButton.Click 也可在已选中再次点击时触发；这里直接用当前 IsChecked 过滤。
        if (sender is RadioButton { IsChecked: true, Tag: UnitOption option })
        {
            _vm.SelectedUnit = option;
        }
    }

    private void OnBannerSelectAllClicked(object? sender, RoutedEventArgs e)
        => _vm.SetAllBannerChecked(true);

    private void OnBannerClearAllClicked(object? sender, RoutedEventArgs e)
        => _vm.SetAllBannerChecked(false);

    private void OnEnqueueClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadCandidate candidate })
        {
            _vm.EnqueueCandidate(candidate);
        }
    }

    private void OnEnqueueAllClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy) return;
        var before = _vm.Tasks.Count;
        _vm.EnqueueAllCandidates();
        var added = _vm.Tasks.Count - before;
        _vm.StatusMessage = added == 0
            ? "没有新的项目被加入（可能列表为空或全都已在下载列表中）。"
            : $"已加入 {added} 项到下载列表（去重后）。";
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsDownloading) return;
        _vm.ClearTasks();
    }

    private void OnOpenDownloadDirectoryClicked(object? sender, RoutedEventArgs e)
    {
        var dir = SettingsService.Instance.Current.DownloadDirectory;
        if (string.IsNullOrWhiteSpace(dir))
            return;
        FileManagerService.OpenFolder(dir);
    }

    private void OnOpenSavePathClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DownloadTaskItem item }) return;
        var path = item.SavePath;
        if (string.IsNullOrWhiteSpace(path)) return;
        FileManagerService.RevealPath(path);
    }

    private async void OnDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy) return;
        if (_vm.Tasks.Count == 0)
        {
            _vm.StatusMessage = "下载列表为空，请先选择并加入要下载的项。";
            return;
        }

        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        _vm.IsDownloading = true;
        _vm.StatusMessage = "正在下载…";
        try
        {
            await _vm.DownloadAllAsync(_downloadCts.Token);
            var done = 0;
            var failed = 0;
            string? firstError = null;
            foreach (var t in _vm.Tasks)
            {
                if (t.Status == DownloadStatus.Done) done++;
                else if (t.Status == DownloadStatus.Failed)
                {
                    failed++;
                    firstError ??= t.LastError;
                }
            }
            if (failed == 0)
            {
                _vm.StatusMessage = $"下载完成：{done} 项成功。";
            }
            else
            {
                var tail = string.IsNullOrEmpty(firstError) ? string.Empty : $"\n首条失败原因: {firstError}";
                _vm.StatusMessage = $"下载结束：{done} 项成功，{failed} 项失败（可点击 “下载” 重试失败项）。{tail}";
            }
        }
        catch (OperationCanceledException)
        {
            _vm.StatusMessage = "下载已取消。";
        }
        catch (Exception ex)
        {
            _vm.StatusMessage = "下载异常：" + ex.Message;
        }
        finally
        {
            _vm.IsDownloading = false;
        }
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return;

        var dialog = new Window
        {
            Title = title,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        var ok = new Button
        {
            Content = "确定",
            Width = 100,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Classes = { "accent" },
        };
        ok.Click += (_, _) => dialog.Close();
        stack.Children.Add(ok);
        dialog.Content = stack;
        await dialog.ShowDialog(owner);
    }

    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

}
