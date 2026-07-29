namespace LanguageLearning.WebApi.Configuration;

/// <summary>
/// Strongly-typed Google OAuth configuration options bound from appsettings.
/// </summary>
public sealed class GoogleSettings
{
    public const string SectionName = "Google";

    public string ClientId { get; init; } = string.Empty;
}
