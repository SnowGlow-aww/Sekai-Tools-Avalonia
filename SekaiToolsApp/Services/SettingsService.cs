using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using SekaiToolsCore;

namespace SekaiToolsApp.Services;

/// <summary>
/// 用户配置的运行时单例。负责加载/保存 JSON 以及向 Avalonia 应用应用主题。
/// 配置文件路径：~/SekaiTools/Data/setting.json。
/// </summary>
public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();

    private readonly object _ioLock = new();

    private SettingsService()
    {
        Current = LoadFromDisk();
    }

    /// <summary>当前生效配置。各 ViewModel 通过这个对象读写后调用 <see cref="Save"/>。</summary>
    public AppSettings Current { get; private set; }

    public string SettingsFilePath
        => Path.Combine(ResourceManager.DataBaseDir, "Data", "setting.json");

    /// <summary>
    /// 一次性迁移标记：存在即表示 <see cref="SettingsFilePath"/> 已被映射成 Avalonia 主题编码体系。
    /// 用独立 flag 文件而非 schema 字段，是为了在 WPF 仍可能写回 setting.json（迁移期）时
    /// 不会丢失迁移状态而触发反复错误转换。
    /// </summary>
    private string ThemeMigrationFlagPath
        => Path.Combine(ResourceManager.DataBaseDir, "Data", ".theme-migrated.flag");

    public void Save()
    {
        lock (_ioLock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
                var json = JsonSerializer.Serialize(Current, AppSettings.JsonOptions);
                File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 配置保存失败不应阻断 UI；后续 m1-logging 时挂上 ILogger。
                Console.Error.WriteLine($"[SettingsService] failed to save: {ex.Message}");
            }
        }
    }

    public void ResetToDefault()
    {
        Current = AppSettings.CreateDefault();
        EnsureDefaultPaths(Current);
        Save();
    }

    public void ApplyCurrentTheme()
    {
        ApplyTheme(Current.CurrentApplicationTheme);
    }

    public static void ApplyTheme(int themeIndex)
    {
        var app = Application.Current;
        if (app is null) return;

        // AppSettings 注释里的 0/1/2 编码：
        app.RequestedThemeVariant = themeIndex switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // 0 跟随系统，或越界回退到默认
        };
    }

    private AppSettings LoadFromDisk()
    {
        try
        {
            var migrationDone = File.Exists(ThemeMigrationFlagPath);

            if (!File.Exists(SettingsFilePath))
            {
                // 全新安装：直接默认值；建立 flag 防止之后 WPF 写入 setting.json 触发错误迁移。
                EnsureThemeMigrationFlag();
                var defaults = AppSettings.CreateDefault();
                EnsureDefaultPaths(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions)
                         ?? AppSettings.CreateDefault();
            EnsureDefaultPaths(loaded);

            var migrated = false;
            if (!migrationDone)
            {
                // WPF 编码：0=Light / 1=Dark / 2=HighContrast / 3=System
                // Avalonia 编码：0=System / 1=Light / 2=Dark
                loaded.CurrentApplicationTheme = MapWpfThemeToAvalonia(loaded.CurrentApplicationTheme);
                migrated = true;
            }

            // 越界（含 WPF 的 3=System）一律回到 0=系统，由用户在 Setting 页重新选择。
            if (loaded.CurrentApplicationTheme is < 0 or > 2)
                loaded.CurrentApplicationTheme = 0;

            if (migrated)
            {
                // 立即把映射后的 setting.json 写回，避免下次启动若 flag 丢失再次误迁移。
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
                    var migratedJson = JsonSerializer.Serialize(loaded, AppSettings.JsonOptions);
                    File.WriteAllText(SettingsFilePath, migratedJson, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[SettingsService] failed to persist migrated settings: {ex.Message}");
                }
                EnsureThemeMigrationFlag();
            }

            return loaded;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SettingsService] failed to load (using defaults): {ex.Message}");
            var defaults = AppSettings.CreateDefault();
            EnsureDefaultPaths(defaults);
            return defaults;
        }
    }

    private static int MapWpfThemeToAvalonia(int wpfValue) => wpfValue switch
    {
        0 => 1, // WPF Light → Avalonia Light
        1 => 2, // WPF Dark → Avalonia Dark
        2 => 0, // WPF HighContrast → Avalonia System（无对应，回退系统）
        3 => 0, // WPF System → Avalonia System
        _ => 0,
    };

    private void EnsureThemeMigrationFlag()
    {
        try
        {
            var dir = Path.GetDirectoryName(ThemeMigrationFlagPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(ThemeMigrationFlagPath))
                File.WriteAllText(ThemeMigrationFlagPath,
                    $"theme migrated at {DateTime.UtcNow:o}\n", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SettingsService] failed to write migration flag: {ex.Message}");
        }
    }

    private static void EnsureDefaultPaths(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DownloadDirectory))
        {
            settings.DownloadDirectory = Path.Combine(ResourceManager.DataBaseDir, "Scripts");
        }
    }
}
