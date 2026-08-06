using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.ExerciseEngine;

public sealed class UserExerciseMistake : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int ExerciseVersion { get; set; }
    public UserExerciseMistakeStatus Status { get; set; } = UserExerciseMistakeStatus.Pending;
    public DateTime FirstFailedAt { get; set; }
    public DateTime LastFailedAt { get; set; }
    public int FailureCount { get; set; }
    public int SuccessfulReviewCount { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public ICollection<LessonAttemptExercise> ReviewActivities { get; set; } = [];
}
