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
            .Where(value => value.UserId == userId)
            .OrderBy(value => value.Status == UserCourseAssignmentStatus.Completed)
            .ThenByDescending(value => value.LastAccessedAt ?? value.AssignedAt)
            .Select(value => new { value.Id, value.CourseId })
            .FirstOrDefaultAsync(cancellationToken);
        if (assignment is null)
            return Resolution(LearningPathState.NoActiveAssignment);

        var courseIsPublished = await dbContext.Courses.AsNoTracking()
            .Where(value => value.Id == assignment.CourseId)
            .Select(value => value.IsPublished)
            .SingleAsync(cancellationToken);
        if (!courseIsPublished)
            return Resolution(LearningPathState.NoPublishedContent, assignment.Id, assignment.CourseId);

        var units = await dbContext.Units.AsNoTracking()
            .Where(value => value.CourseId == assignment.CourseId)
            .OrderBy(value => value.DisplayOrder)
            .Select(value => new { value.Id, value.Title, value.DisplayOrder })
            .ToListAsync(cancellationToken);
        var unitIds = units.Select(value => value.Id).ToArray();
        var unitById = units.ToDictionary(value => value.Id);
        var lessons = unitIds.Length == 0
            ? []
            : await dbContext.Lessons.AsNoTracking()
                .Where(value => unitIds.Contains(value.UnitId) && value.Status == LessonStatus.Published)
                .Select(value => new LessonRow(
                    value.Id,
                    value.UnitId,
                    value.Title,
                    value.DisplayOrder,
                    value.EstimatedDurationMinutes))
                .ToListAsync(cancellationToken);
        var orderedLessons = lessons
            .OrderBy(value => unitById[value.UnitId].DisplayOrder)
            .ThenBy(value => value.DisplayOrder)
            .ToArray();
        if (orderedLessons.Length == 0)
            return Resolution(LearningPathState.NoPublishedContent, assignment.Id, assignment.CourseId);

        var lessonIds = orderedLessons.Select(value => value.Id).ToArray();
        var requiredExercises = await dbContext.Exercises.AsNoTracking()
            .Where(value => lessonIds.Contains(value.LessonId) && value.IsActive && value.IsRequired)
            .Select(value => new ExerciseRow(value.Id, value.LessonId))
            .ToListAsync(cancellationToken);
        var requiredByLessonId = requiredExercises
            .GroupBy(value => value.LessonId)
            .ToDictionary(group => group.Key, group => group.Select(value => value.Id).ToArray());

        var attempts = await dbContext.LessonAttempts.AsNoTracking()
            .Where(value => value.UserId == userId && lessonIds.Contains(value.LessonId))
            .Select(value => new AttemptRow(
                value.Id,
                value.LessonId,
                value.Status,
                value.StartedAt,
                value.LastAccessedAt))
            .ToListAsync(cancellationToken);
        var attemptIds = attempts.Select(value => value.Id).ToArray();
        var requiredExerciseIds = requiredExercises.Select(value => value.Id).ToArray();
        var completedExerciseIds = attemptIds.Length == 0 || requiredExerciseIds.Length == 0
            ? []
            : await dbContext.LessonAttemptExercises.AsNoTracking()
                .Where(value =>
                    attemptIds.Contains(value.LessonAttemptId) &&
                    requiredExerciseIds.Contains(value.ExerciseId) &&
                    value.CompletedAt != null)
                .Select(value => value.ExerciseId)
                .Distinct()
                .ToListAsync(cancellationToken);
        var completedExerciseIdSet = completedExerciseIds.ToHashSet();

        var nextLesson = orderedLessons.FirstOrDefault(lesson =>
            !requiredByLessonId.TryGetValue(lesson.Id, out var requiredIds) ||
            requiredIds.Any(id => !completedExerciseIdSet.Contains(id)));
        if (nextLesson is null)
            return Resolution(LearningPathState.CourseCompleted, assignment.Id, assignment.CourseId);

        if (!requiredByLessonId.ContainsKey(nextLesson.Id))
            return Resolution(LearningPathState.NoPublishedContent, assignment.Id, assignment.CourseId);

        var activeAttempt = attempts
            .Where(value => value.LessonId == nextLesson.Id && value.Status == LessonAttemptStatus.InProgress)
            .OrderByDescending(value => value.LastAccessedAt ?? value.StartedAt)
            .ThenByDescending(value => value.StartedAt)
            .FirstOrDefault();
        var unitTitle = unitById[nextLesson.UnitId].Title;
        if (activeAttempt is not null)
        {
            var nextActivityId = await dbContext.LessonAttemptExercises.AsNoTracking()
                .Where(value =>
                    value.LessonAttemptId == activeAttempt.Id &&
                    value.IsRequired &&
                    value.CompletedAt == null)
                .OrderBy(value => value.DisplayOrder)
                .Select(value => (Guid?)value.Id)
                .FirstOrDefaultAsync(cancellationToken);
            logger.LogInformation(
                "Resuming learning path for UserId {UserId}, LessonAttemptId {LessonAttemptId}, LessonId {LessonId}",
                userId,
                activeAttempt.Id,
                activeAttempt.LessonId);
            return Result<LearningPathResolution>.Success(new(
                LearningPathState.Resume,
                assignment.Id,
                assignment.CourseId,
                activeAttempt.Id,
                nextLesson.Id,
                nextActivityId,
                nextLesson.Title,
                unitTitle,
                nextLesson.EstimatedDurationMinutes));
        }

        logger.LogInformation("Resolved next lesson for UserId {UserId}, LessonId {LessonId}", userId, nextLesson.Id);
        return Result<LearningPathResolution>.Success(new(
            LearningPathState.StartNextLesson,
            assignment.Id,
            assignment.CourseId,
            null,
            nextLesson.Id,
            null,
            nextLesson.Title,
            unitTitle,
            nextLesson.EstimatedDurationMinutes));
    }

    private static Result<LearningPathResolution> Resolution(
        LearningPathState state,
        Guid? assignmentId = null,
        Guid? courseId = null) =>
        Result<LearningPathResolution>.Success(new(
            state, assignmentId, courseId, null, null, null, null, null, null));

    private sealed record LessonRow(
        Guid Id,
        Guid UnitId,
        string Title,
        int DisplayOrder,
        int EstimatedDurationMinutes);

    private sealed record ExerciseRow(Guid Id, Guid LessonId);

    private sealed record AttemptRow(
        Guid Id,
        Guid LessonId,
        LessonAttemptStatus Status,
        DateTime StartedAt,
        DateTime? LastAccessedAt);
}
