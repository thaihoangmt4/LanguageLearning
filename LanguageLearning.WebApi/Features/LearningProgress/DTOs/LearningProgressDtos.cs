using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;

namespace LanguageLearning.WebApi.Features.LearningProgress.DTOs;

public sealed record ContinueLearningResponse(
    string State,
    Guid? CourseId,
    Guid? LessonAttemptId,
    Guid? NextActivityId,
    NextLessonDto? NextLesson);
public sealed record NextLessonDto(Guid Id, string Title, string UnitTitle, int EstimatedDurationMinutes);

public sealed record LearningSessionResponse(string Mode, LessonAttemptPlayerResponse Session);

public sealed record LearningProgressResponse(
    string State,
    CourseProgressDto? Course,
    int CompletedLessonCount,
    int TotalLessonCount,
    decimal ProgressPercentage,
    IReadOnlyList<UnitProgressDto> Units);
public sealed record CourseProgressDto(
    Guid AssignmentId,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    UserCourseAssignmentStatus AssignmentStatus);
public sealed record UnitProgressDto(Guid Id, string Code, string Title, int DisplayOrder, IReadOnlyList<LessonProgressDto> Lessons);
public sealed record LessonProgressDto(Guid Id, string Code, string Title, int DisplayOrder, string State, Guid? LessonAttemptId);

public sealed record LearningHistoryResponse(int PageNumber, int PageSize, int TotalCount, IReadOnlyList<LessonHistoryItemDto> Items);
public sealed record LessonHistoryItemDto(Guid LessonAttemptId, Guid LessonId, string LessonTitle,
    LessonAttemptStatus Status, DateTime StartedAt, DateTime? LastAccessedAt, DateTime? CompletedAt,
    decimal TotalScore, int CompletedActivityCount, int TotalActivityCount);

public sealed record LessonAttemptResultResponse(Guid LessonAttemptId, Guid LessonId, string LessonTitle,
    LessonAttemptStatus Status, DateTime StartedAt, DateTime? CompletedAt, decimal TotalScore,
    int CorrectCount, int IncorrectCount, int CompletedActivityCount, int TotalActivityCount,
    IReadOnlyList<ActivityResultDto> Activities);
public sealed record ActivityResultDto(Guid ActivityId, Guid ExerciseId, string ExerciseTitle, int DisplayOrder,
    bool Completed, EvaluationStatus? EvaluationStatus, decimal? Score, int? AttemptNumber, DateTime? SubmittedAt);
