using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.LearningCatalog.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LanguageLearning.WebApi.Tests.LearningCatalog;

public sealed class LearningCatalogQueryTests
{
    [Fact]
    public async Task Courses_ReturnOnlyPublishedCoursesWithPublishedLessonCounts()
    {
        await using var db = Db();
        var catalog = await SeedAsync(db);

        var result = await new GetCoursesQuery.Handler(db)
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var course = Assert.Single(result.Value.Items);
        Assert.Equal(catalog.PublishedCourse.Id, course.Id);
        Assert.Equal(2, course.LessonCount);
    }

    [Fact]
    public async Task CourseDetail_ReturnsOnlyUnitsWithPublishedLessonsInCatalogOrder()
    {
        await using var db = Db();
        var catalog = await SeedAsync(db);

        var result = await new GetCourseDetailQuery.Handler(db)
            .Handle(new() { CourseId = catalog.PublishedCourse.Id }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["FIRST", "SECOND"], result.Value.Units.Select(unit => unit.Code));
        Assert.All(result.Value.Units, unit => Assert.Single(unit.Lessons));
        Assert.DoesNotContain(
            result.Value.Units.SelectMany(unit => unit.Lessons),
            lesson => lesson.Id == catalog.DraftLesson.Id);
    }

    [Fact]
    public async Task LessonDetail_RequiresPublishedLessonAndPublishedCourse()
    {
        await using var db = Db();
        var catalog = await SeedAsync(db);
        var handler = new GetLessonDetailQuery.Handler(db);

        var published = await handler.Handle(
            new() { LessonId = catalog.PublishedLesson.Id },
            TestContext.Current.CancellationToken);
        var hiddenCourse = await handler.Handle(
            new() { LessonId = catalog.UnpublishedCourseLesson.Id },
            TestContext.Current.CancellationToken);
        var draft = await handler.Handle(
            new() { LessonId = catalog.DraftLesson.Id },
            TestContext.Current.CancellationToken);

        Assert.True(published.IsSuccess);
        Assert.Equal(catalog.PublishedCourse.Id, published.Value.Course.Id);
        Assert.Equal("FIRST", published.Value.Unit.Code);
        Assert.Equal("lessons.not_found", hiddenCourse.Error);
        Assert.Equal("lessons.not_found", draft.Error);
    }

    private static async Task<Catalog> SeedAsync(ApplicationDbContext db)
    {
        var publishedCourse = Course("PUBLISHED", true);
        var firstUnit = Unit(publishedCourse, "FIRST", 1);
        var secondUnit = Unit(publishedCourse, "SECOND", 2);
        var emptyUnit = Unit(publishedCourse, "EMPTY", 3);
        var publishedLesson = Lesson(firstUnit, "PUBLISHED-ONE", 1, LessonStatus.Published);
        var draftLesson = Lesson(firstUnit, "DRAFT", 2, LessonStatus.Draft);
        var secondPublishedLesson = Lesson(secondUnit, "PUBLISHED-TWO", 1, LessonStatus.Published);

        var unpublishedCourse = Course("UNPUBLISHED", false);
        var hiddenUnit = Unit(unpublishedCourse, "HIDDEN", 1);
        var unpublishedCourseLesson = Lesson(hiddenUnit, "HIDDEN-LESSON", 1, LessonStatus.Published);

        db.AddRange(
            publishedCourse,
            firstUnit,
            secondUnit,
            emptyUnit,
            publishedLesson,
            draftLesson,
            secondPublishedLesson,
            unpublishedCourse,
            hiddenUnit,
            unpublishedCourseLesson);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new(publishedCourse, publishedLesson, draftLesson, unpublishedCourseLesson);
    }

    private static Course Course(string code, bool isPublished) => new()
    {
        Code = code,
        Title = code,
        CefrLevel = CefrLevel.A1,
        DisplayOrder = 1,
        IsPublished = isPublished
    };

    private static Unit Unit(Course course, string code, int displayOrder) => new()
    {
        Course = course,
        Code = code,
        Title = code,
        DisplayOrder = displayOrder
    };

    private static Lesson Lesson(Unit unit, string code, int displayOrder, LessonStatus status) => new()
    {
        Unit = unit,
        Code = code,
        Title = code,
        DisplayOrder = displayOrder,
        Status = status,
        DifficultyLevel = DifficultyLevel.Beginner,
        EstimatedDurationMinutes = 10
    };

    private static ApplicationDbContext Db() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed record Catalog(
        Course PublishedCourse,
        Lesson PublishedLesson,
        Lesson DraftLesson,
        Lesson UnpublishedCourseLesson);
}
