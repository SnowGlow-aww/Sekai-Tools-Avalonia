using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsApp.Services;

/// <summary>
/// 启动 Suppressor 所需的输入。原 <c>Suppressor</c> 与 <c>SuppressPageModel</c>
/// 通过 singleton 互相耦合，新版改为参数注入：UI 收集完字段后构造一次此对象交给后台。
/// </summary>
public sealed class SuppressorOptions
{
    public required string SourceVideo { get; init; }
    public string SourceSubtitle { get; init; } = string.Empty;
    public required string OutputPath { get; init; }
    public bool UseComplexConfig { get; init; } = true;
    public int Crf { get; init; } = 21;
    public string FfmpegPath { get; init; } = string.Empty;
    public VideoEncoder PreferredEncoder { get; init; } = VideoEncoder.Libx264;
    public bool UseHwAccelDecode { get; init; } = true;

    /// <summary>
    /// 视频总帧数，用于进度百分比计算。零值时由 <see cref="Suppressor"/> 自行 probe，
    /// 再缓存到该字段（仅本次启动有效）。
    /// </summary>
    public int SourceFrameCount { get; set; }
}

/// <summary>
/// Suppressor 后台流水线状态回调。线程不固定（来自 Process IO 线程），
/// UI 端需要自行把更新分发回主线程（参考 SubtitlePageView 的 Dispatcher.Post 模式）。
/// </summary>
public sealed class SuppressorCallbacks
{
    /// <summary>从启动成功后回调一次，可用作 UI "已开始" 信号。</summary>
    public Action? OnStarted { get; init; }

    /// <summary>追加一行日志（已经按行切分，不含末尾换行）。</summary>
    public Action<string>? OnLogLine { get; init; }

    /// <summary>替换最后一行日志。Suppressor 在解析到 ffmpeg "frame=… fps=…"
    /// 进度行时使用：连续的进度行只占一行，避免文本框无限增长。</summary>
    public Action<string>? OnProgressLogLine { get; init; }

    /// <summary>当前帧 / 总帧数 / FPS。Suppressor 在解析进度行时回调。</summary>
    public Action<int, int, double>? OnProgress { get; init; }

    /// <summary>整条流水线结束（正常退出 / 失败 / 取消）后回调一次。</summary>
    public Action<SuppressorStopReason, Exception?>? OnFinished { get; init; }
}

public enum SuppressorStopReason
{
    Completed,
    Canceled,
    Failed,
}

/// <summary>
/// 视频压制流水线。
///
/// 优先使用本地 VapourSynth 资源（如果存在），否则自动回退到跨平台的 ffmpeg
/// burn-in 流程。这样 macOS / Linux 不再被 Windows-only 资源锁死。
/// </summary>
public sealed partial class Suppressor : IDisposable
{
    private readonly SuppressorOptions _options;
    private readonly SuppressorCallbacks _callbacks;
    private readonly X264Params _x264Params;

    private SuppressRuntimeDescriptor? _runtime;
    private Process? _vProcess;
    private Process? _fProcess;
    private Task? _pipeTask;
    private Task? _logTask;
    private CancellationTokenSource? _cts;
    private int _frameCount;
    private double _fps;
    private bool _lastLogLineWasProgress;
    private int _disposed;

    public Suppressor(SuppressorOptions options, SuppressorCallbacks callbacks)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        _x264Params = new X264Params { Crf = options.Crf };
    }

    public static SuppressRuntimeProbe ProbeRuntime(string? ffmpegPathHint = null)
        => SuppressRuntimeService.Probe(ffmpegPathHint);

    public bool IsRunning => _vProcess is { HasExited: false } || _fProcess is { HasExited: false };

    /// <summary>
    /// 启动一个可用的压制后端并开始跑流水线。
    /// 同步返回，IO 在内部线程跑；调用方拿 <see cref="SuppressorCallbacks.OnFinished"/> 等结束。
    /// </summary>
    public void Start()
    {
        if (IsRunning)
            throw new InvalidOperationException("Suppressor 已经在运行。");

        EnsureSourceExists();

        _runtime = SuppressRuntimeService.Resolve(_options.FfmpegPath);

        _frameCount = 0;
        _fps = 0;
        _lastLogLineWasProgress = false;
        _cts = new CancellationTokenSource();

        switch (_runtime.Backend)
        {
            case SuppressBackend.VapourSynth:
                StartLegacyPipeline();
                break;
            case SuppressBackend.Ffmpeg:
                StartFfmpegPipeline();
                break;
            default:
                throw new InvalidOperationException($"未知压制后端：{_runtime.Backend}");
        }

        _callbacks.OnStarted?.Invoke();
        _ = Task.Run(WaitForExitAsync);
    }

    /// <summary>
    /// 主动停止：终止子进程，等待 IO 任务退出。可重入。
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // 已 Dispose 等情况吞掉。
        }

        TryKill(_vProcess);
        TryKill(_fProcess);

        try
        {
            if (_pipeTask != null) await _pipeTask.ConfigureAwait(false);
        }
        catch
        {
            // pipe 任务在被取消/管道关闭时会抛，统一吞。
        }

        try
        {
            if (_logTask != null) await _logTask.ConfigureAwait(false);
        }
        catch
        {
            // 同上。
        }
    }

    private void StartLegacyPipeline()
    {
        if (_runtime is null)
            throw new InvalidOperationException("压制运行环境尚未解析。");

        _vProcess = CreateVapourProcess(_runtime.VapourSynthPath!, _runtime.VapourScriptPath!);
        _fProcess = CreateLegacyFfmpegProcess(_runtime.FfmpegPath);

        _vProcess.Start();
        _fProcess.Start();

        _pipeTask = Task.Run(() => RunPipe(_cts!.Token));
        _logTask = Task.Run(() => RunLogReader(_cts!.Token));
    }

    private void StartFfmpegPipeline()
    {
        if (_runtime is null)
            throw new InvalidOperationException("压制运行环境尚未解析。");

        _fProcess = CreateFfmpegOnlyProcess(_runtime.FfmpegPath);
        _fProcess.Start();

        _logTask = Task.Run(() => RunLogReader(_cts!.Token));
    }

    private async Task WaitForExitAsync()
    {
        Exception? failure = null;
        var canceled = false;
        try
        {
            if (_vProcess != null) await _vProcess.WaitForExitAsync().ConfigureAwait(false);
            if (_fProcess != null) await _fProcess.WaitForExitAsync().ConfigureAwait(false);

            if (_logTask != null)
            {
                try { await _logTask.ConfigureAwait(false); }
                catch { /* 已经在 RunLogReader 内吞，外层留底。 */ }
            }

            if (_pipeTask != null)
            {
                try { await _pipeTask.ConfigureAwait(false); }
                catch { /* 同上。 */ }
            }

            canceled = _cts?.IsCancellationRequested ?? false;
            if (!canceled)
            {
                failure = BuildExitFailure();
                if (failure != null)
                {
                    _callbacks.OnLogLine?.Invoke($"[Sekai] 压制后端异常退出：{failure.Message}");
                }
                else
                {
                    var total = GetFrameCount();
                    if (total > 0)
                    {
                        _frameCount = total;
                        _callbacks.OnProgress?.Invoke(_frameCount, total, _fps);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        var reason = failure != null ? SuppressorStopReason.Failed
            : canceled ? SuppressorStopReason.Canceled
            : SuppressorStopReason.Completed;

        _callbacks.OnFinished?.Invoke(reason, failure);
    }

    private Exception? BuildExitFailure()
    {
        var failures = new List<string>();

        AppendExitFailure(_vProcess, "VSPipe", failures);
        AppendExitFailure(_fProcess, "ffmpeg", failures);

        if (failures.Count == 0)
            return null;

        return new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }

    private static void AppendExitFailure(Process? process, string name, ICollection<string> failures)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                return;

            if (process.ExitCode != 0)
                failures.Add($"{name} 退出码 {process.ExitCode}。");
        }
        catch (InvalidOperationException)
        {
            // 进程已释放时读取 ExitCode 可能失败，忽略。
        }
    }

    private void RunPipe(CancellationToken token)
    {
        if (_vProcess == null || _fProcess == null) return;
        var src = _vProcess.StandardOutput.BaseStream;
        var dst = _fProcess.StandardInput.BaseStream;

        var buffer = new byte[1 << 16];
        try
        {
            int read;
            while (!token.IsCancellationRequested &&
                   (read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                dst.Write(buffer, 0, read);
            }
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // 取消 / 进程被 Kill 关闭管道 → 正常路径，吞。
        }
        finally
        {
            try { dst.Close(); }
            catch { /* 已关闭。 */ }
        }
    }

    private void RunLogReader(CancellationToken token)
    {
        if (_fProcess == null) return;
        var stderr = _fProcess.StandardError;

        try
        {
            string? line;
            while (!token.IsCancellationRequested && (line = stderr.ReadLine()) != null)
            {
                AnalysisLog(line);
            }
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // 进程被关闭时 ReadLine 会抛，吞。
        }
    }

    private void AnalysisLog(string log)
    {
        if (FfmpegProgressPattern().IsMatch(log))
        {
            var match = FfmpegProgressPattern().Match(log);
            _frameCount = int.Parse(match.Groups["FrameNumber"].Value);
            _fps = double.Parse(match.Groups["FramesPerSecond"].Value);

            // 进度行用 OnProgressLogLine 替换上一行，避免日志窗口被进度刷屏。
            if (_lastLogLineWasProgress)
                _callbacks.OnProgressLogLine?.Invoke(log);
            else
                _callbacks.OnLogLine?.Invoke(log);

            _lastLogLineWasProgress = true;

            var total = GetFrameCount();
            if (total > 0)
                _callbacks.OnProgress?.Invoke(_frameCount, total, _fps);
        }
        else
        {
            _callbacks.OnLogLine?.Invoke(log);
            _lastLogLineWasProgress = false;
        }
    }

    private int GetFrameCount()
    {
        if (_options.SourceFrameCount > 0) return _options.SourceFrameCount;
        if (string.IsNullOrEmpty(_options.SourceVideo) || !File.Exists(_options.SourceVideo))
            return 0;

        using var capture = new VideoCapture(_options.SourceVideo);
        var probed = (int)capture.Get(CapProp.FrameCount);
        _options.SourceFrameCount = probed;
        return probed;
    }

    private Process CreateVapourProcess(string vapourExecutable, string vapourScript)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = vapourExecutable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
            },
        };

        process.StartInfo.ArgumentList.Add(vapourScript);
        process.StartInfo.ArgumentList.Add("-");
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("y4m");
        process.StartInfo.ArgumentList.Add("-a");
        process.StartInfo.ArgumentList.Add($"source={_options.SourceVideo}");
        process.StartInfo.ArgumentList.Add("-a");
        process.StartInfo.ArgumentList.Add($"subtitle={_options.SourceSubtitle}");

        return process;
    }

    private Process CreateLegacyFfmpegProcess(string ffmpegPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
            },
        };

        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("yuv4mpegpipe");
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add("-");
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(_options.SourceVideo);
        process.StartInfo.ArgumentList.Add("-map");
        process.StartInfo.ArgumentList.Add("0:v:0");
        process.StartInfo.ArgumentList.Add("-map");
        process.StartInfo.ArgumentList.Add("1:a?");

        AddEncoderArgs(process.StartInfo.ArgumentList);

        process.StartInfo.ArgumentList.Add("-c:a");
        process.StartInfo.ArgumentList.Add("copy");
        process.StartInfo.ArgumentList.Add("-y");
        process.StartInfo.ArgumentList.Add(_options.OutputPath);

        return process;
    }

    private Process CreateFfmpegOnlyProcess(string ffmpegPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
            },
        };

        process.StartInfo.ArgumentList.Add("-hide_banner");
        process.StartInfo.ArgumentList.Add("-y");

        AddHwAccelDecodeArgs(process.StartInfo.ArgumentList);

        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(_options.SourceVideo);

        var subtitleFilter = BuildSubtitleFilter();
        if (subtitleFilter is not null)
        {
            process.StartInfo.ArgumentList.Add("-vf");
            process.StartInfo.ArgumentList.Add(subtitleFilter);
        }

        process.StartInfo.ArgumentList.Add("-map");
        process.StartInfo.ArgumentList.Add("0:v:0");
        process.StartInfo.ArgumentList.Add("-map");
        process.StartInfo.ArgumentList.Add("0:a?");

        AddEncoderArgs(process.StartInfo.ArgumentList);

        process.StartInfo.ArgumentList.Add("-c:a");
        process.StartInfo.ArgumentList.Add("copy");
        process.StartInfo.ArgumentList.Add(_options.OutputPath);

        return process;
    }

    private string? BuildSubtitleFilter()
    {
        if (string.IsNullOrWhiteSpace(_options.SourceSubtitle))
            return null;

        if (!File.Exists(_options.SourceSubtitle))
            return null;

        var escaped = EscapeFfmpegFilterValue(Path.GetFullPath(_options.SourceSubtitle));
        return $"subtitles=filename='{escaped}'";
    }

    private static string EscapeFfmpegFilterValue(string value)
    {
        var normalized = value.Replace('\\', '/');
        var builder = new StringBuilder(normalized.Length * 2);
        foreach (var ch in normalized)
        {
            switch (ch)
            {
                case '\\':
                case '\'':
                case ':':
                case ',':
                case '[':
                case ']':
                    builder.Append('\\');
                    builder.Append(ch);
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }

    private void AddHwAccelDecodeArgs(IList<string> args)
    {
        if (!_options.UseHwAccelDecode) return;

        if (OperatingSystem.IsMacOS())
        {
            args.Add("-hwaccel");
            args.Add("videotoolbox");
        }
        else
        {
            args.Add("-hwaccel");
            args.Add("auto");
        }
    }

    private void AddEncoderArgs(IList<string> args)
    {
        switch (_options.PreferredEncoder)
        {
            case VideoEncoder.H264VideoToolbox:
                args.Add("-c:v");
                args.Add("h264_videotoolbox");
                args.Add("-q:v");
                args.Add("65");
                args.Add("-profile:v");
                args.Add("high");
                break;
            case VideoEncoder.H264Nvenc:
                args.Add("-c:v");
                args.Add("h264_nvenc");
                args.Add("-preset");
                args.Add("p4");
                args.Add("-cq");
                args.Add(_options.Crf.ToString());
                args.Add("-profile:v");
                args.Add("high");
                break;
            case VideoEncoder.H264Qsv:
                args.Add("-c:v");
                args.Add("h264_qsv");
                args.Add("-global_quality");
                args.Add(_options.Crf.ToString());
                break;
            default:
                var x264 = _options.UseComplexConfig
                    ? _x264Params.GetX264Params()
                    : _x264Params.GetSimpleX264Params();
                args.Add("-c:v");
                args.Add("libx264");
                args.Add("-x264-params");
                args.Add(x264);
                break;
        }
    }

    private static void TryKill(Process? p)
    {
        if (p == null) return;
        try
        {
            if (!p.HasExited) p.Kill(entireProcessTree: true);
        }
        catch
        {
            // 进程已退出 / 句柄失效 → 忽略。
        }
    }

    private void EnsureSourceExists()
    {
        if (string.IsNullOrEmpty(_options.SourceVideo) || !File.Exists(_options.SourceVideo))
            throw new FileNotFoundException("视频文件不存在。", _options.SourceVideo);
        if (!string.IsNullOrEmpty(_options.SourceSubtitle) && !File.Exists(_options.SourceSubtitle))
            throw new FileNotFoundException("字幕文件不存在。", _options.SourceSubtitle);
        var dir = Path.GetDirectoryName(_options.OutputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            throw new DirectoryNotFoundException($"输出目录不存在：{dir}");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        try { _cts?.Cancel(); } catch { /* ignored */ }
        TryKill(_vProcess);
        TryKill(_fProcess);

        try { _vProcess?.Dispose(); } catch { /* ignored */ }
        try { _fProcess?.Dispose(); } catch { /* ignored */ }
        try { _cts?.Dispose(); } catch { /* ignored */ }

        _vProcess = null;
        _fProcess = null;
    }

    [GeneratedRegex(@"^frame=\s{0,}(?<FrameNumber>\d*)\s+fps=\s{0,}(?<FramesPerSecond>[\d\.]+)")]
    private static partial Regex FfmpegProgressPattern();
}
