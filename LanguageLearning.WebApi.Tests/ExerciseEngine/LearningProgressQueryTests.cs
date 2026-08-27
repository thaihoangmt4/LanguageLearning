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

    [Fact]
    public async Task AllCurrentRequiredExercisesCompleted_MarksLessonAndCourseCompleted()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 1);
        var second = NewExercise(data.Lessons[0], 2);
        var third = NewExercise(data.Lessons[0], 3);
        db.AddRange(second, third);
        AddAttempt(db, data.User.Id, data.Lessons[0],
            [(data.Exercises[0], true), (second, true), (third, true)]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal("CourseCompleted", result.Value.State);
        Assert.Equal("Completed", result.Value.Units[0].Lessons[0].State);
        Assert.Equal(1, result.Value.CompletedLessonCount);
        Assert.Equal(100, result.Value.ProgressPercentage);
    }

    [Fact]
    public async Task HistoricalCompletedAttemptWithoutCurrentExerciseCompletion_DoesNotCompleteLesson()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 1);
        AddAttempt(db, data.User.Id, data.Lessons[0], [], LessonAttemptStatus.Completed);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal("InProgress", result.Value.State);
        Assert.Equal("Current", result.Value.Units[0].Lessons[0].State);
        Assert.Equal(0, result.Value.CompletedLessonCount);
    }

    [Fact]
    public async Task InactiveRequiredAndActiveOptionalExercises_DoNotBlockCompletion()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 1);
        var inactiveRequired = NewExercise(data.Lessons[0], 2, isActive: false);
        var activeOptional = NewExercise(data.Lessons[0], 3, isRequired: false);
        db.AddRange(inactiveRequired, activeOptional);
        AddAttempt(db, data.User.Id, data.Lessons[0], [(data.Exercises[0], true)]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal("Completed", result.Value.Units[0].Lessons[0].State);
        Assert.Equal("CourseCompleted", result.Value.State);
    }

    [Fact]
    public async Task NewActiveRequiredExercise_ReopensLessonAndCourseUntilNewExerciseIsCompleted()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 1);
        AddAttempt(db, data.User.Id, data.Lessons[0], [(data.Exercises[0], true)]);
        var newlyGenerated = NewExercise(data.Lessons[0], 2);
        db.Add(newlyGenerated);
        data.Assignment.Status = UserCourseAssignmentStatus.Completed;
        data.Assignment.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal("InProgress", result.Value.State);
        Assert.Equal(UserCourseAssignmentStatus.InProgress, result.Value.Course!.AssignmentStatus);
        Assert.Equal("Current", result.Value.Units[0].Lessons[0].State);
        Assert.Equal(0, result.Value.CompletedLessonCount);

        AddAttempt(db, data.User.Id, data.Lessons[0], [(newlyGenerated, true)]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var completedAgain = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);
        Assert.Equal("CourseCompleted", completedAgain.Value.State);
        Assert.Equal("Completed", completedAgain.Value.Units[0].Lessons[0].State);
    }

    [Fact]
    public async Task PartialCurrentRequiredCompletion_DoesNotCompleteLesson()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 1);
        var second = NewExercise(data.Lessons[0], 2);
        var third = NewExercise(data.Lessons[0], 3);
        db.AddRange(second, third);
        AddAttempt(db, data.User.Id, data.Lessons[0],
            [(data.Exercises[0], true), (second, true), (third, false)]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal("Current", result.Value.Units[0].Lessons[0].State);
        Assert.Equal(0, result.Value.CompletedLessonCount);
    }

    [Fact]
    public async Task PublishedLessonWithZeroRequiredExercises_IsNotCompleted()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 1);
        data.Exercises[0].IsRequired = false;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal("InProgress", result.Value.State);
        Assert.Equal("Upcoming", result.Value.Units[0].Lessons[0].State);
        Assert.Equal(0, result.Value.CompletedLessonCount);
    }

    [Fact]
    public async Task CourseCompletion_IsDerivedFromEveryPublishedLesson()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 2);
        AddAttempt(db, data.User.Id, data.Lessons[0], [(data.Exercises[0], true)]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var partial = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);
        Assert.Equal("InProgress", partial.Value.State);
        Assert.Equal(1, partial.Value.CompletedLessonCount);
        Assert.Equal(50, partial.Value.ProgressPercentage);

        AddAttempt(db, data.User.Id, data.Lessons[1], [(data.Exercises[1], true)]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var complete = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);
        Assert.Equal("CourseCompleted", complete.Value.State);
        Assert.Equal(2, complete.Value.CompletedLessonCount);
    }

    [Fact]
    public async Task ExerciseCompletion_IsUserSpecific()
    {
        await using var db = Db();
        var data = await SeedAsync(db, 1);
        var otherUser = new User { Email = "other@test.local", FullName = "Other" };
        db.Add(otherUser);
        AddAttempt(db, otherUser.Id, data.Lessons[0], [(data.Exercises[0], true)]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, data.User.Id).Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal("Current", result.Value.Units[0].Lessons[0].State);
        Assert.Equal(0, result.Value.CompletedLessonCount);
    }

    private static GetLearningProgressQueryHandler Handler(ApplicationDbContext db, Guid userId)
    {
        var current = new FakeCurrentUser(userId);
        var resolver = new SequentialLearningPathResolver(db, current, NullLogger<SequentialLearningPathResolver>.Instance);
        return new(db, current, resolver);
    }

    private static async Task<ProgressData> SeedAsync(ApplicationDbContext db, int lessonCount)
    {
        var user = new User { Email = Guid.NewGuid() + "@test.local", FullName = "Learner" };
        var course = new Course
        {
            Code = Guid.NewGuid().ToString(),
            Title = "Course",
            DisplayOrder = 1,
            IsPublished = true,
            CefrLevel = CefrLevel.A1
        };
        var unit = new Unit { Course = course, Code = Guid.NewGuid().ToString(), Title = "Unit", DisplayOrder = 1 };
        var lessons = new List<Lesson>();
        var exercises = new List<Exercise>();
        for (var index = 0; index < lessonCount; index++)
        {
            var lesson = new Lesson
            {
                Unit = unit,
                Code = Guid.NewGuid().ToString(),
                Title = $"Lesson {index + 1}",
                DisplayOrder = index + 1,
                Status = LessonStatus.Published,
                DifficultyLevel = DifficultyLevel.Beginner,
                EstimatedDurationMinutes = 10
            };
            var exercise = NewExercise(lesson, 1);
            lessons.Add(lesson);
            exercises.Add(exercise);
            db.AddRange(lesson, exercise);
        }
        var assignment = new UserCourseAssignment
        {
            User = user,
            Course = course,
            Status = UserCourseAssignmentStatus.Assigned,
            AssignedAt = DateTime.UtcNow
        };
        db.AddRange(user, course, unit, assignment);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new(user, lessons, exercises, assignment);
    }

    private static Exercise NewExercise(
        Lesson lesson,
        int displayOrder,
        bool isActive = true,
        bool isRequired = true) => new()
        {
            Lesson = lesson,
            Type = ExerciseType.Typing,
            Title = $"Exercise {displayOrder}",
            Instruction = "Type",
            Difficulty = DifficultyLevel.Beginner,
            DisplayOrder = displayOrder,
            ContentJson = "{}",
            Version = 1,
            IsRequired = isRequired,
            IsActive = isActive
        };

    private static LessonAttempt AddAttempt(
        ApplicationDbContext db,
        Guid userId,
        Lesson lesson,
        IReadOnlyCollection<(Exercise Exercise, bool Completed)> exercises,
        LessonAttemptStatus status = LessonAttemptStatus.Completed)
    {
        var now = DateTime.UtcNow;
        var attempt = new LessonAttempt
        {
            UserId = userId,
            LessonId = lesson.Id,
            Status = status,
            StartedAt = now.AddMinutes(-1),
            CompletedAt = status == LessonAttemptStatus.Completed ? now : null,
            TotalActivityCount = exercises.Count,
            CompletedActivityCount = exercises.Count(value => value.Completed)
        };
        var order = 1;
        foreach (var (exercise, completed) in exercises)
            attempt.Activities.Add(new LessonAttemptExercise
            {
                LessonAttempt = attempt,
                Exercise = exercise,
                ExerciseVersion = exercise.Version,
                ActivityType = ActivityType.Lesson,
                DisplayOrder = order++,
                IsRequired = true,
                SourceLessonId = lesson.Id,
                CompletedAt = completed ? now : null
            });
        db.Add(attempt);
        return attempt;
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed record ProgressData(
        User User,
        List<Lesson> Lessons,
        List<Exercise> Exercises,
        UserCourseAssignment Assignment);
    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext { public Guid? UserId => userId; }
}
