using System.Text.Json;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;

public sealed record SubmitActivityAnswerRequest
{
    public Guid SubmissionId { get; init; }
    public int ExerciseVersion { get; init; }
    public JsonElement Answer { get; init; }
}

public sealed record SubmitActivityAnswerResponse(Guid SubmissionId, Guid LessonAttemptId, Guid ActivityId,
    Guid ExerciseId, int ExerciseVersion, int AttemptNumber, SubmissionEvaluationDto Evaluation,
    SubmissionProgressDto Progress, DateTime SubmittedAt, bool IsIdempotentReplay);
public sealed record SubmissionEvaluationDto(EvaluationStatus Status, decimal? Score, string? Feedback,
    string? Explanation, object? CorrectAnswer, object? Details);
public sealed record SubmissionProgressDto(int CompletedActivityCount, int TotalActivityCount,
    bool IsLessonCompleted, Guid? NextActivityId);

public sealed record CorrectOptionDto(Guid CorrectOptionId);
public sealed record ExpectedTextDto(string ExpectedText);
public sealed record CorrectOrderDto(IReadOnlyList<Guid> OrderedTokenIds);
public sealed record CorrectMatchesDto(IReadOnlyList<MatchDto> Matches);
public sealed record CorrectAssignmentsDto(IReadOnlyList<AssignmentDto> Assignments);
public sealed record MatchDto(Guid SourceId, Guid TargetId);
public sealed record AssignmentDto(Guid ItemId, Guid CategoryId);
public sealed record ImageMatchingDetailsDto(IReadOnlyList<PairResultDto> PairResults);
public sealed record CategorizationDetailsDto(IReadOnlyList<AssignmentResultDto> AssignmentResults);
public sealed record PairResultDto(Guid SourceId, Guid SelectedTargetId, Guid CorrectTargetId, bool IsCorrect);
public sealed record AssignmentResultDto(Guid ItemId, Guid SelectedCategoryId, Guid CorrectCategoryId, bool IsCorrect);
