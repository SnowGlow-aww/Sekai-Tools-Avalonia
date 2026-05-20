using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SekaiToolsApp.ViewModels;
using SekaiToolsApp.Views.Dialogs;

namespace SekaiToolsApp.Views.Pages;

public partial class SettingPageView : UserControl
{
    private readonly SettingPageViewModel _vm;

    public SettingPageView()
    {
        InitializeComponent();
        _vm = new SettingPageViewModel();
        DataContext = _vm;
    }

    private void OnPickDialogFont(object? sender, RoutedEventArgs e)
        => _ = PickFontAsync(s => _vm.DialogFontFamily = s, _vm.DialogFontFamily);

    private void OnPickBannerFont(object? sender, RoutedEventArgs e)
        => _ = PickFontAsync(s => _vm.BannerFontFamily = s, _vm.BannerFontFamily);

    private void OnPickMarkerFont(object? sender, RoutedEventArgs e)
        => _ = PickFontAsync(s => _vm.MarkerFontFamily = s, _vm.MarkerFontFamily);

    private void OnBrowseDownloadDirectory(object? sender, RoutedEventArgs e)
        => _ = PickDownloadDirectoryAsync();

    private void OnBrowseFfmpegPath(object? sender, RoutedEventArgs e)
        => _ = PickFfmpegPathAsync();

    private async Task PickFontAsync(Action<string> onConfirm, string current)
    {
        try
        {
            var owner = GetOwnerWindow();
            if (owner is null)
            {
                Console.Error.WriteLine("[SettingPageView] cannot locate owner window for FontPickerWindow");
                return;
            }

            var window = new FontPickerWindow { InitialSelectedFont = current };
            var result = await window.ShowDialog<string?>(owner);
            if (!string.IsNullOrWhiteSpace(result))
            {
                onConfirm(result!);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SettingPageView] PickFontAsync failed: {ex}");
        }
    }

    private async Task PickDownloadDirectoryAsync()
    {
        try
        {
            var owner = GetOwnerWindow();
            if (owner is null)
            {
                Console.Error.WriteLine("[SettingPageView] cannot locate owner window for folder picker");
                return;
            }

            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择下载目录",
                AllowMultiple = false,
            });

            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _vm.DownloadDirectory = path;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SettingPageView] PickDownloadDirectoryAsync failed: {ex}");
        }
    }

    private async Task PickFfmpegPathAsync()
    {
        try
        {
            var owner = GetOwnerWindow();
            if (owner is null)
            {
                Console.Error.WriteLine("[SettingPageView] cannot locate owner window for ffmpeg picker");
                return;
            }

            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 ffmpeg 可执行文件",
                AllowMultiple = false,
            });

            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _vm.FfmpegPath = path;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SettingPageView] PickFfmpegPathAsync failed: {ex}");
        }
    }

    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
