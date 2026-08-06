using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.ExerciseEngine.Models;

public sealed record ExerciseEvaluationResult(EvaluationStatus Status, decimal? Score, string? Feedback,
    string? Explanation, object? CorrectAnswer, object? Details);

public sealed record ItemEvaluationDetail(Guid ItemId, Guid SubmittedValueId, Guid CorrectValueId, bool IsCorrect);
