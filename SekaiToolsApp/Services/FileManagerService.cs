using System.Diagnostics;
using System.IO;

namespace SekaiToolsApp.Services;

/// <summary>
/// 跨平台打开系统文件管理器的薄封装。
///
/// 统一处理：
/// - 打开目录
/// - 高亮指定文件
/// - Windows / macOS / Linux 的进程参数差异
/// </summary>
public static class FileManagerService
{
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Directory.CreateDirectory(path);

            if (OperatingSystem.IsWindows())
            {
                Start("explorer.exe", path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Start("open", path);
            }
            else
            {
                Start("xdg-open", path);
            }
        }
        catch
        {
            // 文件管理器调用失败不影响主流程。
        }
    }

    public static void RevealPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var arg = File.Exists(path)
                    ? $"/select,{path}"
                    : Directory.Exists(path)
                        ? path
                        : Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(arg))
                    Start("explorer.exe", arg);
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                if (File.Exists(path))
                {
                    Start("open", "-R", path);
                }
                else if (Directory.Exists(path))
                {
                    OpenFolder(path);
                }
                else
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(dir))
                        OpenFolder(dir);
                }
                return;
            }

            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
                OpenFolder(folder);
        }
        catch
        {
            // 文件管理器调用失败不影响主流程。
        }
    }

    private static void Start(string fileName, params string[] arguments)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        Process.Start(psi);
    }
}
