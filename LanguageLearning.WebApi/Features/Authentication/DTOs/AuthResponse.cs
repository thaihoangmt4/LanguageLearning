namespace LanguageLearning.WebApi.Features.Authentication.DTOs;

/// <summary>
/// Returned to the client after successful authentication or token refresh.
/// </summary>
public sealed record AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }
}
