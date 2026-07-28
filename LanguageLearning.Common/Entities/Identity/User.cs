using LanguageLearning.Common.Entities.Base;

namespace LanguageLearning.Common.Entities.Identity;

/// <summary>
/// Represents a registered user of the platform.
/// </summary>
public sealed class User : BaseEntity, IAuditableEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
