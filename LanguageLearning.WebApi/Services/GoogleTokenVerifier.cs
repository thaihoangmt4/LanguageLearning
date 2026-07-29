using Google.Apis.Auth;
using LanguageLearning.Common.Configuration;

namespace LanguageLearning.WebApi.Services;

/// <summary>
/// Validates Google ID tokens against Google's public signing keys.
/// </summary>
public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly GoogleAuthOptions _options;

    public GoogleTokenVerifier(GoogleAuthOptions options)
    {
        _options = options;
    }

    public async Task<GoogleTokenPayload?> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _options.ClientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleTokenPayload
            {
                Sub = payload.Subject,
                Email = payload.Email,
                Name = payload.Name,
                EmailVerified = payload.EmailVerified
            };
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
