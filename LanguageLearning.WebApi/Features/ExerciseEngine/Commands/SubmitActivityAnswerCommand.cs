using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;
using MediatR;

namespace LanguageLearning.WebApi.Features.ExerciseEngine.Commands;

public sealed record SubmitActivityAnswerCommand(Guid LessonAttemptId, Guid ActivityId, Guid SubmissionId,
    int ExerciseVersion, JsonElement Answer) : IRequest<Result<SubmitActivityAnswerResponse>>;

public sealed class SubmitActivityAnswerCommandValidator : AbstractValidator<SubmitActivityAnswerCommand>
{
    public SubmitActivityAnswerCommandValidator()
    {
        RuleFor(x => x.LessonAttemptId).NotEmpty();
        RuleFor(x => x.ActivityId).NotEmpty();
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.ExerciseVersion).GreaterThan(0);
        RuleFor(x => x.Answer).Must(x => x.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
            .WithMessage("An answer payload is required.");
    }
}

public sealed class SubmitActivityAnswerCommandHandler(
    IExerciseSubmissionService submissionService,
    ILogger<SubmitActivityAnswerCommandHandler> logger)
    : IRequestHandler<SubmitActivityAnswerCommand, Result<SubmitActivityAnswerResponse>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<SubmitActivityAnswerResponse>> Handle(SubmitActivityAnswerCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await submissionService.SubmitAsync(new(request.LessonAttemptId, request.ActivityId,
            request.ExerciseVersion, request.SubmissionId, request.Answer.GetRawText()), cancellationToken);
        if (result.IsFailure)
            return Result<SubmitActivityAnswerResponse>.Failure(result.Error);

        var value = result.Value;
        var evaluation = new SubmissionEvaluationDto(value.Evaluation.Status, value.Evaluation.Score,
            value.Evaluation.Feedback, value.Evaluation.Explanation,
            MapCorrectAnswer(value.ExerciseType, value.Evaluation.CorrectAnswer),
            MapDetails(value.ExerciseType, value.Evaluation.Details));
        var response = new SubmitActivityAnswerResponse(value.SubmissionId, value.LessonAttemptId, value.ActivityId,
            value.ExerciseId, value.ExerciseVersion, value.AttemptNumber, evaluation,
            new(value.CompletedActivityCount, value.TotalActivityCount,
                value.LessonAttemptStatus == LessonAttemptStatus.Completed, value.NextActivityId), value.SubmittedAt, value.IsReplay);
        logger.LogInformation("Submission response for LessonAttemptId {LessonAttemptId}, ActivityId {ActivityId}, ExerciseId {ExerciseId}, ExerciseType {ExerciseType}, ExerciseVersion {ExerciseVersion}, SubmissionId {SubmissionId}, AttemptNumber {AttemptNumber}, EvaluationStatus {EvaluationStatus}, IsIdempotentReplay {IsIdempotentReplay}, DurationMs {DurationMs}",
            value.LessonAttemptId, value.ActivityId, value.ExerciseId, value.ExerciseType, value.ExerciseVersion,
            value.SubmissionId, value.AttemptNumber, value.Evaluation.Status, value.IsReplay, stopwatch.ElapsedMilliseconds);
        return Result<SubmitActivityAnswerResponse>.Success(response);
    }

    private static object? MapCorrectAnswer(ExerciseType type, object? value)
    {
        if (value is null || type == ExerciseType.Speaking) return null;
        return type switch
        {
            ExerciseType.MultipleChoice or ExerciseType.AudioMatching => new CorrectOptionDto(Read<Guid>(value)),
            ExerciseType.Typing => new ExpectedTextDto(Read<string>(value)),
            ExerciseType.SentenceOrdering => new CorrectOrderDto(Read<IReadOnlyList<Guid>>(value)),
            ExerciseType.ImageMatching => new CorrectMatchesDto(Read<IReadOnlyList<MatchPair>>(value).Select(x => new MatchDto(x.SourceId, x.TargetId)).ToArray()),
            ExerciseType.Categorization => new CorrectAssignmentsDto(Read<IReadOnlyList<CategoryAssignment>>(value).Select(x => new AssignmentDto(x.ItemId, x.CategoryId)).ToArray()),
            _ => null
        };
    }

    private static object? MapDetails(ExerciseType type, object? value)
    {
        if (value is null) return null;
        var details = Read<IReadOnlyList<ItemEvaluationDetail>>(value);
        return type switch
        {
            ExerciseType.ImageMatching => new ImageMatchingDetailsDto(details.Select(x =>
                new PairResultDto(x.ItemId, x.SubmittedValueId, x.CorrectValueId, x.IsCorrect)).ToArray()),
            ExerciseType.Categorization => new CategorizationDetailsDto(details.Select(x =>
                new AssignmentResultDto(x.ItemId, x.SubmittedValueId, x.CorrectValueId, x.IsCorrect)).ToArray()),
            _ => null
        };
    }

    private static T Read<T>(object value)
    {
        if (value is T typed) return typed;
        if (value is JsonElement element) return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions)!;
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    }
}
