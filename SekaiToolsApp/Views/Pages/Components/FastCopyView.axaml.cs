using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using SekaiToolsApp.Services;
using SekaiToolsPlatform.Services;

namespace SekaiToolsApp.Views.Pages.Components;

/// <summary>
/// 翻译页左侧的"特殊字符"侧栏。
/// </summary>
public partial class FastCopyView : UserControl
{
    private const string CustomCharsKey = "FastCopy_CustomChars";

    private readonly IPlatformPreferences _preferences = JsonFilePlatformPreferences.Instance;
    private CancellationTokenSource? _copyFeedbackCts;

    public FastCopyView()
    {
        InitializeComponent();
        LoadCustomButtons();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private List<string> LoadCustomChars()
    {
        var json = _preferences.Get(CustomCharsKey, "[]");
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            // 数据损坏时退回空列表，下次写入会自动覆盖。
            return new List<string>();
        }
    }

    private void SaveCustomChars(List<string> chars)
    {
        _preferences.Set(CustomCharsKey, JsonSerializer.Serialize(chars));
    }

    private void LoadCustomButtons()
    {
        var panel = this.FindControl<StackPanel>("CustomCharsPanel");
        if (panel is null) return;
        var chars = LoadCustomChars();
        panel.Children.Clear();
        foreach (var ch in chars)
            panel.Children.Add(BuildCustomButton(ch));
    }

    private Control BuildCustomButton(string content)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };

        var btn = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 2, 0),
        };
        btn.Click += OnSpecialCharClicked;
        Grid.SetColumn(btn, 0);

        var del = new Button
        {
            Content = "✕",
            Width = 36,
            FontSize = 11,
            Background = Avalonia.Media.Brushes.Transparent,
        };
        del.Click += (_, _) =>
        {
            var chars = LoadCustomChars();
            chars.Remove(content);
            SaveCustomChars(chars);
            LoadCustomButtons();
        };
        Grid.SetColumn(del, 1);

        grid.Children.Add(btn);
        grid.Children.Add(del);
        return grid;
    }

    private async void OnSpecialCharClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var text = btn.Content?.ToString();
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                await ShowCopyStatusAsync("复制失败");
                return;
            }
            await clipboard.SetTextAsync(text);
            await ShowCopyStatusAsync($"已复制：{text}");
        }
        catch
        {
            // 剪贴板在某些环境下可能无效（headless 调试），吞。
            await ShowCopyStatusAsync("复制失败");
        }
    }

    private async void OnAddCustomClicked(object? sender, RoutedEventArgs e)
    {
        var input = await ShowPromptAsync("添加自定义字符", "请输入要快速复制的字符：");
        if (string.IsNullOrEmpty(input)) return;

        var chars = LoadCustomChars();
        if (chars.Contains(input)) return;
        chars.Add(input);
        SaveCustomChars(chars);
        LoadCustomButtons();
    }

    private async System.Threading.Tasks.Task<string?> ShowPromptAsync(string title, string message)
    {
        var owner = GetOwnerWindow();
        if (owner is null) return null;

        var dialog = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var textBox = new TextBox { Watermark = message };
        var ok = new Button { Content = "添加", Classes = { "accent" }, IsDefault = true };
        var cancel = new Button { Content = "取消", IsCancel = true };

        string? result = null;
        ok.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                result = textBox.Text;
                dialog.Close();
            }
        };

        var layout = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        layout.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        layout.Children.Add(textBox);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        layout.Children.Add(buttons);
        dialog.Content = layout;
        await dialog.ShowDialog(owner);
        return string.IsNullOrEmpty(result) ? null : result;
    }

    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private async Task ShowCopyStatusAsync(string message)
    {
        var host = this.FindControl<Border>("CopyStatusHost");
        var textBlock = this.FindControl<TextBlock>("CopyStatusText");
        if (host is null || textBlock is null) return;

        _copyFeedbackCts?.Cancel();
        var cts = _copyFeedbackCts = new CancellationTokenSource();
        textBlock.Text = message;
        host.IsVisible = true;

        try
        {
            await Task.Delay(1200, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cts.IsCancellationRequested)
        {
            host.IsVisible = false;
            textBlock.Text = string.Empty;
        }
    }
}
