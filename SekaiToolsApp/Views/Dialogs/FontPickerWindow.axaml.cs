using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SkiaSharp;

namespace SekaiToolsApp.Views.Dialogs;

/// <summary>
/// 跨平台字体选择器（独立 Window，比 ContentDialog 稳定）。
///
/// 核心要点：
/// - 字体枚举放在 <see cref="Task.Run"/> 里做，避免 1000+ 项在 UI 线程上同步构造 ListBoxItem；
/// - <c>ListBox</c> 显式启用 <c>VirtualizingStackPanel</c>，即便字体数极多也只渲染可见项；
/// - 通过 <see cref="Window.ShowDialog{TResult}"/> 回传字符串结果。
/// </summary>
public partial class FontPickerWindow : Window
{
    private List<string> _allFonts = new();
    private string? _initialSelectedFont;

    public FontPickerWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    /// <summary>调用方在构造后设置，<see cref="OnOpened"/> 触发时应用到 ListBox。</summary>
    public string? InitialSelectedFont
    {
        get => _initialSelectedFont;
        set => _initialSelectedFont = value;
    }

    /// <summary>当前选中字体（确认按钮可用的前提）。</summary>
    public string? SelectedFont => FontList.SelectedItem as string;

    private async void OnOpened(object? sender, EventArgs e)
    {
        StatusText.Text = "加载中...";
        try
        {
            var fonts = await Task.Run(LoadFontsBlocking);
            _allFonts = fonts;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FontPickerWindow] font enumeration failed: {ex}");
            StatusText.Text = "加载字体失败";
            LoadingOverlay.IsVisible = false;
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            FontList.ItemsSource = _allFonts;
            LoadingOverlay.IsVisible = false;
            StatusText.Text = $"{_allFonts.Count} 个字体";

            ApplyInitialSelection();
        });
    }

    private static List<string> LoadFontsBlocking()
    {
        return SKFontManager.Default.FontFamilies
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyInitialSelection()
    {
        if (string.IsNullOrEmpty(_initialSelectedFont)) return;
        var match = _allFonts.FirstOrDefault(
            f => string.Equals(f, _initialSelectedFont, StringComparison.OrdinalIgnoreCase));
        if (match is null) return;
        FontList.SelectedItem = match;
        FontList.ScrollIntoView(match);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var keyword = (SearchBox.Text ?? string.Empty).Trim();
        FontList.ItemsSource = string.IsNullOrEmpty(keyword)
            ? _allFonts
            : _allFonts.Where(f => f.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = FontList.SelectedItem is string;
    }

    private void OnItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (FontList.SelectedItem is string font)
        {
            Close(font);
        }
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (FontList.SelectedItem is string font)
        {
            Close(font);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
