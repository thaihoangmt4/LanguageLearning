using LanguageLearning.Common.Entities.Base;

namespace LanguageLearning.Common.Entities.Identity;

/// <summary>
/// Stores the learning profile for a platform user.
/// </summary>
public sealed class UserProfile : BaseEntity, IAuditableEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? NativeLanguageCode { get; set; }

    public string? TimeZoneId { get; set; }

    public int DailyGoalMinutes { get; set; } = 15;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
