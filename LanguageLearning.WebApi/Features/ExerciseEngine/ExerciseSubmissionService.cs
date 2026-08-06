using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LanguageLearning.WebApi.Features.ExerciseEngine;

public interface IExerciseSubmissionService
{
    Task<Result<ExerciseSubmissionResult>> SubmitAsync(ExerciseSubmission submission, CancellationToken cancellationToken = default);
}

public sealed class ExerciseSubmissionService(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    IExerciseContentSerializer contentSerializer,
    IExerciseAnswerSerializer answerSerializer,
    IExerciseDefinitionValidatorResolver definitionValidator,
    IExerciseAnswerValidatorResolver answerValidator,
    IExerciseEvaluatorResolver evaluatorResolver,
    ILogger<ExerciseSubmissionService> logger) : IExerciseSubmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<ExerciseSubmissionResult>> SubmitAsync(ExerciseSubmission submission, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var attempt = await dbContext.LessonAttempts.SingleOrDefaultAsync(x => x.Id == submission.LessonAttemptId, cancellationToken);
        if (attempt is null)
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.LessonAttemptNotFound);
        if (attempt.UserId != userId)
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.LessonAttemptForbidden);

        var existing = await dbContext.ExerciseAttempts.AsNoTracking()
            .Include(x => x.LessonAttemptExercise).ThenInclude(x => x.Exercise)
            .SingleOrDefaultAsync(x => x.LessonAttemptExercise.LessonAttemptId == attempt.Id && x.SubmissionId == submission.SubmissionId, cancellationToken);
        if (existing is not null)
        {
            if (existing.LessonAttemptExerciseId != submission.LessonAttemptExerciseId ||
                existing.ExerciseVersion != submission.ExerciseVersion || !JsonEquivalent(existing.AnswerJson, submission.AnswerJson))
            {
                logger.LogWarning("Submission payload conflict for UserId {UserId}, LessonAttemptId {LessonAttemptId}, LessonAttemptExerciseId {LessonAttemptExerciseId}, SubmissionId {SubmissionId}",
                    userId, attempt.Id, submission.LessonAttemptExerciseId, submission.SubmissionId);
                return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.SubmissionPayloadMismatch);
            }
            var stored = JsonSerializer.Deserialize<ExerciseEvaluationResult>(existing.ResultJson!, JsonOptions)!;
            logger.LogInformation("Replayed SubmissionId {SubmissionId} for UserId {UserId}, LessonAttemptId {LessonAttemptId}, ExerciseId {ExerciseId}, AttemptNumber {AttemptNumber}",
                submission.SubmissionId, userId, attempt.Id, existing.LessonAttemptExercise.ExerciseId, existing.AttemptNumber);
            var replayNextActivityId = await NextRequiredActivityIdAsync(attempt.Id, cancellationToken);
            return Result<ExerciseSubmissionResult>.Success(new(existing.Id, existing.SubmissionId, attempt.Id,
                existing.LessonAttemptExerciseId, existing.LessonAttemptExercise.ExerciseId, existing.LessonAttemptExercise.Exercise.Type, existing.ExerciseVersion,
                existing.AttemptNumber, true, stored, attempt.CompletedActivityCount, attempt.TotalActivityCount,
                replayNextActivityId, attempt.Status, existing.SubmittedAt));
        }
        if (attempt.Status != LessonAttemptStatus.InProgress)
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.LessonAttemptCompleted);

        var activity = await dbContext.LessonAttemptExercises
            .Include(x => x.Exercise)
            .SingleOrDefaultAsync(x => x.Id == submission.LessonAttemptExerciseId, cancellationToken);
        if (activity is null)
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.LessonAttemptExerciseNotFound);
        if (activity.LessonAttemptId != attempt.Id)
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.ExerciseNotPartOfAttempt);
        if (!activity.Exercise.IsActive)
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.ExerciseInactive);
        if (submission.ExerciseVersion != activity.ExerciseVersion || submission.ExerciseVersion != activity.Exercise.Version)
        {
            logger.LogWarning("Exercise version conflict for UserId {UserId}, LessonAttemptId {LessonAttemptId}, LessonAttemptExerciseId {LessonAttemptExerciseId}, ExerciseId {ExerciseId}, submitted {SubmittedVersion}, activity {ActivityVersion}, current {CurrentVersion}",
                userId, attempt.Id, activity.Id, activity.ExerciseId, submission.ExerciseVersion, activity.ExerciseVersion, activity.Exercise.Version);
            return Result<ExerciseSubmissionResult>.Failure(ExerciseWorkflowErrors.ExerciseVersionMismatch);
        }

        var content = contentSerializer.Deserialize(activity.Exercise.Type, activity.Exercise.ContentJson);
        if (content.IsFailure)
            return Result<ExerciseSubmissionResult>.Failure(content.Error);
        var validDefinition = definitionValidator.Validate(activity.Exercise.Type, content.Value);
        if (validDefinition.IsFailure)
            return Result<ExerciseSubmissionResult>.Failure(validDefinition.Error);
        var answer = answerSerializer.Deserialize(activity.Exercise.Type, submission.AnswerJson);
        if (answer.IsFailure)
            return Result<ExerciseSubmissionResult>.Failure(answer.Error);
        var validAnswer = answerValidator.Validate(activity.Exercise.Type, content.Value, answer.Value);
        if (validAnswer.IsFailure)
            return Result<ExerciseSubmissionResult>.Failure(validAnswer.Error);
        var evaluated = evaluatorResolver.Evaluate(activity.Exercise.Type, content.Value, answer.Value);
        if (evaluated.IsFailure)
            return Result<ExerciseSubmissionResult>.Failure(evaluated.Error);

        var now = DateTime.UtcNow;
        var attemptNumber = await dbContext.ExerciseAttempts
            .Where(x => x.LessonAttemptExerciseId == activity.Id)
            .Select(x => (int?)x.AttemptNumber).MaxAsync(cancellationToken) is { } current ? current + 1 : 1;
        var exerciseAttempt = new ExerciseAttempt
        {
            SubmissionId = submission.SubmissionId,
            LessonAttemptExerciseId = activity.Id,
            ExerciseVersion = activity.ExerciseVersion,
            AttemptNumber = attemptNumber,
            AnswerJson = submission.AnswerJson,
            EvaluationStatus = evaluated.Value.Status,
            Score = evaluated.Value.Score,
            Feedback = evaluated.Value.Feedback,
            ResultJson = JsonSerializer.Serialize(evaluated.Value, JsonOptions),
            SubmittedAt = now
        };
        dbContext.ExerciseAttempts.Add(exerciseAttempt);
        var firstCompletion = activity.CompletedAt is null;
        activity.CompletedAt ??= now;
        await UpdateMistakeAsync(userId, activity, evaluated.Value.Status, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateProgressAsync(attempt, now, cancellationToken);
        var nextActivityId = await NextRequiredActivityIdAsync(attempt.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);

        logger.LogInformation("Processed SubmissionId {SubmissionId} for UserId {UserId}, LessonAttemptId {LessonAttemptId}, LessonId {LessonId}, LessonAttemptExerciseId {LessonAttemptExerciseId}, ExerciseId {ExerciseId}, ExerciseType {ExerciseType}, ActivityType {ActivityType}, ExerciseVersion {ExerciseVersion}, EvaluationStatus {EvaluationStatus}, AttemptNumber {AttemptNumber}, FirstCompletion {FirstCompletion}",
            submission.SubmissionId, userId, attempt.Id, attempt.LessonId, activity.Id, activity.ExerciseId, activity.Exercise.Type,
            activity.ActivityType, activity.ExerciseVersion, evaluated.Value.Status, attemptNumber, firstCompletion);
        return Result<ExerciseSubmissionResult>.Success(new(exerciseAttempt.Id, submission.SubmissionId, attempt.Id,
            activity.Id, activity.ExerciseId, activity.Exercise.Type, activity.ExerciseVersion, attemptNumber, false,
            evaluated.Value, attempt.CompletedActivityCount, attempt.TotalActivityCount, nextActivityId,
            attempt.Status, now));
    }

    private async Task UpdateMistakeAsync(Guid userId, LessonAttemptExercise activity, EvaluationStatus status, DateTime now, CancellationToken cancellationToken)
    {
        if (status == EvaluationStatus.NotEvaluated)
            return;
        var mistake = await dbContext.UserExerciseMistakes
            .SingleOrDefaultAsync(x => x.UserId == userId && x.ExerciseId == activity.ExerciseId, cancellationToken);
        if (status is EvaluationStatus.Incorrect or EvaluationStatus.PartiallyCorrect)
        {
            if (mistake is null)
            {
                mistake = new UserExerciseMistake
                {
                    UserId = userId,
                    ExerciseId = activity.ExerciseId,
                    ExerciseVersion = activity.ExerciseVersion,
                    Status = UserExerciseMistakeStatus.Pending,
                    FirstFailedAt = now,
                    LastFailedAt = now,
                    FailureCount = 1
                };
                dbContext.UserExerciseMistakes.Add(mistake);
                logger.LogInformation("Created pending mistake for UserId {UserId}, ExerciseId {ExerciseId}, LessonAttemptExerciseId {LessonAttemptExerciseId}", userId, activity.ExerciseId, activity.Id);
            }
            else
            {
                mistake.ExerciseVersion = activity.ExerciseVersion;
                mistake.Status = UserExerciseMistakeStatus.Pending;
                mistake.LastFailedAt = now;
                mistake.FailureCount++;
                mistake.ResolvedAt = null;
            }
        }
        else if (status == EvaluationStatus.Correct && activity.ActivityType == ActivityType.Review && mistake is not null)
        {
            mistake.Status = UserExerciseMistakeStatus.Resolved;
            mistake.SuccessfulReviewCount++;
            mistake.ResolvedAt = now;
            logger.LogInformation("Resolved mistake for UserId {UserId}, ExerciseId {ExerciseId}, LessonAttemptExerciseId {LessonAttemptExerciseId}", userId, activity.ExerciseId, activity.Id);
        }
    }

    private async Task RecalculateProgressAsync(LessonAttempt attempt, DateTime now, CancellationToken cancellationToken)
    {
        var activities = await dbContext.LessonAttemptExercises.Where(x => x.LessonAttemptId == attempt.Id)
            .OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
        var submissions = await dbContext.ExerciseAttempts.AsNoTracking()
            .Where(x => x.LessonAttemptExercise.LessonAttemptId == attempt.Id).ToListAsync(cancellationToken);
        var best = submissions.GroupBy(x => x.LessonAttemptExerciseId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(a => a.Score ?? -1).ThenByDescending(a => a.SubmittedAt).First());
        attempt.CompletedActivityCount = activities.Count(x => x.CompletedAt is not null);
        attempt.TotalActivityCount = activities.Count;
        attempt.CorrectCount = best.Values.Count(x => x.EvaluationStatus == EvaluationStatus.Correct);
        attempt.IncorrectCount = best.Values.Count(x => x.EvaluationStatus is EvaluationStatus.Incorrect or EvaluationStatus.PartiallyCorrect);
        var scored = best.Values.Where(x => x.Score.HasValue).Select(x => x.Score!.Value).ToArray();
        attempt.TotalScore = scored.Length == 0 ? 0 : Math.Round(scored.Average(), 2);
        var next = activities.FirstOrDefault(x => x.IsRequired && x.CompletedAt is null);
        if (next is null)
        {
            attempt.Status = LessonAttemptStatus.Completed;
            attempt.CompletedAt = now;
            logger.LogInformation("Completed LessonAttemptId {LessonAttemptId}, LessonId {LessonId}, UserId {UserId}", attempt.Id, attempt.LessonId, attempt.UserId);
        }
    }

    private Task<Guid?> NextRequiredActivityIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        dbContext.LessonAttemptExercises.AsNoTracking()
            .Where(x => x.LessonAttemptId == attemptId && x.IsRequired && x.CompletedAt == null)
            .OrderBy(x => x.DisplayOrder).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);

    private static bool JsonEquivalent(string left, string right)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right)); }
        catch (JsonException) { return false; }
    }
    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            return null;
        return await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    }
    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);
}
