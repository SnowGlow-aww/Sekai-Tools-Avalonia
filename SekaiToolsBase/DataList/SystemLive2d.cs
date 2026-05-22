namespace SekaiToolsBase.DataList;

public class SystemLive2d
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public string Unit { get; set; } = "";
    public string Serif { get; set; } = "";
    public string AssetbundleName { get; set; } = "";
    public string Voice { get; set; } = "";
    public string Motion { get; set; } = "";
    public string Expression { get; set; } = "";
    public long PublishedAt { get; set; }
    public long ClosedAt { get; set; }
    public int Weight { get; set; }
}
