namespace LanguageLearning.Common.Configuration;

/// <summary>
/// Options for Google OAuth token verification.
/// </summary>
public sealed class GoogleAuthOptions
{
    public string ClientId { get; init; } = string.Empty;
}
