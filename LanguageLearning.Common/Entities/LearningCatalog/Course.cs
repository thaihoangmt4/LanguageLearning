using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.LearningCatalog;

/// <summary>
/// Represents a course in the learning catalog.
/// </summary>
public sealed class Course : BaseEntity, IAuditableEntity
{
    private readonly List<Unit> _units = [];

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CefrLevel CefrLevel { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyCollection<Unit> Units => _units.AsReadOnly();

    public ICollection<UserCourseAssignment> UserAssignments { get; set; } = [];
}
