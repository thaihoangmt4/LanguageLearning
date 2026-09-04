using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;

namespace LanguageLearning.Common.Entities.ExerciseEngine;

public sealed class UserLessonProgress : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public DateTime CompletedAt { get; set; }
}
