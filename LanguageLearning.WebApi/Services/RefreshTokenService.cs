using System.Security.Cryptography;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Services;

/// <summary>
/// Manages the lifecycle of refresh tokens using SHA-256 hashing for storage
/// and cryptographic randomness for generation.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITokenHasher _tokenHasher;

    public RefreshTokenService(ApplicationDbContext dbContext, ITokenHasher tokenHasher)
    {
        _dbContext = dbContext;
        _tokenHasher = tokenHasher;
    }

    public (string rawToken, RefreshToken entity) CreateRefreshToken(Guid userId, int expirationDays)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = _tokenHasher.Hash(rawToken);

        var entity = new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _dbContext.RefreshTokens.Add(entity);

        return (rawToken, entity);
    }

    public async Task<RefreshToken?> GetByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenHasher.Hash(rawToken);

        var entity = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (entity is null || !entity.IsActive)
            return null;

        return entity;
    }

    public async Task RevokeByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenHasher.Hash(rawToken);

        var entity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (entity is not null && entity.IsActive)
        {
            entity.IsRevoked = true;
            entity.RevokedAt = DateTime.UtcNow;
        }
    }
}
