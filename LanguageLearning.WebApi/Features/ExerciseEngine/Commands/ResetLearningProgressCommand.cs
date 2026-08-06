using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LanguageLearning.WebApi.Features.ExerciseEngine.Commands;

public sealed record ResetLearningProgressCommand : IRequest<Result<ResetLearningProgressResponse>>;

public sealed record ResetLearningProgressResponse(
    int LessonAttemptsDeleted,
    int ActivitiesDeleted,
    int SubmissionsDeleted,
    int MistakesDeleted);

public sealed class ResetLearningProgressCommandHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    IHostEnvironment environment,
    ILogger<ResetLearningProgressCommandHandler> logger)
    : IRequestHandler<ResetLearningProgressCommand, Result<ResetLearningProgressResponse>>
{
    public const string NotAvailable = "test.learning_progress_reset_not_available";

    public async Task<Result<ResetLearningProgressResponse>> Handle(
        ResetLearningProgressCommand request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return Result<ResetLearningProgressResponse>.Failure(NotAvailable);
        if (currentUser.UserId is not { } userId)
            return Result<ResetLearningProgressResponse>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var attemptIds = await dbContext.LessonAttempts
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var activities = await dbContext.LessonAttemptExercises
            .Where(x => attemptIds.Contains(x.LessonAttemptId))
            .ToListAsync(cancellationToken);
        var activityIds = activities.Select(x => x.Id).ToArray();
        var submissions = await dbContext.ExerciseAttempts
            .Where(x => activityIds.Contains(x.LessonAttemptExerciseId))
            .ToListAsync(cancellationToken);
        var attempts = await dbContext.LessonAttempts
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var mistakes = await dbContext.UserExerciseMistakes
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.ExerciseAttempts.RemoveRange(submissions);
        dbContext.LessonAttemptExercises.RemoveRange(activities);
        dbContext.LessonAttempts.RemoveRange(attempts);
        dbContext.UserExerciseMistakes.RemoveRange(mistakes);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Development learning progress reset for UserId {UserId}: {LessonAttemptCount} attempts, {ActivityCount} activities, {SubmissionCount} submissions, {MistakeCount} mistakes",
            userId, attempts.Count, activities.Count, submissions.Count, mistakes.Count);
        return Result<ResetLearningProgressResponse>.Success(new(
            attempts.Count, activities.Count, submissions.Count, mistakes.Count));
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
}
