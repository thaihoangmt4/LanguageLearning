namespace LanguageLearning.Common.Configuration;

/// <summary>
/// Options for JWT access token and refresh token generation.
/// </summary>
public sealed class TokenGenerationOptions
{
    public string Secret { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public int AccessTokenExpirationMinutes { get; init; } = 15;

    public int RefreshTokenExpirationDays { get; init; } = 30;
}
