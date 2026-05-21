namespace SekaiToolsApp.Services;

/// <summary>
/// x264 编码参数构造器。原 <c>SekaiToolsGUI/Suppress/X264Params</c> 的 singleton
/// 改为普通对象，由 <see cref="Suppressor"/> 在每次启动时按 <see cref="SuppressorOptions"/>
/// 重新构造，方便并行任务和单元测试。
/// </summary>
public sealed class X264Params
{
    public int BFrames { get; init; } = 8;
    public int BAdapt { get; init; } = 2;
    public string Me { get; init; } = "umh";
    public int MeRange { get; init; } = 16;
    public int SubMe { get; init; } = 7;
    public int AqMode { get; init; } = 3;
    public int Ref { get; init; } = 4;
    public string PsyRd { get; init; } = "0.2:0.0";
    public string DeBlock { get; init; } = "1:2";
    public int KeyInt { get; init; } = 600;
    public int Crf { get; init; } = 21;

    public string GetX264Params()
    {
        return $"bframes={BFrames}:" +
               $"b-adapt={BAdapt}:" +
               $"me={Me}:" +
               $"merange={MeRange}:" +
               $"subme={SubMe}:" +
               $"aq-mode={AqMode}:" +
               $"ref={Ref}:" +
               $"psy-rd='{PsyRd}':" +
               $"deblock='{DeBlock}':" +
               $"keyint={KeyInt}:" +
               $"crf={Crf}";
    }

    public string GetSimpleX264Params()
    {
        return $"psy-rd='{PsyRd}':crf={Crf}";
    }
}
