namespace SekaiToolsApp.Services;

public enum VideoEncoder
{
    Libx264,
    H264VideoToolbox,
    H264Nvenc,
    H264Qsv,
}

public static class VideoEncoderExtensions
{
    public static string DisplayName(this VideoEncoder encoder) => encoder switch
    {
        VideoEncoder.Libx264 => "Libx264（不推荐）",
        VideoEncoder.H264VideoToolbox => "H264 VideoToolbox",
        VideoEncoder.H264Nvenc => "H264 NVENC",
        VideoEncoder.H264Qsv => "H264 QSV",
        _ => encoder.ToString(),
    };
}
