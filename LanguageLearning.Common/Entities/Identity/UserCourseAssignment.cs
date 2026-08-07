using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.Identity;

public sealed class UserCourseAssignment : BaseEntity, IAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public UserCourseAssignmentStatus Status { get; set; } = UserCourseAssignmentStatus.Assigned;
    public DateTime AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
