using FluentValidation;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace LanguageLearning.WebApi.Features.ExerciseEngine.Queries;

public sealed record GetLessonAttemptPlayerQuery(Guid LessonAttemptId) : IRequest<Result<LessonAttemptPlayerResponse>>;

public sealed class GetLessonAttemptPlayerQueryValidator : AbstractValidator<GetLessonAttemptPlayerQuery>
{
    public GetLessonAttemptPlayerQueryValidator() => RuleFor(x => x.LessonAttemptId).NotEmpty();
}

public sealed class GetLessonAttemptPlayerQueryHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    IExerciseContentSerializer contentSerializer,
    IExerciseDefinitionValidatorResolver definitionValidator,
    IExercisePublicContentMapper publicContentMapper,
    ILogger<GetLessonAttemptPlayerQueryHandler> logger)
    : IRequestHandler<GetLessonAttemptPlayerQuery, Result<LessonAttemptPlayerResponse>>
{
    public async Task<Result<LessonAttemptPlayerResponse>> Handle(GetLessonAttemptPlayerQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        if (currentUser.UserId is not { } userId)
            return Result<LessonAttemptPlayerResponse>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);

        var attempt = await dbContext.LessonAttempts.AsNoTracking()
            .Where(x => x.Id == request.LessonAttemptId && x.UserId == userId)
            .Select(x => new AttemptRow(
                x.Id,
                x.LessonId,
                x.Status,
                x.StartedAt,
                x.CompletedAt,
                x.CompletedActivityCount,
                x.TotalActivityCount,
                x.TotalScore,
                x.CorrectCount,
                x.IncorrectCount))
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
            return Result<LessonAttemptPlayerResponse>.Failure(ExerciseWorkflowErrors.LessonAttemptNotFound);

        var lesson = await dbContext.Lessons.AsNoTracking()
            .Where(value => value.Id == attempt.LessonId)
            .Select(value => new LessonRow(value.Title, value.Description))
            .SingleAsync(cancellationToken);
        var activities = await dbContext.LessonAttemptExercises.AsNoTracking()
            .Where(x => x.LessonAttemptId == attempt.Id)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new ActivityRow(
                x.Id,
                x.ExerciseId,
                x.ActivityType,
                x.DisplayOrder,
                x.ExerciseVersion,
                x.IsRequired,
                x.CompletedAt))
            .ToListAsync(cancellationToken);
        var exerciseIds = activities.Select(activity => activity.ExerciseId).Distinct().ToList();
        var exercises = exerciseIds.Count == 0
            ? []
            : await dbContext.Exercises.AsNoTracking()
                .Where(exercise => exerciseIds.Contains(exercise.Id))
                .Select(exercise => new ExerciseRow(
                    exercise.Id,
                    exercise.Type,
                    exercise.Title,
                    exercise.Instruction,
                    exercise.Difficulty,
                    exercise.ContentJson))
                .ToDictionaryAsync(exercise => exercise.Id, cancellationToken);
        var activityIds = activities.Select(x => x.Id).ToArray();
        var currentActivityId = activities.Where(x => x.IsRequired && x.CompletedAt == null)
            .OrderBy(x => x.DisplayOrder).Select(x => (Guid?)x.Id).FirstOrDefault();
        var submissionRows = activityIds.Length == 0
            ? []
            : await dbContext.ExerciseAttempts.AsNoTracking()
                .Where(x => activityIds.Contains(x.LessonAttemptExerciseId))
                .Select(x => new SubmissionRow(
                    x.LessonAttemptExerciseId,
                    x.EvaluationStatus,
                    x.Score,
                    x.Feedback,
                    x.AttemptNumber,
                    x.SubmittedAt))
                .ToListAsync(cancellationToken);
        var latestResults = submissionRows
            .GroupBy(x => x.LessonAttemptExerciseId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(a => a.AttemptNumber).First());

        var responseActivities = new List<LearningActivityDto>(activities.Count);
        foreach (var activity in activities)
        {
            var exercise = exercises[activity.ExerciseId];
            var content = contentSerializer.Deserialize(exercise.Type, exercise.ContentJson);
            if (content.IsFailure)
            {
                logger.LogError("Invalid persisted exercise content for LessonAttemptId {LessonAttemptId}, LessonAttemptExerciseId {LessonAttemptExerciseId}, ExerciseId {ExerciseId}, ExerciseType {ExerciseType}, ExerciseVersion {ExerciseVersion}",
                    attempt.Id, activity.Id, activity.ExerciseId, exercise.Type, activity.ExerciseVersion);
                return Result<LessonAttemptPlayerResponse>.Failure(content.Error);
            }
            var definition = definitionValidator.Validate(exercise.Type, content.Value);
            if (definition.IsFailure)
            {
                logger.LogError("Invalid persisted exercise definition for LessonAttemptId {LessonAttemptId}, LessonAttemptExerciseId {LessonAttemptExerciseId}, ExerciseId {ExerciseId}, ExerciseType {ExerciseType}, ExerciseVersion {ExerciseVersion}",
                    attempt.Id, activity.Id, activity.ExerciseId, exercise.Type, activity.ExerciseVersion);
                return Result<LessonAttemptPlayerResponse>.Failure(definition.Error);
            }
            var publicContent = publicContentMapper.Map(exercise.Type, content.Value);
            if (publicContent.IsFailure)
            {
                logger.LogError("Missing or failed public mapper for ExerciseId {ExerciseId}, ExerciseType {ExerciseType}", activity.ExerciseId, exercise.Type);
                return Result<LessonAttemptPlayerResponse>.Failure(publicContent.Error);
            }
            latestResults.TryGetValue(activity.Id, out var latest);
            responseActivities.Add(new(activity.Id, activity.ExerciseId, activity.ActivityType, exercise.Type,
                exercise.Title, exercise.Instruction, exercise.Difficulty, activity.DisplayOrder, activity.ExerciseVersion,
                activity.IsRequired, activity.CompletedAt is null ? "NotStarted" : "Completed",
                latest is null ? null : new(latest.EvaluationStatus, latest.Score, latest.Feedback, latest.AttemptNumber, latest.SubmittedAt),
                publicContent.Value));
        }

        logger.LogInformation("Loaded player state for UserId {UserId}, LessonAttemptId {LessonAttemptId}, LessonId {LessonId}, ActivityCount {ActivityCount}",
            userId, attempt.Id, attempt.LessonId, responseActivities.Count);
        logger.LogInformation("Player state loaded for LessonAttemptId {LessonAttemptId} in {DurationMs}ms",
            attempt.Id, stopwatch.ElapsedMilliseconds);
        return Result<LessonAttemptPlayerResponse>.Success(new(
            new(attempt.Id, attempt.LessonId, attempt.Status, attempt.StartedAt, attempt.CompletedAt,
                currentActivityId, attempt.CompletedActivityCount, attempt.TotalActivityCount,
                attempt.TotalScore, attempt.CorrectCount, attempt.IncorrectCount),
            new(attempt.LessonId, lesson.Title, lesson.Description), responseActivities));
    }

    private sealed record AttemptRow(
        Guid Id,
        Guid LessonId,
        LessonAttemptStatus Status,
        DateTime StartedAt,
        DateTime? CompletedAt,
        int CompletedActivityCount,
        int TotalActivityCount,
        decimal TotalScore,
        int CorrectCount,
        int IncorrectCount);

    private sealed record LessonRow(string Title, string? Description);

    private sealed record ActivityRow(
        Guid Id,
        Guid ExerciseId,
        ActivityType ActivityType,
        int DisplayOrder,
        int ExerciseVersion,
        bool IsRequired,
        DateTime? CompletedAt);

    private sealed record ExerciseRow(
        Guid Id,
        ExerciseType Type,
        string Title,
        string Instruction,
        DifficultyLevel Difficulty,
        string ContentJson);

    private sealed record SubmissionRow(
        Guid LessonAttemptExerciseId,
        EvaluationStatus EvaluationStatus,
        decimal? Score,
        string? Feedback,
        int AttemptNumber,
        DateTime SubmittedAt);
}
