using System.Data;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LanguageLearning.WebApi.Features.ExerciseEngine;

public interface ILearningSessionService
{
    Task<Result<LearningSessionResult>> StartOrResumeAsync(CancellationToken cancellationToken = default);
}

public sealed class LearningSessionService(
    ApplicationDbContext dbContext,
    ILearningPathResolver pathResolver,
    ICurrentUserContext currentUser,
    ILogger<LearningSessionService> logger) : ILearningSessionService
{
    private const int ReviewLimit = 3;

    public async Task<Result<LearningSessionResult>> StartOrResumeAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
            return Result<LearningSessionResult>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var resolution = await pathResolver.ResolveAsync(cancellationToken);
        if (resolution.IsFailure)
            return Result<LearningSessionResult>.Failure(resolution.Error);
        if (resolution.Value.State == LearningPathState.NoActiveAssignment)
            return Result<LearningSessionResult>.Failure(ExerciseWorkflowErrors.NoActiveAssignment);
        if (resolution.Value.State == LearningPathState.NoPublishedContent)
            return Result<LearningSessionResult>.Failure(ExerciseWorkflowErrors.NoPublishedContent);
        if (resolution.Value.State == LearningPathState.CourseCompleted)
            return Result<LearningSessionResult>.Failure(ExerciseWorkflowErrors.LearningPathCompleted);

        var now = DateTime.UtcNow;
        var assignment = await dbContext.UserCourseAssignments
            .SingleAsync(x => x.Id == resolution.Value.AssignmentId, cancellationToken);

        if (resolution.Value.State == LearningPathState.Resume)
        {
            var resumed = await dbContext.LessonAttempts
                .SingleAsync(x => x.Id == resolution.Value.LessonAttemptId, cancellationToken);
            resumed.LastAccessedAt = now;
            assignment.Status = UserCourseAssignmentStatus.InProgress;
            assignment.CompletedAt = null;
            assignment.StartedAt ??= now;
            assignment.LastAccessedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            logger.LogInformation("Resumed LessonAttemptId {LessonAttemptId} for UserId {UserId}, LessonId {LessonId}", resumed.Id, userId, resumed.LessonId);
            return Result<LearningSessionResult>.Success(new(resumed.Id, resumed.LessonId, LearningSessionMode.Resumed, resumed.Status));
        }

        var lessonId = resolution.Value.LessonId!.Value;
        var coreExercises = await dbContext.Exercises
            .Where(x => x.LessonId == lessonId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

        var coreIds = coreExercises.Select(x => x.Id).ToArray();
        var reviews = await dbContext.UserExerciseMistakes
            .Include(x => x.Exercise)
            .Where(x => x.UserId == userId && x.Status == UserExerciseMistakeStatus.Pending && x.Exercise.IsActive && !coreIds.Contains(x.ExerciseId))
            .OrderBy(x => x.LastFailedAt)
            .ThenBy(x => x.Id)
            .Take(ReviewLimit)
            .ToListAsync(cancellationToken);

        var attempt = new LessonAttempt
        {
            UserId = userId,
            LessonId = lessonId,
            Status = LessonAttemptStatus.InProgress,
            StartedAt = now,
            LastAccessedAt = now,
            TotalActivityCount = reviews.Count + coreExercises.Count
        };
        var order = 1;
        foreach (var mistake in reviews)
            attempt.Activities.Add(new LessonAttemptExercise
            {
                LessonAttempt = attempt,
                ExerciseId = mistake.ExerciseId,
                ExerciseVersion = mistake.Exercise.Version,
                ActivityType = ActivityType.Review,
                DisplayOrder = order++,
                IsRequired = true,
                SourceLessonId = mistake.Exercise.LessonId,
                UserExerciseMistakeId = mistake.Id
            });
        foreach (var exercise in coreExercises)
            attempt.Activities.Add(new LessonAttemptExercise
            {
                LessonAttempt = attempt,
                ExerciseId = exercise.Id,
                ExerciseVersion = exercise.Version,
                ActivityType = ActivityType.Lesson,
                DisplayOrder = order++,
                IsRequired = exercise.IsRequired,
                SourceLessonId = lessonId
            });

        if (!attempt.Activities.Any(x => x.IsRequired))
        {
            attempt.Status = LessonAttemptStatus.Completed;
            attempt.CompletedAt = now;
        }

        dbContext.LessonAttempts.Add(attempt);
        assignment.Status = UserCourseAssignmentStatus.InProgress;
        assignment.CompletedAt = null;
        assignment.StartedAt ??= now;
        assignment.LastAccessedAt = now;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (attempt.Status == LessonAttemptStatus.Completed)
            {
                var completedPath = await pathResolver.ResolveAsync(cancellationToken);
                if (completedPath.IsSuccess && completedPath.Value.State == LearningPathState.CourseCompleted)
                {
                    assignment.Status = UserCourseAssignmentStatus.Completed;
                    assignment.CompletedAt = now;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            await CommitAsync(transaction, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActiveAttemptConflict(exception))
        {
            logger.LogWarning("Concurrent active lesson attempt conflict for UserId {UserId}, LessonId {LessonId}", userId, lessonId);
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var concurrentAttempt = await dbContext.LessonAttempts.AsNoTracking()
                .Where(x => x.UserId == userId && x.LessonId == lessonId && x.Status == LessonAttemptStatus.InProgress)
                .OrderByDescending(x => x.LastAccessedAt ?? x.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return concurrentAttempt is null
                ? Result<LearningSessionResult>.Failure(ExerciseWorkflowErrors.ActiveLessonAttemptConflict)
                : Result<LearningSessionResult>.Success(new(concurrentAttempt.Id, concurrentAttempt.LessonId,
                    LearningSessionMode.Resumed, concurrentAttempt.Status));
        }

        logger.LogInformation("Started LessonAttemptId {LessonAttemptId} for UserId {UserId}, LessonId {LessonId} with {ReviewCount} review and {CoreCount} core activities",
            attempt.Id, userId, lessonId, reviews.Count, coreExercises.Count);
        return Result<LearningSessionResult>.Success(new(attempt.Id, lessonId, LearningSessionMode.Started, attempt.Status));
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            return null;
        return await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    }

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static bool IsActiveAttemptConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_lesson_attempts_UserId_LessonId_InProgress" };
}
