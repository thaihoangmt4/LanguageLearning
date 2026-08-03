using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.LearningCatalog;

/// <summary>
/// Represents an ordered lesson owned by a unit.
/// </summary>
public sealed class Lesson : BaseEntity, IAuditableEntity
{
    private readonly List<LessonSection> _lessonSections = [];

    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LearningObjectiveSummary { get; set; }

    public int EstimatedDurationMinutes { get; set; }

    public LessonDifficulty DifficultyLevel { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyCollection<LessonSection> LessonSections => _lessonSections.AsReadOnly();
}
