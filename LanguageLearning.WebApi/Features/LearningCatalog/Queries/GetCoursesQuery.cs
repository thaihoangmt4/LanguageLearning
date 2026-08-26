using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LearningCatalog.Queries;

/// <summary>
/// Retrieves the published learning catalog courses.
/// </summary>
public sealed class GetCoursesQuery : IRequest<Result<GetCoursesResponse>>
{
    public sealed class Handler : IRequestHandler<GetCoursesQuery, Result<GetCoursesResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public Handler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Result<GetCoursesResponse>> Handle(
            GetCoursesQuery request,
            CancellationToken cancellationToken)
        {
            var courses = await _dbContext.Courses
                .AsNoTracking()
                .Where(course => course.IsPublished)
                .OrderBy(course => course.DisplayOrder)
                .Select(course => new CourseRow(
                    course.Id,
                    course.Code,
                    course.Title,
                    course.Description,
                    course.CefrLevel))
                .ToListAsync(cancellationToken);

            var courseIds = courses.Select(course => course.Id).ToList();
            var units = courseIds.Count == 0
                ? []
                : await _dbContext.Units
                    .AsNoTracking()
                    .Where(unit => courseIds.Contains(unit.CourseId))
                    .Select(unit => new UnitRow(unit.Id, unit.CourseId))
                    .ToListAsync(cancellationToken);

            var unitIds = units.Select(unit => unit.Id).ToList();
            var publishedLessonUnitIds = unitIds.Count == 0
                ? []
                : await _dbContext.Lessons
                    .AsNoTracking()
                    .Where(lesson =>
                        unitIds.Contains(lesson.UnitId) &&
                        lesson.Status == LessonStatus.Published)
                    .Select(lesson => lesson.UnitId)
                    .ToListAsync(cancellationToken);

            var courseIdByUnitId = units.ToDictionary(unit => unit.Id, unit => unit.CourseId);
            var lessonCountByCourseId = publishedLessonUnitIds
                .GroupBy(unitId => courseIdByUnitId[unitId])
                .ToDictionary(group => group.Key, group => group.Count());
            var items = courses.Select(course => new CourseListItemResponse
            {
                Id = course.Id,
                Code = course.Code,
                Title = course.Title,
                Description = course.Description,
                CefrLevel = course.CefrLevel,
                LessonCount = lessonCountByCourseId.GetValueOrDefault(course.Id)
            }).ToList();

            return Result<GetCoursesResponse>.Success(new GetCoursesResponse
            {
                Items = items
            });
        }

        private sealed record CourseRow(
            Guid Id,
            string Code,
            string Title,
            string? Description,
            CefrLevel CefrLevel);

        private sealed record UnitRow(Guid Id, Guid CourseId);
    }
}
