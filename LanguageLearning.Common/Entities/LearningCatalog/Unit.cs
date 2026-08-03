using LanguageLearning.Common.Entities.Base;

namespace LanguageLearning.Common.Entities.LearningCatalog;

/// <summary>
/// Represents an ordered unit owned by a course.
/// </summary>
public sealed class Unit : BaseEntity, IAuditableEntity
{
    private readonly List<Lesson> _lessons = [];

    public Guid CourseId { get; set; }

    public Course Course { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyCollection<Lesson> Lessons => _lessons.AsReadOnly();
}
