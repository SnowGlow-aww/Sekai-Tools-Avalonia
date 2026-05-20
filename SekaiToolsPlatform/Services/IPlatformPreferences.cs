namespace SekaiToolsPlatform.Services;

/// <summary>
/// 一组键值偏好的最小抽象。
/// MAUI 时代由 <c>Microsoft.Maui.Storage.Preferences</c> 提供；
/// Avalonia 桌面端由宿主负责实现（默认走 SekaiToolsApp 的 SettingsService JSON 文件）。
///
/// SDK 层只需要 base url / access token 两个键，因此接口保持极小。
/// </summary>
public interface IPlatformPreferences
{
    string Get(string key, string defaultValue);
    void Set(string key, string value);
    void Remove(string key);
}

/// <summary>
/// 进程内默认实现，仅用于单元测试 / CLI 等无持久化需求的场景。
/// 桌面端宿主务必注入自己的实现，否则重启后 token 会丢。
/// </summary>
public sealed class InMemoryPlatformPreferences : IPlatformPreferences
{
    private readonly Dictionary<string, string> _data = new();
    private readonly object _gate = new();

    public string Get(string key, string defaultValue)
    {
        lock (_gate) return _data.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void Set(string key, string value)
    {
        lock (_gate) _data[key] = value;
    }

    public void Remove(string key)
    {
        lock (_gate) _data.Remove(key);
    }
}
