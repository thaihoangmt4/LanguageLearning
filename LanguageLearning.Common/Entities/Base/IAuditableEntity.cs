namespace LanguageLearning.Common.Entities.Base;

/// <summary>
/// Marks an entity as auditable, automatically tracking creation and modification timestamps.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }

    DateTime? UpdatedAt { get; set; }
}
