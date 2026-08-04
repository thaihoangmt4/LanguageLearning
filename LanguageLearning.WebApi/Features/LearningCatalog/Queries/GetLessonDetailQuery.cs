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
                    && lesson.Status == LessonStatus.Published
                    && lesson.Unit.Course.IsPublished)
                .Select(lesson => new GetLessonDetailResponse
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
                        Id = lesson.Unit.Course.Id,
                        Code = lesson.Unit.Course.Code,
                        Title = lesson.Unit.Course.Title,
                        CefrLevel = lesson.Unit.Course.CefrLevel
                    },
                    Unit = new LessonUnitResponse
                    {
                        Id = lesson.Unit.Id,
                        Code = lesson.Unit.Code,
                        Title = lesson.Unit.Title
                    },
                    Sections = lesson.LessonSections
                        .OrderBy(section => section.DisplayOrder)
                        .Select(section => new LessonSectionResponse
                        {
                            Id = section.Id,
                            SectionType = section.SectionType,
                            Title = section.Title,
                            IsRequired = section.IsRequired
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
            {
                return Result<GetLessonDetailResponse>.Failure("lessons.not_found");
            }

            return Result<GetLessonDetailResponse>.Success(lesson);
        }
    }
}
