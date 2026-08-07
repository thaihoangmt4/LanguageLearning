using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.ExerciseEngine;

public interface IDefaultCourseResolver
{
    Task<Result<Course>> ResolveAsync(CancellationToken cancellationToken = default);
}

public sealed class DefaultCourseResolver(ApplicationDbContext dbContext, LearningOptions options) : IDefaultCourseResolver
{
    public const string Missing = "learning.default_course_missing";
    public const string Unavailable = "learning.default_course_unavailable";
    public const string Ambiguous = "learning.default_course_ambiguous";

    public async Task<Result<Course>> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultCourseCode))
            return Result<Course>.Failure(Missing);

        var courses = await dbContext.Courses
            .Where(course => course.Code == options.DefaultCourseCode)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (courses.Count == 0)
            return Result<Course>.Failure(Missing);
        if (courses.Count > 1)
            return Result<Course>.Failure(Ambiguous);

        var course = courses[0];
        var hasValidContent = course.IsPublished && await dbContext.Lessons.AsNoTracking()
            .AnyAsync(lesson => lesson.Unit.CourseId == course.Id && lesson.Status == LessonStatus.Published &&
                lesson.Exercises.Any(exercise => exercise.IsActive), cancellationToken);
        return hasValidContent
            ? Result<Course>.Success(course)
            : Result<Course>.Failure(Unavailable);
    }
}
