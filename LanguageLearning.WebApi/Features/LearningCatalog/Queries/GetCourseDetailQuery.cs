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
                .Select(course => new GetCourseDetailResponse
                {
                    Id = course.Id,
                    Code = course.Code,
                    Title = course.Title,
                    Description = course.Description,
                    CefrLevel = course.CefrLevel,
                    Units = course.Units
                        .Where(unit => unit.Lessons.Any(lesson => lesson.Status == LessonStatus.Published))
                        .OrderBy(unit => unit.DisplayOrder)
                        .Select(unit => new CourseUnitResponse
                        {
                            Id = unit.Id,
                            Code = unit.Code,
                            Title = unit.Title,
                            Description = unit.Description,
                            Lessons = unit.Lessons
                                .Where(lesson => lesson.Status == LessonStatus.Published)
                                .OrderBy(lesson => lesson.DisplayOrder)
                                .Select(lesson => new CourseLessonResponse
                                {
                                    Id = lesson.Id,
                                    Code = lesson.Code,
                                    Title = lesson.Title,
                                    Description = lesson.Description,
                                    LearningObjectiveSummary = lesson.LearningObjectiveSummary,
                                    EstimatedDurationMinutes = lesson.EstimatedDurationMinutes,
                                    DifficultyLevel = lesson.DifficultyLevel
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (course is null)
            {
                return Result<GetCourseDetailResponse>.Failure("courses.not_found");
            }

            return Result<GetCourseDetailResponse>.Success(course);
        }
    }
}
