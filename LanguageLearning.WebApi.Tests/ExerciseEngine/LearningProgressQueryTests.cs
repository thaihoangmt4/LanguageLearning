using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.LearningProgress.Queries;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class LearningProgressQueryTests
{
    [Fact]
    public async Task NoAssignment_ReturnsSuccessfulBusinessState()
    {
        await using var db = Db();
        var userId = Guid.NewGuid();
        var result = await Handler(db, userId).Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("NoActiveAssignment", result.Value.State);
        Assert.Null(result.Value.Course);
        Assert.Empty(result.Value.Units);
        Assert.Equal(0, result.Value.ProgressPercentage);
    }

    [Fact]
    public async Task ActiveAssignment_ReturnsOrderedRoadmapAndDerivedCounts()
    {
        await using var db = Db();
        var user = new User { Email = "progress@test.local", FullName = "Learner" };
        var course = new Course { Code = "COURSE", Title = "Course", DisplayOrder = 1, IsPublished = true, CefrLevel = CefrLevel.A1 };
        var unit = new Unit { Course = course, Code = "UNIT", Title = "Unit", DisplayOrder = 1 };
        var lesson = new Lesson { Unit = unit, Code = "LESSON", Title = "Lesson", DisplayOrder = 1,
            Status = LessonStatus.Published, DifficultyLevel = DifficultyLevel.Beginner, EstimatedDurationMinutes = 10 };
        var exercise = new Exercise { Lesson = lesson, Type = ExerciseType.Typing, Title = "Exercise", Instruction = "Type",
            Difficulty = DifficultyLevel.Beginner, DisplayOrder = 1, ContentJson = "{}", Version = 1, IsRequired = true, IsActive = true };
        db.AddRange(user, course, unit, lesson, exercise, new UserCourseAssignment
        {
            User = user, Course = course, Status = UserCourseAssignmentStatus.Assigned, AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, user.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("InProgress", result.Value.State);
        Assert.Equal(course.Id, result.Value.Course!.CourseId);
        Assert.Single(result.Value.Units);
        Assert.Single(result.Value.Units[0].Lessons);
        Assert.Equal("Current", result.Value.Units[0].Lessons[0].State);
        Assert.Equal(0, result.Value.CompletedLessonCount);
        Assert.Equal(1, result.Value.TotalLessonCount);
    }

    private static GetLearningProgressQueryHandler Handler(ApplicationDbContext db, Guid userId)
    {
        var current = new FakeCurrentUser(userId);
        var resolver = new SequentialLearningPathResolver(db, current, NullLogger<SequentialLearningPathResolver>.Instance);
        return new(db, current, resolver);
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext { public Guid? UserId => userId; }
}
