using System.Security.Claims;

namespace LanguageLearning.WebApi.Services;

/// <summary>
/// Generates short-lived JWT access tokens for authenticated users.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Creates a signed JWT access token containing the given claims.
    /// </summary>
    string GenerateAccessToken(IEnumerable<Claim> claims);
}
