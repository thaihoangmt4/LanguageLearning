using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.ExerciseEngine;

public sealed class ExerciseAttempt : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Guid LessonAttemptExerciseId { get; set; }
    public LessonAttemptExercise LessonAttemptExercise { get; set; } = null!;
    public int ExerciseVersion { get; set; }
    public int AttemptNumber { get; set; }
    public string AnswerJson { get; set; } = "{}";
    public EvaluationStatus EvaluationStatus { get; set; } = EvaluationStatus.NotEvaluated;
    public decimal? Score { get; set; }
    public string? Feedback { get; set; }
    public string? ResultJson { get; set; }
    public DateTime SubmittedAt { get; set; }
}
