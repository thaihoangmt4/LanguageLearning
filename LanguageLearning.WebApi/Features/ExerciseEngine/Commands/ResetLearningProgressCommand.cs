using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Enums;
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
    int MistakesDeleted,
    Guid AssignmentId,
    Guid CourseId,
    bool AssignmentCreated);

public sealed class ResetLearningProgressCommandHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    IHostEnvironment environment,
    IDefaultCourseResolver defaultCourseResolver,
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

        var defaultCourse = await defaultCourseResolver.ResolveAsync(cancellationToken);
        if (defaultCourse.IsFailure)
            return Result<ResetLearningProgressResponse>.Failure(defaultCourse.Error);

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
        var assignments = await dbContext.UserCourseAssignments
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var assignment = assignments.SingleOrDefault(x => x.CourseId == defaultCourse.Value.Id);
        var assignmentCreated = assignment is null;
        var now = DateTime.UtcNow;
        var oldActiveAssignments = assignments
            .Where(x => x != assignment && x.Status != UserCourseAssignmentStatus.Completed)
            .ToArray();
        foreach (var oldActive in oldActiveAssignments)
        {
            oldActive.Status = UserCourseAssignmentStatus.Completed;
            oldActive.CompletedAt = now;
        }
        if (oldActiveAssignments.Length > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        if (assignment is null)
        {
            assignment = new UserCourseAssignment
            {
                UserId = userId,
                CourseId = defaultCourse.Value.Id,
                Status = UserCourseAssignmentStatus.Assigned,
                AssignedAt = now
            };
            dbContext.UserCourseAssignments.Add(assignment);
        }
        else
        {
            assignment.Status = UserCourseAssignmentStatus.Assigned;
            assignment.StartedAt = null;
            assignment.LastAccessedAt = null;
            assignment.CompletedAt = null;
        }

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
            attempts.Count, activities.Count, submissions.Count, mistakes.Count,
            assignment.Id, assignment.CourseId, assignmentCreated));
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
}
