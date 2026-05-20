using System.Text.Json;
using SekaiToolsApp.ViewModels;
using SekaiToolsCore;

namespace SekaiToolsApp.Services;

/// <summary>
/// 下载列表本地历史。
///
/// 记录 Download 页右侧列表当前状态，重启后可恢复查看下载项与保存路径。
/// </summary>
public sealed class DownloadHistoryService
{
    public static DownloadHistoryService Instance { get; } = new();

    private readonly string _filePath = Path.Combine(ResourceManager.DataBaseDir, "Data", "download-history.json");
    private readonly object _ioLock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private DownloadHistoryService()
    {
    }

    public IReadOnlyList<DownloadTaskSnapshot> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<DownloadTaskSnapshot>();
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<DownloadTaskSnapshot>();
            return JsonSerializer.Deserialize<DownloadTaskSnapshot[]>(json, _jsonOptions)
                   ?? Array.Empty<DownloadTaskSnapshot>();
        }
        catch
        {
            return Array.Empty<DownloadTaskSnapshot>();
        }
    }

    public void Save(IEnumerable<DownloadTaskSnapshot> snapshots)
    {
        lock (_ioLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(snapshots.ToArray(), _jsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // 下载历史只是 UX 数据，写失败不阻断主流程。
            }
        }
    }
}
