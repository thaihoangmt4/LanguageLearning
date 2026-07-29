using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LanguageLearning.Common.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LanguageLearning.WebApi.Services;

/// <summary>
/// Creates signed JWT access tokens using the configured signing key and expiration.
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly TokenGenerationOptions _options;

    public JwtTokenGenerator(TokenGenerationOptions options)
    {
        _options = options;
    }

    public string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
