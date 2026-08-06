using FluentValidation;
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
            .Select(x => new
            {
                x.Id,
                x.LessonId,
                x.Status,
                x.StartedAt,
                x.CompletedAt,
                x.CompletedActivityCount,
                x.TotalActivityCount,
                x.TotalScore,
                x.CorrectCount,
                x.IncorrectCount,
                LessonTitle = x.Lesson.Title,
                LessonDescription = x.Lesson.Description
            }).SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
            return Result<LessonAttemptPlayerResponse>.Failure(ExerciseWorkflowErrors.LessonAttemptNotFound);

        var activities = await dbContext.LessonAttemptExercises.AsNoTracking()
            .Where(x => x.LessonAttemptId == attempt.Id)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new
            {
                x.Id,
                x.ExerciseId,
                x.ActivityType,
                x.DisplayOrder,
                x.ExerciseVersion,
                x.IsRequired,
                x.CompletedAt,
                x.Exercise.Type,
                x.Exercise.Title,
                x.Exercise.Instruction,
                x.Exercise.Difficulty,
                x.Exercise.ContentJson
            }).ToListAsync(cancellationToken);
        var activityIds = activities.Select(x => x.Id).ToArray();
        var currentActivityId = activities.Where(x => x.IsRequired && x.CompletedAt == null)
            .OrderBy(x => x.DisplayOrder).Select(x => (Guid?)x.Id).FirstOrDefault();
        var latestResults = (await dbContext.ExerciseAttempts.AsNoTracking()
            .Where(x => activityIds.Contains(x.LessonAttemptExerciseId) &&
                x.AttemptNumber == dbContext.ExerciseAttempts
                    .Where(candidate => candidate.LessonAttemptExerciseId == x.LessonAttemptExerciseId)
                    .Max(candidate => candidate.AttemptNumber))
            .Select(x => new { x.LessonAttemptExerciseId, x.EvaluationStatus, x.Score, x.Feedback, x.AttemptNumber, x.SubmittedAt })
            .ToListAsync(cancellationToken))
            .GroupBy(x => x.LessonAttemptExerciseId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(a => a.AttemptNumber).First());

        var responseActivities = new List<LearningActivityDto>(activities.Count);
        foreach (var activity in activities)
        {
            var content = contentSerializer.Deserialize(activity.Type, activity.ContentJson);
            if (content.IsFailure)
            {
                logger.LogError("Invalid persisted exercise content for LessonAttemptId {LessonAttemptId}, LessonAttemptExerciseId {LessonAttemptExerciseId}, ExerciseId {ExerciseId}, ExerciseType {ExerciseType}, ExerciseVersion {ExerciseVersion}",
                    attempt.Id, activity.Id, activity.ExerciseId, activity.Type, activity.ExerciseVersion);
                return Result<LessonAttemptPlayerResponse>.Failure(content.Error);
            }
            var definition = definitionValidator.Validate(activity.Type, content.Value);
            if (definition.IsFailure)
            {
                logger.LogError("Invalid persisted exercise definition for LessonAttemptId {LessonAttemptId}, LessonAttemptExerciseId {LessonAttemptExerciseId}, ExerciseId {ExerciseId}, ExerciseType {ExerciseType}, ExerciseVersion {ExerciseVersion}",
                    attempt.Id, activity.Id, activity.ExerciseId, activity.Type, activity.ExerciseVersion);
                return Result<LessonAttemptPlayerResponse>.Failure(definition.Error);
            }
            var publicContent = publicContentMapper.Map(activity.Type, content.Value);
            if (publicContent.IsFailure)
            {
                logger.LogError("Missing or failed public mapper for ExerciseId {ExerciseId}, ExerciseType {ExerciseType}", activity.ExerciseId, activity.Type);
                return Result<LessonAttemptPlayerResponse>.Failure(publicContent.Error);
            }
            latestResults.TryGetValue(activity.Id, out var latest);
            responseActivities.Add(new(activity.Id, activity.ExerciseId, activity.ActivityType, activity.Type,
                activity.Title, activity.Instruction, activity.Difficulty, activity.DisplayOrder, activity.ExerciseVersion,
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
            new(attempt.LessonId, attempt.LessonTitle, attempt.LessonDescription), responseActivities));
    }
}
