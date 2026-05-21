using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsApp.Services;

namespace SekaiToolsApp.ViewModels;

/// <summary>
/// 视频压制页 ViewModel。
///
/// 对应原 <c>SekaiToolsGUI/ViewModel/Suppress/SuppressPageModel</c>，但去掉了
/// singleton；该 VM 由 <see cref="Views.Pages.SuppressPageView"/> 持有，
/// <see cref="Suppressor"/> 也由 View 直接构造，回调里更新本 VM。
///
/// 状态机和 <see cref="SubtitlePageViewModel"/> 一致：
/// <see cref="SuppressRunState.NotStarted"/> → <see cref="SuppressRunState.Running"/>
/// → <see cref="SuppressRunState.Finished"/> / <see cref="SuppressRunState.Stopped"/>
/// / <see cref="SuppressRunState.Failed"/>。
/// </summary>
public partial class SuppressPageViewModel : ViewModelBase
{
    #region 文件路径

    [ObservableProperty] private string _sourceVideo = string.Empty;
    [ObservableProperty] private string _sourceSubtitle = string.Empty;
    [ObservableProperty] private string _outputPath = string.Empty;

    /// <summary>视频总帧数。在 <see cref="SourceVideo"/> 改变时探测一次，避免运行时再读 native。</summary>
    [ObservableProperty] private int _sourceFrameCount;

    public bool HasSourceVideo => !string.IsNullOrWhiteSpace(SourceVideo);
    public bool HasSourceSubtitle => !string.IsNullOrWhiteSpace(SourceSubtitle);
    public bool HasOutputPath => !string.IsNullOrWhiteSpace(OutputPath);

    /// <summary>
    /// 源视频改变时联动：探测帧数 / 自动猜同名 .ass / 自动生成默认输出路径。
    /// 字幕缺失允许（VSPipe 脚本会无字幕直出），所以并非启动门槛。
    /// </summary>
    partial void OnSourceVideoChanged(string value)
    {
        OnPropertyChanged(nameof(HasSourceVideo));
        OnPropertyChanged(nameof(CanStart));

        SourceFrameCount = 0;
        if (File.Exists(value))
        {
            try
            {
                using var capture = new VideoCapture(value);
                SourceFrameCount = (int)capture.Get(CapProp.FrameCount);
            }
            catch
            {
                // 帧数 probe 失败不阻塞流程，运行时 ffmpeg 进度行会持续推帧号但百分比可能停 0。
                SourceFrameCount = 0;
            }

            var guess = Path.ChangeExtension(value, ".ass");
            if (File.Exists(guess)) SourceSubtitle = guess;

            var dir = Path.GetDirectoryName(value);
            if (!string.IsNullOrEmpty(dir))
            {
                OutputPath = Path.Combine(dir,
                    "[STVS]" + Path.GetFileNameWithoutExtension(value) + ".mp4");
            }
        }
    }

    partial void OnSourceSubtitleChanged(string value)
    {
        OnPropertyChanged(nameof(HasSourceSubtitle));
    }

    partial void OnOutputPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutputPath));
        OnPropertyChanged(nameof(CanStart));
    }

    #endregion

    #region 编码参数

    [ObservableProperty] private int _suppressCrf = 21;
    [ObservableProperty] private bool _useComplexConfig = true;
    [ObservableProperty] private VideoEncoder _selectedEncoder = VideoEncoder.Libx264;
    [ObservableProperty] private List<VideoEncoder> _availableEncoders = [VideoEncoder.Libx264];
    [ObservableProperty] private bool _useHwAccelDecode = true;

    public List<string> AvailableEncoderNames => AvailableEncoders.Select(e => e.DisplayName()).ToList();

    public int SelectedEncoderIndex
    {
        get => AvailableEncoders.IndexOf(SelectedEncoder);
        set
        {
            if (value >= 0 && value < AvailableEncoders.Count)
                SelectedEncoder = AvailableEncoders[value];
        }
    }

    partial void OnAvailableEncodersChanged(List<VideoEncoder> value)
    {
        OnPropertyChanged(nameof(AvailableEncoderNames));
        OnPropertyChanged(nameof(SelectedEncoderIndex));
    }

    partial void OnSelectedEncoderChanged(VideoEncoder value)
    {
        OnPropertyChanged(nameof(SelectedEncoderIndex));
    }

    #endregion

    #region 运行状态

    [ObservableProperty] private SuppressRunState _runState = SuppressRunState.NotStarted;

    public bool HasNotStarted => RunState == SuppressRunState.NotStarted;
    public bool IsRunning => RunState == SuppressRunState.Running;
    public bool IsFinished => RunState == SuppressRunState.Finished;
    public bool IsStopped => RunState == SuppressRunState.Stopped;
    public bool IsFailed => RunState == SuppressRunState.Failed;

    /// <summary>
    /// 启动按钮可用条件：未运行 + 视频/输出齐全 + 资源已就绪。字幕可空。
    /// </summary>
    public bool CanStart => HasNotStarted && HasSourceVideo && HasOutputPath && IsResourceReady;
    public bool CanStop => IsRunning;
    public bool CanReset => !IsRunning && (HasSourceVideo || HasSourceSubtitle || HasOutputPath ||
                                            RunState != SuppressRunState.NotStarted);

    public bool CanShowOutput => IsFinished && File.Exists(OutputPath);

    public string RunStateText => RunState switch
    {
        SuppressRunState.NotStarted => "未开始",
        SuppressRunState.Running => "压制中",
        SuppressRunState.Finished => "已完成",
        SuppressRunState.Stopped => "已停止",
        SuppressRunState.Failed => "已失败",
        _ => string.Empty,
    };

    partial void OnRunStateChanged(SuppressRunState value)
    {
        OnPropertyChanged(nameof(HasNotStarted));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(CanShowOutput));
        OnPropertyChanged(nameof(RunStateText));
    }

    #endregion

    #region 进度 / 日志

    [ObservableProperty] private double _progress;
    [ObservableProperty] private double _fps;
    [ObservableProperty] private string _fpsText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _stopReasonText = string.Empty;

    public bool HasFpsText => !string.IsNullOrEmpty(FpsText);
    public bool HasStopReasonText => !string.IsNullOrEmpty(StopReasonText);

    partial void OnFpsTextChanged(string value) => OnPropertyChanged(nameof(HasFpsText));
    partial void OnStopReasonTextChanged(string value) => OnPropertyChanged(nameof(HasStopReasonText));

    /// <summary>
    /// 追加一行日志。Suppressor 在主线程外调用，UI 转发前需切回主线程。
    /// </summary>
    public void AppendLog(string line)
    {
        Status = string.IsNullOrEmpty(Status) ? line : Status + "\n" + line;
    }

    /// <summary>
    /// 替换最后一行日志（用于 ffmpeg 进度行连续刷新）。如果没有上一行则当作 Append。
    /// </summary>
    public void ReplaceLastLog(string line)
    {
        if (string.IsNullOrEmpty(Status))
        {
            Status = line;
            return;
        }

        var idx = Status.LastIndexOf('\n');
        Status = idx < 0 ? line : Status[..idx] + "\n" + line;
    }

    #endregion

    #region 资源引导

    [ObservableProperty] private ResourceReadyState _resourceState = ResourceReadyState.NotChecked;
    [ObservableProperty] private string _resourceStatusText = string.Empty;
    [ObservableProperty] private bool _resourceOverlayDismissed;
    [ObservableProperty] private bool _isPlatformSupported =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    public bool IsResourceReady => ResourceState == ResourceReadyState.Ready;
    public bool IsResourceChecking => ResourceState is ResourceReadyState.Checking or ResourceReadyState.NotChecked;
    public bool IsResourceFailed => ResourceState == ResourceReadyState.Failed;

    /// <summary>
    /// 覆盖层显示条件：平台不支持 / 资源未就绪 / 就绪但用户未点过"开始使用"。
    /// 与 <see cref="SubtitlePageViewModel.IsResourceNotReady"/> 一致的 UX。
    /// </summary>
    public bool IsResourceNotReady =>
        !IsPlatformSupported ||
        ResourceState != ResourceReadyState.Ready ||
        (ResourceState == ResourceReadyState.Ready && !ResourceOverlayDismissed);

    partial void OnResourceStateChanged(ResourceReadyState value)
    {
        OnPropertyChanged(nameof(IsResourceReady));
        OnPropertyChanged(nameof(IsResourceNotReady));
        OnPropertyChanged(nameof(IsResourceChecking));
        OnPropertyChanged(nameof(IsResourceFailed));
        OnPropertyChanged(nameof(CanStart));
    }

    partial void OnResourceOverlayDismissedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsResourceNotReady));
    }

    partial void OnIsPlatformSupportedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsResourceNotReady));
        OnPropertyChanged(nameof(CanStart));
    }

    #endregion

    public SuppressorOptions BuildOptions()
    {
        return new SuppressorOptions
        {
            SourceVideo = SourceVideo,
            SourceSubtitle = SourceSubtitle,
            OutputPath = OutputPath,
            UseComplexConfig = UseComplexConfig,
            Crf = SuppressCrf,
            FfmpegPath = SettingsService.Instance.Current.FfmpegPath,
            SourceFrameCount = SourceFrameCount,
            PreferredEncoder = SelectedEncoder,
            UseHwAccelDecode = UseHwAccelDecode,
        };
    }

    public void ResetRunData()
    {
        RunState = SuppressRunState.NotStarted;
        Progress = 0;
        Fps = 0;
        FpsText = string.Empty;
        Status = string.Empty;
        StopReasonText = string.Empty;
    }

    public void ResetAll()
    {
        ResetRunData();
        SourceVideo = string.Empty;
        SourceSubtitle = string.Empty;
        OutputPath = string.Empty;
        SuppressCrf = 21;
        UseComplexConfig = true;
        SourceFrameCount = 0;
        SelectedEncoder = VideoEncoder.Libx264;
        UseHwAccelDecode = true;
    }
}

public enum SuppressRunState
{
    NotStarted,
    Running,
    Finished,
    Stopped,
    Failed,
}
