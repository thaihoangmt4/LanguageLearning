using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.ExerciseEngine;

public sealed class LessonAttemptExercise : BaseEntity
{
    public Guid LessonAttemptId { get; set; }
    public LessonAttempt LessonAttempt { get; set; } = null!;
    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int ExerciseVersion { get; set; }
    public ActivityType ActivityType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; }
    public Guid SourceLessonId { get; set; }
    public Lesson SourceLesson { get; set; } = null!;
    public Guid? UserExerciseMistakeId { get; set; }
    public UserExerciseMistake? UserExerciseMistake { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<ExerciseAttempt> ExerciseAttempts { get; set; } = [];
}
