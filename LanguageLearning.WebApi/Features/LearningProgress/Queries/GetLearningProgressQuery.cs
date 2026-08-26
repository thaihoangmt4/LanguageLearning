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
            .Select(x => new AssignmentRow(x.Id, x.CourseId, x.Status))
            .FirstOrDefaultAsync(cancellationToken);
        if (assignment is null)
            return Result<LearningProgressResponse>.Success(new(
                "NoActiveAssignment", null, 0, 0, 0, []));

        var course = await dbContext.Courses.AsNoTracking()
            .Where(value => value.Id == assignment.CourseId)
            .Select(value => new CourseRow(value.Code, value.Title))
            .SingleAsync(cancellationToken);
        var path = await resolver.ResolveAsync(cancellationToken);
        if (path.IsFailure) return Result<LearningProgressResponse>.Failure(path.Error);
        var currentLessonId = path.Value.CourseId == assignment.CourseId &&
            path.Value.State is LearningPathState.Resume or LearningPathState.StartNextLesson
            ? path.Value.LessonId : null;

        var unitRows = await dbContext.Units.AsNoTracking()
            .Where(x => x.CourseId == assignment.CourseId)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new UnitRow(x.Id, x.Code, x.Title, x.DisplayOrder))
            .ToListAsync(cancellationToken);
        var unitIds = unitRows.Select(unit => unit.Id).ToList();
        var lessonRows = unitIds.Count == 0
            ? []
            : await dbContext.Lessons.AsNoTracking()
                .Where(lesson => unitIds.Contains(lesson.UnitId))
                .OrderBy(lesson => lesson.DisplayOrder)
                .Select(lesson => new LessonRow(
                    lesson.Id,
                    lesson.UnitId,
                    lesson.Code,
                    lesson.Title,
                    lesson.DisplayOrder,
                    lesson.Status))
                .ToListAsync(cancellationToken);

        var lessonIds = lessonRows.Select(lesson => lesson.Id).ToList();
        var attemptRows = lessonIds.Count == 0
            ? []
            : await dbContext.LessonAttempts.AsNoTracking()
                .Where(attempt =>
                    attempt.UserId == userId &&
                    lessonIds.Contains(attempt.LessonId))
                .Select(attempt => new AttemptRow(
                    attempt.Id,
                    attempt.LessonId,
                    attempt.Status,
                    attempt.StartedAt,
                    attempt.LastAccessedAt))
                .ToListAsync(cancellationToken);
        var completed = attemptRows
            .Where(attempt => attempt.Status == LessonAttemptStatus.Completed)
            .Select(attempt => attempt.LessonId)
            .ToHashSet();
        var attempts = attemptRows
            .GroupBy(attempt => attempt.LessonId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(attempt => attempt.LastAccessedAt ?? attempt.StartedAt)
                    .Select(attempt => attempt.Id)
                    .First());
        var publishedLessonsByUnitId = lessonRows
            .Where(lesson => lesson.Status == LessonStatus.Published)
            .GroupBy(lesson => lesson.UnitId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var units = unitRows.Select(unit => new UnitProgressDto(unit.Id, unit.Code, unit.Title, unit.DisplayOrder,
            publishedLessonsByUnitId.GetValueOrDefault(unit.Id, []).Select(lesson => new LessonProgressDto(lesson.Id, lesson.Code, lesson.Title, lesson.DisplayOrder,
                completed.Contains(lesson.Id) ? "Completed" : lesson.Id == currentLessonId ? "Current" : "Upcoming",
                attempts.GetValueOrDefault(lesson.Id))).ToArray())).ToArray();
        var completedCount = completed.Count;
        var totalCount = units.Sum(unit => unit.Lessons.Count);
        var percentage = totalCount == 0 ? 0 : Math.Round((decimal)completedCount / totalCount * 100, 2);
        var state = assignment.Status == UserCourseAssignmentStatus.Completed
            ? "CourseCompleted"
            : "InProgress";
        return Result<LearningProgressResponse>.Success(new(state,
            new(assignment.Id, assignment.CourseId, course.Code, course.Title, assignment.Status),
            completedCount, totalCount, percentage, units));
    }

    private sealed record AssignmentRow(
        Guid Id,
        Guid CourseId,
        UserCourseAssignmentStatus Status);

    private sealed record CourseRow(string Code, string Title);

    private sealed record UnitRow(Guid Id, string Code, string Title, int DisplayOrder);

    private sealed record LessonRow(
        Guid Id,
        Guid UnitId,
        string Code,
        string Title,
        int DisplayOrder,
        LessonStatus Status);

    private sealed record AttemptRow(
        Guid Id,
        Guid LessonId,
        LessonAttemptStatus Status,
        DateTime StartedAt,
        DateTime? LastAccessedAt);
}
