using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SekaiToolsCore;

namespace SekaiToolsApp.Services;

public enum SuppressBackend
{
    VapourSynth,
    Ffmpeg,
}

public sealed record SuppressRuntimeDescriptor(
    SuppressBackend Backend,
    string FfmpegPath,
    string? VapourSynthPath = null,
    string? VapourScriptPath = null);

public sealed record SuppressRuntimeProbe(
    bool IsReady,
    string Message,
    SuppressRuntimeDescriptor? Descriptor = null);

public static class SuppressRuntimeService
{
    private static readonly string[] FfmpegExecutableNames =
        OperatingSystem.IsWindows() ? ["ffmpeg.exe", "ffmpeg"] : ["ffmpeg"];

    private static readonly string[] VapourExecutableNames =
        OperatingSystem.IsWindows() ? ["VSPipe.exe"] : ["VSPipe", "vspipe"];

    public static SuppressRuntimeProbe Probe(string? ffmpegPathHint = null)
    {
        if (TryResolveVapourSynth(ffmpegPathHint, out var legacyDescriptor, out var legacyMessage))
            return new SuppressRuntimeProbe(true, legacyMessage, legacyDescriptor);

        if (TryResolveFfmpeg(ffmpegPathHint, out var ffmpegPath, out var ffmpegMessage))
        {
            return new SuppressRuntimeProbe(true, ffmpegMessage,
                new SuppressRuntimeDescriptor(SuppressBackend.Ffmpeg, ffmpegPath));
        }

        return new SuppressRuntimeProbe(false, BuildFailureMessage());
    }

    public static SuppressRuntimeDescriptor Resolve(string? ffmpegPathHint = null)
    {
        var probe = Probe(ffmpegPathHint);
        if (!probe.IsReady || probe.Descriptor is null)
            throw new FileNotFoundException(probe.Message);

        return probe.Descriptor;
    }

    public static async Task<List<VideoEncoder>> ProbeAvailableEncodersAsync(string? ffmpegPathHint = null)
    {
        var available = new List<VideoEncoder> { VideoEncoder.Libx264 };

        if (!TryResolveFfmpeg(ffmpegPathHint, out var ffmpegPath, out _))
            return available;

        var encoderMap = new Dictionary<string, VideoEncoder>
        {
            ["h264_videotoolbox"] = VideoEncoder.H264VideoToolbox,
            ["hevc_videotoolbox"] = VideoEncoder.HevcVideoToolbox,
            ["h264_nvenc"] = VideoEncoder.H264Nvenc,
            ["hevc_nvenc"] = VideoEncoder.HevcNvenc,
            ["av1_nvenc"] = VideoEncoder.Av1Nvenc,
            ["h264_qsv"] = VideoEncoder.H264Qsv,
            ["hevc_qsv"] = VideoEncoder.HevcQsv,
            ["av1_qsv"] = VideoEncoder.Av1Qsv,
        };

        var softwareMap = new Dictionary<string, VideoEncoder>
        {
            ["libx265"] = VideoEncoder.Libx265,
            ["libsvtav1"] = VideoEncoder.LibSvtAv1,
        };

        try
        {
            var psi = new ProcessStartInfo(ffmpegPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-encoders");

            using var proc = Process.Start(psi);
            if (proc == null) return available;

            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            foreach (var (name, encoder) in encoderMap)
            {
                if (output.Contains(name))
                    available.Add(encoder);
            }

            foreach (var (name, encoder) in softwareMap)
            {
                if (output.Contains(name))
                    available.Add(encoder);
            }
        }
        catch
        {
            // probe 失败不阻塞
        }

        return available;
    }

    private static bool TryResolveVapourSynth(
        string? ffmpegPathHint,
        out SuppressRuntimeDescriptor? descriptor,
        out string message)
    {
        descriptor = null;
        message = string.Empty;

        var vapourPath = ResolveExecutable(VapourExecutableNames);
        if (vapourPath is null)
            return false;

        var scriptPath = ResolveScript();
        if (scriptPath is null)
            return false;

        if (!TryResolveFfmpeg(ffmpegPathHint, out var ffmpegPath, out _))
            return false;

        descriptor = new SuppressRuntimeDescriptor(
            SuppressBackend.VapourSynth,
            ffmpegPath,
            vapourPath,
            scriptPath);
        message = $"已检测到 VapourSynth 压制环境（{Path.GetFileName(vapourPath)} + ffmpeg）。";
        return true;
    }

    private static bool TryResolveFfmpeg(
        string? ffmpegPathHint,
        out string ffmpegPath,
        out string message)
    {
        ffmpegPath = ResolveExecutable(FfmpegExecutableNames, ffmpegPathHint) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            message = string.Empty;
            return false;
        }

        message = $"已检测到 ffmpeg 压制环境（{ffmpegPath}）。";
        return true;
    }

    private static string? ResolveExecutable(IEnumerable<string> candidateNames, string? configuredPath = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var full = Path.GetFullPath(configuredPath);
            if (File.Exists(full))
                return full;

            var fromPath = FindOnPath(configuredPath);
            if (fromPath is not null)
                return fromPath;
        }

        foreach (var candidate in candidateNames)
        {
            foreach (var root in SearchRoots())
            {
                var path = Path.Combine(root, candidate);
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }
        }

        foreach (var candidate in candidateNames)
        {
            var path = FindOnPath(candidate);
            if (path is not null)
                return path;
        }

        return null;
    }

    private static string? ResolveScript()
    {
        foreach (var path in ScriptSearchPaths())
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    private static IEnumerable<string> SearchRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "Resources");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources"));
        yield return Path.Combine(ResourceManager.DataBaseDir, "Resource", "vapourSynth");
    }

    private static IEnumerable<string> ScriptSearchPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Resources", "lim5994.vpy");
        yield return Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources")), "lim5994.vpy");
        yield return Path.Combine(AppContext.BaseDirectory, "lim5994.vpy");
        yield return Path.Combine(ResourceManager.DataBaseDir, "Resource", "vapourSynth", "lim5994.vpy");
    }

    private static string? FindOnPath(string command)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        var directories = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidates = ExpandPathCandidates(command);

        foreach (var directory in directories)
        {
            foreach (var candidate in candidates)
            {
                var path = Path.Combine(directory, candidate);
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static IEnumerable<string> ExpandPathCandidates(string command)
    {
        if (Path.HasExtension(command))
        {
            yield return command;
            yield break;
        }

        yield return command;
        if (!OperatingSystem.IsWindows())
            yield break;

        var pathext = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        foreach (var ext in pathext.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return command + ext;
        }
    }

    private static string BuildFailureMessage()
    {
        return
            "未找到可用的压制运行环境。\n" +
            "请在设置里指定 ffmpeg 路径，或把 ffmpeg 放到 PATH。\n" +
            "如果你已有 VapourSynth / VSPipe，也可以把它们放到应用目录或 PATH。";
    }
}
