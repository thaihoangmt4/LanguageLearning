using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;

public sealed record StartLearningSessionResponse(string Status, Guid? LessonAttemptId, Guid? LessonId);

public sealed record LessonAttemptPlayerResponse(
    LessonAttemptDto Attempt,
    LessonSummaryDto Lesson,
    IReadOnlyList<LearningActivityDto> Activities);

public sealed record LessonAttemptDto(Guid Id, Guid LessonId, LessonAttemptStatus Status, DateTime StartedAt,
    DateTime? CompletedAt, Guid? CurrentActivityId, int CompletedActivityCount, int TotalActivityCount,
    decimal TotalScore, int CorrectCount, int IncorrectCount);
public sealed record LessonSummaryDto(Guid Id, string Title, string? Description);
public sealed record LearningActivityDto(Guid ActivityId, Guid ExerciseId, ActivityType ActivityType,
    ExerciseType ExerciseType, string Title, string Instruction, DifficultyLevel Difficulty, int DisplayOrder,
    int ExerciseVersion, bool IsRequired, string Status, LatestActivityResultDto? LatestResult, object Content);
public sealed record LatestActivityResultDto(EvaluationStatus Status, decimal? Score, string? Feedback,
    int AttemptNumber, DateTime SubmittedAt);
