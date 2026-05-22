using System;
using System.IO;
using System.Text;
using SekaiToolsCore;

namespace SekaiToolsApp.Services;

public static class CrashLogService
{
    private static readonly string LogDir = Path.Combine(ResourceManager.DataBaseDir, "Logs");

    public static string WriteLog(Exception ex, string? context = null)
    {
        Directory.CreateDirectory(LogDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"crash_{timestamp}.log";
        var filePath = Path.Combine(LogDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"Runtime: {Environment.Version}");
        if (!string.IsNullOrEmpty(context))
            sb.AppendLine($"Context: {context}");
        sb.AppendLine();
        sb.AppendLine("--- Exception ---");
        sb.AppendLine(ex.ToString());

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    public static string GetLogDir() => LogDir;
}
