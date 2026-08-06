using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.ExerciseEngine;

public sealed class LessonAttempt : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public LessonAttemptStatus Status { get; set; } = LessonAttemptStatus.InProgress;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal TotalScore { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int CompletedActivityCount { get; set; }
    public int TotalActivityCount { get; set; }
    public ICollection<LessonAttemptExercise> Activities { get; set; } = [];
}
