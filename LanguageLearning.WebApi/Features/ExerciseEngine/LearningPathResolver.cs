using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.ExerciseEngine;

public interface ILearningPathResolver
{
    Task<Result<LearningPathResolution>> ResolveAsync(CancellationToken cancellationToken = default);
}

public sealed class SequentialLearningPathResolver(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    ILogger<SequentialLearningPathResolver> logger) : ILearningPathResolver
{
    public async Task<Result<LearningPathResolution>> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
            return Result<LearningPathResolution>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);

        var assignment = await dbContext.UserCourseAssignments.AsNoTracking()
            .Where(x => x.UserId == userId &&
                (x.Status == UserCourseAssignmentStatus.Assigned || x.Status == UserCourseAssignmentStatus.InProgress))
            .Select(x => new { x.Id, x.CourseId })
            .SingleOrDefaultAsync(cancellationToken);
        if (assignment is null)
            return Result<LearningPathResolution>.Success(new(
                LearningPathState.NoActiveAssignment, null, null, null, null, null, null, null, null));

        var activeAttempt = await dbContext.LessonAttempts.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == LessonAttemptStatus.InProgress &&
                x.Lesson.Unit.CourseId == assignment.CourseId)
            .OrderByDescending(x => x.LastAccessedAt ?? x.StartedAt)
            .ThenByDescending(x => x.StartedAt)
            .Select(x => new
            {
                x.Id,
                x.LessonId,
                x.Lesson.Title,
                UnitTitle = x.Lesson.Unit.Title,
                x.Lesson.EstimatedDurationMinutes,
                NextActivityId = x.Activities.Where(activity => activity.IsRequired && activity.CompletedAt == null)
                    .OrderBy(activity => activity.DisplayOrder).Select(activity => (Guid?)activity.Id).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (activeAttempt is not null)
        {
            logger.LogInformation("Resuming learning path for UserId {UserId}, LessonAttemptId {LessonAttemptId}, LessonId {LessonId}",
                userId, activeAttempt.Id, activeAttempt.LessonId);
            return Result<LearningPathResolution>.Success(new(
                LearningPathState.Resume, assignment.Id, assignment.CourseId, activeAttempt.Id,
                activeAttempt.LessonId, activeAttempt.NextActivityId, activeAttempt.Title,
                activeAttempt.UnitTitle, activeAttempt.EstimatedDurationMinutes));
        }

        var publishedLessons = dbContext.Lessons.AsNoTracking()
            .Where(x => x.Unit.CourseId == assignment.CourseId && x.Status == LessonStatus.Published &&
                x.Unit.Course.IsPublished && x.Exercises.Any(e => e.IsActive));

        var nextLesson = await publishedLessons
            .Where(x => !dbContext.LessonAttempts.Any(a => a.UserId == userId && a.LessonId == x.Id && a.Status == LessonAttemptStatus.Completed))
            .OrderBy(x => x.Unit.DisplayOrder)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => new { x.Id, x.Title, UnitTitle = x.Unit.Title, x.EstimatedDurationMinutes })
            .FirstOrDefaultAsync(cancellationToken);

        if (nextLesson is null)
            return Result<LearningPathResolution>.Success(new(
                LearningPathState.CourseCompleted, assignment.Id, assignment.CourseId,
                null, null, null, null, null, null));

        logger.LogInformation("Resolved next lesson for UserId {UserId}, LessonId {LessonId}", userId, nextLesson.Id);
        return Result<LearningPathResolution>.Success(new(
            LearningPathState.StartNextLesson, assignment.Id, assignment.CourseId, null, nextLesson.Id,
            null, nextLesson.Title, nextLesson.UnitTitle, nextLesson.EstimatedDurationMinutes));
    }
}
