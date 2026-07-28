namespace LanguageLearning.WebApi.Configuration;

/// <summary>
/// Strongly-typed JWT configuration options bound from appsettings.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public int ExpirationInMinutes { get; init; } = 60;

    public int RefreshTokenExpirationInDays { get; init; } = 7;
}
