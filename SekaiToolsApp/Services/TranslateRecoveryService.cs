using System.Text;
using System.Text.Json;
using SekaiToolsCore;
using SekaiToolsPlatform.Models;
using SekaiToolsPlatform.ViewModels;

namespace SekaiToolsApp.Services;

/// <summary>
/// 翻译工作台的本地 autosave / recovery 存储。
///
/// 保存策略：
/// - 本地模式：保存当前导出的翻译文本，恢复时写入临时 txt 再走既有文件加载逻辑。
/// - 平台模式：保存当前行快照，恢复时直接回灌到 <see cref="TranslatePageModel"/>。
/// </summary>
public sealed class TranslateRecoveryService
{
    public const string LocalMode = "local";
    public const string PlatformMode = "platform";

    public static TranslateRecoveryService Instance { get; } = new();

    private readonly string _filePath = Path.Combine(ResourceManager.DataBaseDir, "Data", "translate-recovery.json");
    private readonly object _ioLock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private TranslateRecoveryService()
    {
    }

    public bool HasRecovery => File.Exists(_filePath);

    public TranslateRecoveryData? Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<TranslateRecoveryData>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(TranslatePageModel model, string scriptPath, string translationPath)
    {
        if (model.IsEmpty) return;
        Save(CreateSnapshot(model, scriptPath, translationPath));
    }

    public void Save(TranslateRecoveryData data)
    {
        lock (_ioLock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(data, _jsonOptions);
                File.WriteAllText(_filePath, json, Encoding.UTF8);
            }
            catch
            {
                // Recovery 只是兜底数据，写失败不阻断主流程。
            }
        }
    }

    public void Clear()
    {
        lock (_ioLock)
        {
            try
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
            }
            catch
            {
                // 清理失败不影响主流程。
            }
        }
    }

    private static TranslateRecoveryData CreateSnapshot(
        TranslatePageModel model,
        string scriptPath,
        string translationPath)
    {
        if (model.IsPlatformMode && model.CurrentPlatformStoryId > 0)
        {
            var selectedStory = model.SelectedStory ?? new PlatformStory(
                model.CurrentPlatformStoryId,
                null,
                string.Empty,
                string.Empty,
                model.CurrentDocumentTitle,
                0);

            return new TranslateRecoveryData
            {
                Mode = PlatformMode,
                SavedAt = DateTimeOffset.Now,
                DocumentTitle = model.CurrentDocumentTitle,
                ScriptPath = scriptPath,
                TranslationPath = translationPath,
                PlatformStory = selectedStory,
                SourceLines = BuildSourceLines(model, selectedStory.Id),
                TranslationLines = BuildTranslationLines(model, selectedStory.Id),
            };
        }

        return new TranslateRecoveryData
        {
            Mode = LocalMode,
            SavedAt = DateTimeOffset.Now,
            DocumentTitle = model.CurrentDocumentTitle,
            ScriptPath = scriptPath,
            TranslationPath = translationPath,
            LocalResult = model.Result,
        };
    }

    private static PlatformSourceLine[] BuildSourceLines(TranslatePageModel model, long storyId)
    {
        return model.Events
            .Select(line =>
            {
                var sourceLineId = line.SourceLineId ?? 0;
                var sourceLineNo = line.SourceLineNo ?? 0;
                var lineType = line.SourceLineType ?? (line is LineDialogModel ? "dialogue" : "effect");
                var metadata = line.SourceLineMetadata;

                return line switch
                {
                    LineDialogModel dialog => new PlatformSourceLine(
                        sourceLineId,
                        storyId,
                        sourceLineNo,
                        lineType,
                        dialog.OriginalCharacter,
                        dialog.OriginalContent,
                        metadata),
                    LineEffectModel effect => new PlatformSourceLine(
                        sourceLineId,
                        storyId,
                        sourceLineNo,
                        lineType,
                        null,
                        effect.OriginalContent,
                        metadata),
                    _ => new PlatformSourceLine(
                        sourceLineId,
                        storyId,
                        sourceLineNo,
                        lineType,
                        null,
                        line.Result,
                        metadata),
                };
            })
            .ToArray();
    }

    private static PlatformTranslationLine[] BuildTranslationLines(TranslatePageModel model, long storyId)
    {
        return model.Events
            .Select(line =>
            {
                var sourceLineId = line.SourceLineId ?? 0;
                var sourceLineNo = line.SourceLineNo ?? 0;
                var metadata = line.TranslationLineMetadata;

                return line switch
                {
                    LineDialogModel dialog => new PlatformTranslationLine(
                        0,
                        0,
                        sourceLineId,
                        storyId,
                        sourceLineNo,
                        string.IsNullOrWhiteSpace(dialog.TranslatedCharacter) ? null : dialog.TranslatedCharacter,
                        dialog.TranslatedContent,
                        metadata),
                    LineEffectModel effect => new PlatformTranslationLine(
                        0,
                        0,
                        sourceLineId,
                        storyId,
                        sourceLineNo,
                        null,
                        effect.TranslatedContent,
                        metadata),
                    _ => new PlatformTranslationLine(
                        0,
                        0,
                        sourceLineId,
                        storyId,
                        sourceLineNo,
                        null,
                        line.Result,
                        metadata),
                };
            })
            .ToArray();
    }
}

public sealed class TranslateRecoveryData
{
    public string Mode { get; set; } = TranslateRecoveryService.LocalMode;
    public DateTimeOffset SavedAt { get; set; }
    public string? ScriptPath { get; set; }
    public string? TranslationPath { get; set; }
    public string? DocumentTitle { get; set; }
    public string? LocalResult { get; set; }
    public PlatformStory? PlatformStory { get; set; }
    public PlatformSourceLine[] SourceLines { get; set; } = [];
    public PlatformTranslationLine[] TranslationLines { get; set; } = [];
}
