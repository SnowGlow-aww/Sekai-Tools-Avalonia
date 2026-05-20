using SekaiToolsPlatform.Models;

namespace SekaiToolsPlatform.Services;

/// <summary>
/// 故事 / 翻译版本域：拉取列表、读译文行、提交新版本。
/// 是 <see cref="SekaiPlatformClient"/> 之上的薄封装；分层主要是给 UI 注入用。
/// </summary>
public sealed class PlatformStoryService(SekaiPlatformClient platformClient)
{
    public async Task<IReadOnlyList<string>> GetStoryTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStoryTypesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformStoryGroup>> GetStoryGroupsAsync(
        string storyType, CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStoryGroupsAsync(storyType, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformStory>> GetStoriesAsync(
        long storyGroupId, CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStoriesAsync(storyGroupId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformSourceLine>> GetStorySourceLinesAsync(
        long storyId, CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStorySourceLinesAsync(storyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformTranslationVersion>> GetTranslationVersionsAsync(
        long storyId, CancellationToken cancellationToken = default)
    {
        return await platformClient.GetTranslationVersionsAsync(storyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformTranslationLine>> GetTranslationLinesAsync(
        long translationVersionId, CancellationToken cancellationToken = default)
    {
        return await platformClient.GetTranslationLinesAsync(translationVersionId, cancellationToken);
    }

    public async Task<PlatformCreateTranslationVersionResponse> CreateTranslationVersionAsync(
        long storyId,
        PlatformCreateTranslationVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await platformClient.CreateTranslationVersionAsync(storyId, request, cancellationToken);
    }
}
