using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LearningCatalog.Queries;

/// <summary>
/// Retrieves an available lesson with its catalog context and ordered sections.
/// </summary>
public sealed class GetLessonDetailQuery : IRequest<Result<GetLessonDetailResponse>>
{
    public Guid LessonId { get; init; }

    public sealed class Handler : IRequestHandler<GetLessonDetailQuery, Result<GetLessonDetailResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public Handler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Result<GetLessonDetailResponse>> Handle(
            GetLessonDetailQuery request,
            CancellationToken cancellationToken)
        {
            var lesson = await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson =>
                    lesson.Id == request.LessonId
                    && lesson.Status == LessonStatus.Published)
                .Select(lesson => new LessonRow(
                    lesson.Id,
                    lesson.UnitId,
                    lesson.Code,
                    lesson.Title,
                    lesson.Description,
                    lesson.LearningObjectiveSummary,
                    lesson.EstimatedDurationMinutes,
                    lesson.DifficultyLevel))
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
                return Result<GetLessonDetailResponse>.Failure("lessons.not_found");

            var unit = await _dbContext.Units
                .AsNoTracking()
                .Where(unit => unit.Id == lesson.UnitId)
                .Select(unit => new UnitRow(unit.Id, unit.CourseId, unit.Code, unit.Title))
                .SingleAsync(cancellationToken);

            var course = await _dbContext.Courses
                .AsNoTracking()
                .Where(course => course.Id == unit.CourseId && course.IsPublished)
                .Select(course => new CourseRow(course.Id, course.Code, course.Title, course.CefrLevel))
                .SingleOrDefaultAsync(cancellationToken);

            if (course is null)
                return Result<GetLessonDetailResponse>.Failure("lessons.not_found");

            return Result<GetLessonDetailResponse>.Success(new GetLessonDetailResponse
            {
                Id = lesson.Id,
                Code = lesson.Code,
                Title = lesson.Title,
                Description = lesson.Description,
                LearningObjectiveSummary = lesson.LearningObjectiveSummary,
                EstimatedDurationMinutes = lesson.EstimatedDurationMinutes,
                DifficultyLevel = lesson.DifficultyLevel,
                Course = new LessonCourseResponse
                {
                    Id = course.Id,
                    Code = course.Code,
                    Title = course.Title,
                    CefrLevel = course.CefrLevel
                },
                Unit = new LessonUnitResponse
                {
                    Id = unit.Id,
                    Code = unit.Code,
                    Title = unit.Title
                }
            });
        }

        private sealed record LessonRow(
            Guid Id,
            Guid UnitId,
            string Code,
            string Title,
            string? Description,
            string? LearningObjectiveSummary,
            int EstimatedDurationMinutes,
            DifficultyLevel DifficultyLevel);

        private sealed record UnitRow(Guid Id, Guid CourseId, string Code, string Title);

        private sealed record CourseRow(Guid Id, string Code, string Title, CefrLevel CefrLevel);
    }
}
