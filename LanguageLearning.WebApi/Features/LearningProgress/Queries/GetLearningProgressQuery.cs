using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.LearningProgress.DTOs;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LearningProgress.Queries;

public sealed record GetLearningProgressQuery : IRequest<Result<LearningProgressResponse>>;

public sealed class GetLearningProgressQueryHandler(
    ApplicationDbContext dbContext, ICurrentUserContext currentUser, ILearningPathResolver resolver)
    : IRequestHandler<GetLearningProgressQuery, Result<LearningProgressResponse>>
{
    public async Task<Result<LearningProgressResponse>> Handle(GetLearningProgressQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result<LearningProgressResponse>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);

        var assignment = await dbContext.UserCourseAssignments.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Status == UserCourseAssignmentStatus.Completed)
            .ThenByDescending(x => x.LastAccessedAt ?? x.AssignedAt)
            .Select(x => new { x.Id, x.CourseId, x.Course.Code, x.Course.Title, x.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (assignment is null)
            return Result<LearningProgressResponse>.Success(new(
                "NoActiveAssignment", null, 0, 0, 0, []));

        var path = await resolver.ResolveAsync(cancellationToken);
        if (path.IsFailure) return Result<LearningProgressResponse>.Failure(path.Error);
        var currentLessonId = path.Value.CourseId == assignment.CourseId &&
            path.Value.State is LearningPathState.Resume or LearningPathState.StartNextLesson
            ? path.Value.LessonId : null;

        var completedLessonIds = await dbContext.LessonAttempts.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == LessonAttemptStatus.Completed && x.Lesson.Unit.CourseId == assignment.CourseId)
            .Select(x => x.LessonId).Distinct().ToArrayAsync(cancellationToken);
        var attempts = await dbContext.LessonAttempts.AsNoTracking()
            .Where(x => x.UserId == userId && x.Lesson.Unit.CourseId == assignment.CourseId)
            .GroupBy(x => x.LessonId)
            .Select(group => new { LessonId = group.Key, AttemptId = group.OrderByDescending(x => x.LastAccessedAt ?? x.StartedAt).Select(x => x.Id).First() })
            .ToDictionaryAsync(x => x.LessonId, x => x.AttemptId, cancellationToken);
        var completed = completedLessonIds.ToHashSet();

        var catalog = await dbContext.Units.AsNoTracking()
            .Where(x => x.CourseId == assignment.CourseId)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new
            {
                x.Id, x.Code, x.Title, x.DisplayOrder,
                Lessons = x.Lessons.Where(lesson => lesson.Status == LessonStatus.Published)
                    .OrderBy(lesson => lesson.DisplayOrder)
                    .Select(lesson => new { lesson.Id, lesson.Code, lesson.Title, lesson.DisplayOrder }).ToList()
            }).ToListAsync(cancellationToken);

        var units = catalog.Select(unit => new UnitProgressDto(unit.Id, unit.Code, unit.Title, unit.DisplayOrder,
            unit.Lessons.Select(lesson => new LessonProgressDto(lesson.Id, lesson.Code, lesson.Title, lesson.DisplayOrder,
                completed.Contains(lesson.Id) ? "Completed" : lesson.Id == currentLessonId ? "Current" : "Upcoming",
                attempts.GetValueOrDefault(lesson.Id))).ToArray())).ToArray();
        var completedCount = completed.Count;
        var totalCount = units.Sum(unit => unit.Lessons.Count);
        var percentage = totalCount == 0 ? 0 : Math.Round((decimal)completedCount / totalCount * 100, 2);
        var state = assignment.Status == UserCourseAssignmentStatus.Completed
            ? "CourseCompleted"
            : "InProgress";
        return Result<LearningProgressResponse>.Success(new(state,
            new(assignment.Id, assignment.CourseId, assignment.Code, assignment.Title, assignment.Status),
            completedCount, totalCount, percentage, units));
    }
}
