using SekaiToolsMauiText.Models;

namespace SekaiToolsMauiText.Services;

public sealed class PlatformStoryService(SekaiPlatformClient platformClient)
{
    public async Task<IReadOnlyList<string>> GetStoryTypesAsync(CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStoryTypesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformStoryGroup>> GetStoryGroupsAsync(
        string storyType,
        CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStoryGroupsAsync(storyType, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformStory>> GetStoriesAsync(
        long storyGroupId,
        CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStoriesAsync(storyGroupId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformSourceLine>> GetStorySourceLinesAsync(
        long storyId,
        CancellationToken cancellationToken = default)
    {
        return await platformClient.GetStorySourceLinesAsync(storyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformTranslationVersion>> GetTranslationVersionsAsync(
        long storyId,
        CancellationToken cancellationToken = default)
    {
        return await platformClient.GetTranslationVersionsAsync(storyId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformTranslationLine>> GetTranslationLinesAsync(
        long translationVersionId,
        CancellationToken cancellationToken = default)
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
