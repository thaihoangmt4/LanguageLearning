using LanguageLearning.Common.Entities.Identity;

namespace LanguageLearning.WebApi.Services;

/// <summary>
/// Manages the lifecycle of refresh tokens: generation, validation, and revocation.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Generates a cryptographically random refresh token, hashes it,
    /// and creates a persisted <see cref="RefreshToken"/> entity (not yet saved).
    /// Returns the raw token to send to the client and the entity ready to persist.
    /// </summary>
    (string rawToken, RefreshToken entity) CreateRefreshToken(Guid userId, int expirationDays);

    /// <summary>
    /// Looks up a refresh token by its raw value.
    /// Returns the entity if found and still active; null otherwise.
    /// </summary>
    Task<RefreshToken?> GetByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a refresh token by its raw value (idempotent — succeeds if token is already revoked or not found).
    /// </summary>
    Task RevokeByRawTokenAsync(string rawToken, CancellationToken cancellationToken = default);
}
