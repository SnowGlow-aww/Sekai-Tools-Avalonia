using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SekaiToolsMauiText.Models;

namespace SekaiToolsMauiText.Services;

public sealed class SekaiPlatformClient
{
    private const string BaseUrlKey = "SekaiPlatform_BaseUrl";
    private const string AccessTokenKey = "SekaiPlatform_AccessToken";

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CookieContainer _cookieContainer = new();
    private readonly HttpClient _httpClient;

    public SekaiPlatformClient()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = _cookieContainer
        };
        _httpClient = new HttpClient(handler);
        UpdateBaseAddress(GetBaseUrl());
        ApplyTokenFromPreferences();
    }

    public string GetBaseUrl()
    {
        return Preferences.Get(BaseUrlKey, "http://localhost:8080").TrimEnd('/');
    }

    public void SetBaseUrl(string baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:8080" : baseUrl.Trim().TrimEnd('/');
        Preferences.Set(BaseUrlKey, normalized);
        UpdateBaseAddress(normalized);
    }

    public async Task<PlatformAuthSession> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<PlatformAuthSession>(
            HttpMethod.Post,
            "/api/auth/login",
            new PlatformLoginRequest(username, password),
            includeAuthorization: false,
            cancellationToken: cancellationToken);
        SaveToken(result.AccessToken);
        return result;
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        SaveToken(null);
        return SendAsync<object>(
            HttpMethod.Post,
            "/api/auth/logout",
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    public async Task<PlatformAuthSession> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await SendAsync<PlatformAuthSession>(
            HttpMethod.Get,
            "/api/auth/session",
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            SaveToken(session.AccessToken);
        }

        return session;
    }

    public async Task<PlatformAuthSession> SwitchTenantAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var session = await SendAsync<PlatformAuthSession>(
            HttpMethod.Put,
            "/api/auth/current-tenant",
            new PlatformSwitchTenantRequest(tenantId),
            includeAuthorization: true,
            cancellationToken: cancellationToken);
        SaveToken(session.AccessToken);
        return session;
    }

    public Task<IReadOnlyList<string>> GetStoryTypesAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<IReadOnlyList<string>>(
            HttpMethod.Get,
            "/api/story-types",
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<PlatformStoryGroup>> GetStoryGroupsAsync(string storyType, CancellationToken cancellationToken = default)
    {
        var path = $"/api/story-groups?story_type={Uri.EscapeDataString(storyType)}";
        return SendAsync<IReadOnlyList<PlatformStoryGroup>>(
            HttpMethod.Get,
            path,
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<PlatformStory>> GetStoriesAsync(long storyGroupId, CancellationToken cancellationToken = default)
    {
        var path = $"/api/stories?story_group_id={storyGroupId}";
        return SendAsync<IReadOnlyList<PlatformStory>>(
            HttpMethod.Get,
            path,
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<PlatformSourceLine>> GetStorySourceLinesAsync(long storyId, CancellationToken cancellationToken = default)
    {
        return SendAsync<IReadOnlyList<PlatformSourceLine>>(
            HttpMethod.Get,
            $"/api/stories/{storyId}/source-lines",
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<PlatformTranslationVersion>> GetTranslationVersionsAsync(long storyId, CancellationToken cancellationToken = default)
    {
        return SendAsync<IReadOnlyList<PlatformTranslationVersion>>(
            HttpMethod.Get,
            $"/api/stories/{storyId}/translation-versions",
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<PlatformTranslationLine>> GetTranslationLinesAsync(long translationVersionId, CancellationToken cancellationToken = default)
    {
        return SendAsync<IReadOnlyList<PlatformTranslationLine>>(
            HttpMethod.Get,
            $"/api/translation-versions/{translationVersionId}/lines",
            null,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    public Task<PlatformCreateTranslationVersionResponse> CreateTranslationVersionAsync(
        long storyId,
        PlatformCreateTranslationVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PlatformCreateTranslationVersionResponse>(
            HttpMethod.Post,
            $"/api/stories/{storyId}/translation-versions",
            request,
            includeAuthorization: true,
            cancellationToken: cancellationToken);
    }

    private void UpdateBaseAddress(string baseUrl)
    {
        _httpClient.BaseAddress = new Uri($"{baseUrl}/");
    }

    private void ApplyTokenFromPreferences()
    {
        var token = Preferences.Get(AccessTokenKey, string.Empty);
        if (string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private void SaveToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Preferences.Remove(AccessTokenKey);
            _httpClient.DefaultRequestHeaders.Authorization = null;
            return;
        }

        Preferences.Set(AccessTokenKey, token);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        bool includeAuthorization,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!includeAuthorization)
        {
            request.Headers.Authorization = null;
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: _jsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(object))
            {
                return (T)(object)new object();
            }

            var result = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
            return result ?? throw new InvalidOperationException("Response body is empty.");
        }

        var message = await ReadErrorMessageAsync(response, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            SaveToken(null);
        }

        throw new InvalidOperationException(message);
    }

    private async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<PlatformError>(_jsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Msg))
            {
                return error.Msg;
            }
        }
        catch
        {
            // ignore parse failure
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(raw)
            ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
            : raw;
    }
}
