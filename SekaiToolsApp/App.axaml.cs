using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SekaiToolsApp.Services;
using SekaiToolsApp.Views;

namespace SekaiToolsApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 全局兜底未捕获异常：迁移期排查很多 UI bug 都是“静默闪退”，把 stack 打出来。
        AppDomain.CurrentDomain.UnhandledException += static (_, e) =>
        {
            Console.Error.WriteLine($"[Unhandled] {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += static (_, e) =>
        {
            Console.Error.WriteLine($"[UnobservedTask] {e.Exception}");
            e.SetObserved();
        };

        // 应用持久化的主题选择（跟随系统 / 浅色 / 深色）。
        // 这一步必须在 MainWindow 创建前完成，避免主窗口先以默认主题闪一下再切换。
        SettingsService.Instance.ApplyCurrentTheme();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Closing += (_, _) => TranslateRecoveryService.Instance.Clear();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
