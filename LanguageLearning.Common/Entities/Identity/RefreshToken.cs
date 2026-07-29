namespace LanguageLearning.Common.Entities.Identity;

/// <summary>
/// Stores a hashed refresh token for a user session.
/// Supports rotation: each use revokes the old token and issues a new one.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// SHA-256 hash of the raw refresh token. The raw value is never persisted.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => !IsRevoked && !IsExpired;
}
