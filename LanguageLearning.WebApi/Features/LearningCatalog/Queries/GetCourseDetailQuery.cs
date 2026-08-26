using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LearningCatalog.Queries;

/// <summary>
/// Retrieves a published course and its learner-visible curriculum structure.
/// </summary>
public sealed class GetCourseDetailQuery : IRequest<Result<GetCourseDetailResponse>>
{
    public Guid CourseId { get; init; }

    public sealed class Handler : IRequestHandler<GetCourseDetailQuery, Result<GetCourseDetailResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public Handler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Result<GetCourseDetailResponse>> Handle(
            GetCourseDetailQuery request,
            CancellationToken cancellationToken)
        {
            var course = await _dbContext.Courses
                .AsNoTracking()
                .Where(course => course.Id == request.CourseId && course.IsPublished)
                .Select(course => new CourseRow(
                    course.Id,
                    course.Code,
                    course.Title,
                    course.Description,
                    course.CefrLevel))
                .FirstOrDefaultAsync(cancellationToken);

            if (course is null)
            {
                return Result<GetCourseDetailResponse>.Failure("courses.not_found");
            }

            var unitRows = await _dbContext.Units
                .AsNoTracking()
                .Where(unit => unit.CourseId == course.Id)
                .OrderBy(unit => unit.DisplayOrder)
                .Select(unit => new UnitRow(
                    unit.Id,
                    unit.Code,
                    unit.Title,
                    unit.Description))
                .ToListAsync(cancellationToken);

            var unitIds = unitRows.Select(unit => unit.Id).ToList();
            var lessonRows = unitIds.Count == 0
                ? []
                : await _dbContext.Lessons
                    .AsNoTracking()
                    .Where(lesson =>
                        unitIds.Contains(lesson.UnitId) &&
                        lesson.Status == LessonStatus.Published)
                    .OrderBy(lesson => lesson.DisplayOrder)
                    .Select(lesson => new LessonRow(
                        lesson.Id,
                        lesson.UnitId,
                        lesson.Code,
                        lesson.Title,
                        lesson.Description,
                        lesson.LearningObjectiveSummary,
                        lesson.EstimatedDurationMinutes,
                        lesson.DifficultyLevel))
                    .ToListAsync(cancellationToken);

            var lessonsByUnitId = lessonRows
                .GroupBy(lesson => lesson.UnitId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var units = unitRows
                .Where(unit => lessonsByUnitId.ContainsKey(unit.Id))
                .Select(unit => new CourseUnitResponse
                {
                    Id = unit.Id,
                    Code = unit.Code,
                    Title = unit.Title,
                    Description = unit.Description,
                    Lessons = lessonsByUnitId[unit.Id].Select(lesson => new CourseLessonResponse
                    {
                        Id = lesson.Id,
                        Code = lesson.Code,
                        Title = lesson.Title,
                        Description = lesson.Description,
                        LearningObjectiveSummary = lesson.LearningObjectiveSummary,
                        EstimatedDurationMinutes = lesson.EstimatedDurationMinutes,
                        DifficultyLevel = lesson.DifficultyLevel
                    }).ToList()
                }).ToList();

            return Result<GetCourseDetailResponse>.Success(new GetCourseDetailResponse
            {
                Id = course.Id,
                Code = course.Code,
                Title = course.Title,
                Description = course.Description,
                CefrLevel = course.CefrLevel,
                Units = units
            });
        }

        private sealed record CourseRow(
            Guid Id,
            string Code,
            string Title,
            string? Description,
            CefrLevel CefrLevel);

        private sealed record UnitRow(Guid Id, string Code, string Title, string? Description);

        private sealed record LessonRow(
            Guid Id,
            Guid UnitId,
            string Code,
            string Title,
            string? Description,
            string? LearningObjectiveSummary,
            int EstimatedDurationMinutes,
            DifficultyLevel DifficultyLevel);
    }
}
