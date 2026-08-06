using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.LearningCatalog;

/// <summary>
/// Represents an ordered lesson owned by a unit.
/// </summary>
public sealed class Lesson : BaseEntity, IAuditableEntity
{
    private readonly List<Exercise> _exercises = [];

    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LearningObjectiveSummary { get; set; }

    public int EstimatedDurationMinutes { get; set; }

    public DifficultyLevel DifficultyLevel { get; set; }

    public int DisplayOrder { get; set; }

    public LessonStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyCollection<Exercise> Exercises => _exercises.AsReadOnly();
}
