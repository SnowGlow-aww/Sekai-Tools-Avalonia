using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using SekaiToolsCore;
using SekaiToolsPlatform.Services;

namespace SekaiToolsApp.Services;

/// <summary>
/// <see cref="IPlatformPreferences"/> 的桌面端实现：把 SekaiPlatform SDK 需要的几个键
/// (base url / access token) 持久化到独立的 JSON 文件。
///
/// 不复用 <see cref="SettingsService"/>：
/// - SettingsService 走的是 WPF 时代留下的 setting.json，那一份和 UI 主题等紧密耦合，
///   迁移期间还可能被旧 WPF 写回，不适合塞 access token；
/// - 平台凭证按 "platform-prefs.json" 单独存可以独立删除（登出、换服务器都更干净）。
/// </summary>
public sealed class JsonFilePlatformPreferences : IPlatformPreferences
{
    private static readonly Lazy<JsonFilePlatformPreferences> _instance = new(() => new());
    public static JsonFilePlatformPreferences Instance => _instance.Value;

    private readonly string _filePath;
    private readonly object _ioLock = new();
    private readonly ConcurrentDictionary<string, string> _cache;

    private JsonFilePlatformPreferences()
    {
        _filePath = Path.Combine(ResourceManager.DataBaseDir, "Data", "platform-prefs.json");
        _cache = new ConcurrentDictionary<string, string>(LoadFromDisk());
    }

    public string Get(string key, string defaultValue)
    {
        return _cache.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void Set(string key, string value)
    {
        _cache[key] = value;
        SaveToDisk();
    }

    public void Remove(string key)
    {
        if (_cache.TryRemove(key, out _))
        {
            SaveToDisk();
        }
    }

    private Dictionary<string, string> LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath)) return new Dictionary<string, string>();
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            // 文件损坏 / 不可读 → 当成空，下一次写入会覆盖。不抛异常以免阻塞启动。
            return new Dictionary<string, string>();
        }
    }

    private void SaveToDisk()
    {
        lock (_ioLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_cache,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // 写盘失败时静默：下一次操作会再次尝试，不会让 token 缓存丢。
            }
        }
    }
}
