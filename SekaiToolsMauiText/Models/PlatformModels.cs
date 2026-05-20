using System.Text.Json.Serialization;

namespace SekaiToolsMauiText.Models;

public sealed record PlatformUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("qq_id")] string? QqId,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl);

public sealed record PlatformTenant(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("role")] string Role);

public sealed record PlatformAuthSession(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("user")] PlatformUser User,
    [property: JsonPropertyName("current_tenant")] PlatformTenant? CurrentTenant,
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<PlatformTenant> Tenants);

public sealed record PlatformStoryGroup(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("story_type")] string StoryType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subtitle")] string? Subtitle,
    [property: JsonPropertyName("display_no")] int? DisplayNo,
    [property: JsonPropertyName("story_count")] int StoryCount)
{
    public string DisplayLabel => DisplayNo.HasValue ? $"{DisplayNo:000} {Title}" : Title;
}

public sealed record PlatformStory(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("group_id")] long? GroupId,
    [property: JsonPropertyName("story_type")] string StoryType,
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("sort_order")] int SortOrder)
{
    public string DisplayLabel => $"{SortOrder:000} {Title}";
}

public sealed record PlatformSourceLine(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("story_id")] long StoryId,
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("line_type")] string LineType,
    [property: JsonPropertyName("speaker")] string? Speaker,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("metadata")] string? Metadata);

public sealed record PlatformTranslationVersion(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("story_id")] long StoryId,
    [property: JsonPropertyName("version_no")] int VersionNo,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("created_by")] long CreatedBy,
    [property: JsonPropertyName("created_by_name")] string? CreatedByName,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt)
{
    public string DisplayLabel => $"v{VersionNo} {Title ?? string.Empty}".Trim();
}

public sealed record PlatformTranslationLine(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("version_id")] long VersionId,
    [property: JsonPropertyName("source_line_id")] long SourceLineId,
    [property: JsonPropertyName("story_id")] long StoryId,
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("speaker")] string? Speaker,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("metadata")] string? Metadata);

public sealed record PlatformCreateTranslationLineRequest(
    [property: JsonPropertyName("source_line_id")] long SourceLineId,
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("speaker")] string? Speaker,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("metadata")] string? Metadata);

public sealed record PlatformCreateTranslationVersionRequest(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("lines")] IReadOnlyCollection<PlatformCreateTranslationLineRequest> Lines);

public sealed record PlatformCreateTranslationVersionResponse(
    [property: JsonPropertyName("version")] PlatformTranslationVersion Version,
    [property: JsonPropertyName("line_count")] int LineCount);

public sealed record PlatformLoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

public sealed record PlatformSwitchTenantRequest(
    [property: JsonPropertyName("tenant_id")] long TenantId);

public sealed record PlatformError(
    [property: JsonPropertyName("msg")] string Msg,
    [property: JsonPropertyName("trace_id")] string? TraceId);
