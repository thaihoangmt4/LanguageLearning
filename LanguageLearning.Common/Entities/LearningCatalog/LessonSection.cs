using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.LearningCatalog;

/// <summary>
/// Represents an ordered instructional section owned by a lesson.
/// </summary>
public sealed class LessonSection : BaseEntity, IAuditableEntity
{
    public Guid LessonId { get; set; }

    public Lesson Lesson { get; set; } = null!;

    public LessonSectionType SectionType { get; set; }

    public string Title { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsRequired { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
