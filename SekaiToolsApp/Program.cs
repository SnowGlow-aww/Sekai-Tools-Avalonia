using Avalonia;
using Avalonia.ReactiveUI;
using System;

namespace SekaiToolsApp;

internal static class Program
{
    // Avalonia 启动入口；必须是 STA。
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
