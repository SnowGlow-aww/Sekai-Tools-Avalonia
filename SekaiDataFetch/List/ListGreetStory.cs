using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsBase.DataList;

namespace SekaiDataFetch.List;

public class ListGreetStory : BaseListStory
{
    public List<SystemLive2d> Data { get; private set; } = [];

    private ListGreetStory(Proxy? proxy = null)
    {
        SetProxy(proxy ?? Proxy.None);
        Load();
    }

    [CachePath("systemLive2ds")]
    private static string CachePathSystemLive2ds =>
        Path.Combine(DataBaseDir, "Data", "cache", "systemLive2ds.json");

    [SourcePath("systemLive2ds")]
    private static string SourceSystemLive2ds => Fetcher.SourceList.SystemLive2ds;

    public static ListGreetStory Instance { get; } = new();

    protected sealed override void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePathSystemLive2ds)!);
        if (!File.Exists(CachePathSystemLive2ds)) return;

        var content = File.ReadAllText(CachePathSystemLive2ds);
        try
        {
            var items = Utils.Deserialize<SystemLive2d[]>(content);
            if (items == null) throw new Exception("Json parse error");
            Data = items.ToList();
        }
        catch (Exception e)
        {
            Logger.Log(
                $"{GetType().Name} Failed to load data. Clearing cache. Error: {e.Message}",
                LogLevel.Error);
            ClearCache();
        }
    }
}
