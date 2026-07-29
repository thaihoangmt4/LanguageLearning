namespace LanguageLearning.WebApi.Services;

/// <summary>
/// Verifies a Google ID token and returns the validated payload.
/// </summary>
public interface IGoogleTokenVerifier
{
    /// <summary>
    /// Validates the given Google ID token and returns the parsed payload.
    /// Returns null if the token is invalid or expired.
    /// </summary>
    Task<GoogleTokenPayload?> VerifyAsync(string idToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// The verified claims extracted from a Google ID token.
/// </summary>
public sealed record GoogleTokenPayload
{
    public string Sub { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
}
