using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.ExerciseEngine;

namespace LanguageLearning.Common.Entities.Identity;

/// <summary>
/// Represents a registered user of the platform.
/// Authentication is handled exclusively through Google OAuth.
/// </summary>
public sealed class User : BaseEntity, IAuditableEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier from Google (the "sub" claim).
    /// Used to match returning users after Google OAuth login.
    /// </summary>
    public string? GoogleId { get; set; }

    /// <summary>
    /// The role assigned to this user (e.g., "Admin", "User").
    /// Defaults to "User" for new Google OAuth registrations.
    /// </summary>
    public string Role { get; set; } = "User";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public UserProfile UserProfile { get; set; } = null!;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<LessonAttempt> LessonAttempts { get; set; } = [];

    public ICollection<UserExerciseMistake> ExerciseMistakes { get; set; } = [];
}
