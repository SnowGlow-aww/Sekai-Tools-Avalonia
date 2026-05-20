using SekaiToolsMauiText.Models;

namespace SekaiToolsMauiText.Services;

public sealed class PlatformSessionService(SekaiPlatformClient platformClient)
{
    public string GetBaseUrl()
    {
        return platformClient.GetBaseUrl();
    }

    public async Task<PlatformAuthSession> RefreshSessionAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        platformClient.SetBaseUrl(baseUrl);
        return await platformClient.GetSessionAsync(cancellationToken);
    }

    public async Task<PlatformAuthSession> LoginAsync(
        string baseUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        platformClient.SetBaseUrl(baseUrl);
        return await platformClient.LoginAsync(username, password, cancellationToken);
    }

    public async Task<PlatformAuthSession> SwitchTenantAsync(
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        return await platformClient.SwitchTenantAsync(tenantId, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await platformClient.LogoutAsync(cancellationToken);
    }
}
