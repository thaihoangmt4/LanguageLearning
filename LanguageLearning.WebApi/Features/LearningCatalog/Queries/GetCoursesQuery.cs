using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
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
            var items = await _dbContext.Courses
                .AsNoTracking()
                .Where(course => course.IsPublished)
                .OrderBy(course => course.DisplayOrder)
                .Select(course => new CourseListItemResponse
                {
                    Id = course.Id,
                    Code = course.Code,
                    Title = course.Title,
                    Description = course.Description,
                    CefrLevel = course.CefrLevel,
                    LessonCount = course.Units
                        .SelectMany(unit => unit.Lessons)
                        .Count(lesson => lesson.IsPublished)
                })
                .ToListAsync(cancellationToken);

            return Result<GetCoursesResponse>.Success(new GetCoursesResponse
            {
                Items = items
            });
        }
    }
}
