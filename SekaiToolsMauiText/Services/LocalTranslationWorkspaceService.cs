using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;

namespace SekaiToolsMauiText.Services;

public sealed class LocalTranslationWorkspaceService
{
    public Story LoadStory(string path)
    {
        return Story.FromFile(path);
    }

    public Story LoadStoryWithTranslation(string scriptPath, string translationPath)
    {
        var translationData = new TranslationData(translationPath);
        foreach (var translation in translationData.Translations)
        {
            translation.Body = translation.Body.Replace("\\N", "\n");
        }

        var gameScript = new GameScript(scriptPath);
        if (!translationData.IsApplicable(gameScript))
        {
            throw new InvalidOperationException("翻译数据不适用于此剧本");
        }

        return new Story(gameScript, translationData);
    }

    public Story LoadReferenceStory(string scriptPath, string translationPath)
    {
        return LoadStoryWithTranslation(scriptPath, translationPath);
    }

    public async Task SaveTranslationAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }
}
