namespace SekaiToolsCore.Process.Config;

public struct MatchingThreshold()
{
    public double DialogNametagNormal { get; init; } = 0.70;
    public double DialogNametagSpecial { get; init; } = 0.70;
    public double DialogContentNormal { get; init; } = 0.70;
    public double DialogContentSpecial { get; init; } = 0.70;
    public double BannerNormal { get; init; } = 0.50;
    public double MarkerNormal { get; init; } = 0.50;
}