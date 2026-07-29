using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.Authentication.DTOs;
using LanguageLearning.WebApi.Services;
using MediatR;
using System.Security.Claims;

namespace LanguageLearning.WebApi.Features.Authentication.Commands;

/// <summary>
/// Exchanges a valid refresh token for a new access token and rotated refresh token.
/// </summary>

public class RefreshTokenCommand : IRequest<Result<AuthResponse>>
{
    public string RefreshToken { get; set; }

    public class Handler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
    {
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly ApplicationDbContext _dbContext;
        private readonly TokenGenerationOptions _tokenOptions;

        public Handler(TokenGenerationOptions tokenOptions, IRefreshTokenService refreshTokenService, IJwtTokenGenerator jwtTokenGenerator, ApplicationDbContext dbContext)
        {
            _tokenOptions = tokenOptions;
            _refreshTokenService = refreshTokenService;
            _jwtTokenGenerator = jwtTokenGenerator;
            _dbContext = dbContext;
        }

        public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
        {
            // 1. Validate the existing refresh token
            var existingToken = await _refreshTokenService.GetByRawTokenAsync(
                command.RefreshToken, cancellationToken);

            if (existingToken is null)
                return Result<AuthResponse>.Failure("Invalid or expired refresh token.");

            // 2. Revoke the old token (rotation)
            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;

            // 3. Generate new tokens
            var user = existingToken.User;

            var accessToken = GenerateAccessToken(user);

            var (rawToken, newRefreshTokenEntity) = _refreshTokenService.CreateRefreshToken(
                user.Id,
                _tokenOptions.RefreshTokenExpirationDays);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Success(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = rawToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_tokenOptions.AccessTokenExpirationMinutes)
            });
        }

        private string GenerateAccessToken(Common.Entities.Identity.User user)
        {
            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };

            return _jwtTokenGenerator.GenerateAccessToken(claims);
        }
    }
}