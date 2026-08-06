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

        var activeAttempt = await dbContext.LessonAttempts.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == LessonAttemptStatus.InProgress)
            .OrderBy(x => x.StartedAt)
            .Select(x => new { x.Id, x.LessonId })
            .FirstOrDefaultAsync(cancellationToken);
        if (activeAttempt is not null)
        {
            logger.LogInformation("Resuming learning path for UserId {UserId}, LessonAttemptId {LessonAttemptId}, LessonId {LessonId}",
                userId, activeAttempt.Id, activeAttempt.LessonId);
            return Result<LearningPathResolution>.Success(new(activeAttempt.Id, activeAttempt.LessonId, true));
        }

        var publishedLessons = dbContext.Lessons.AsNoTracking()
            .Where(x => x.Status == LessonStatus.Published && x.Unit.Course.IsPublished && x.Exercises.Any(e => e.IsActive));

        if (!await publishedLessons.AnyAsync(cancellationToken))
            return Result<LearningPathResolution>.Failure(ExerciseWorkflowErrors.NoPublishedContent);

        var nextLesson = await publishedLessons
            .Where(x => !dbContext.LessonAttempts.Any(a => a.UserId == userId && a.LessonId == x.Id && a.Status == LessonAttemptStatus.Completed))
            .OrderBy(x => x.Unit.Course.DisplayOrder)
            .ThenBy(x => x.Unit.DisplayOrder)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => new { x.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (nextLesson is null)
            return Result<LearningPathResolution>.Failure(ExerciseWorkflowErrors.LearningPathCompleted);

        logger.LogInformation("Resolved next lesson for UserId {UserId}, LessonId {LessonId}", userId, nextLesson.Id);
        return Result<LearningPathResolution>.Success(new(null, nextLesson.Id, false));
    }
}
