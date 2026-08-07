using System.Reflection;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Configuration;
using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Features.ExerciseEngine.Commands;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class ResetLearningProgressTests
{
    [Fact]
    public async Task DevelopmentReset_DeletesOnlyCurrentUsersLearningState()
    {
        await using var db = Db();
        var currentUser = new User { Email = "current@test.local", FullName = "Current" };
        var otherUser = new User { Email = "other@test.local", FullName = "Other" };
        var lesson = Catalog();
        var exercise = Exercise(lesson);
        var currentAttempt = Attempt(currentUser, lesson, exercise);
        var otherAttempt = Attempt(otherUser, lesson, exercise);
        var currentMistake = Mistake(currentUser, exercise);
        var otherMistake = Mistake(otherUser, exercise);
        db.AddRange(currentAttempt, otherAttempt, currentMistake, otherMistake);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new ResetLearningProgressCommandHandler(db, new CurrentUser(currentUser.Id),
            new TestEnvironment(Environments.Development), DefaultResolver(db, lesson.Unit.Course.Code),
            NullLogger<ResetLearningProgressCommandHandler>.Instance);
        var result = await handler.Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.LessonAttemptsDeleted);
        Assert.Equal(1, result.Value.ActivitiesDeleted);
        Assert.Equal(1, result.Value.SubmissionsDeleted);
        Assert.Equal(1, result.Value.MistakesDeleted);
        Assert.True(result.Value.AssignmentCreated);
        var token = TestContext.Current.CancellationToken;
        Assert.Empty(await db.LessonAttempts.Where(x => x.UserId == currentUser.Id).ToListAsync(token));
        Assert.Empty(await db.UserExerciseMistakes.Where(x => x.UserId == currentUser.Id).ToListAsync(token));
        Assert.Single(await db.LessonAttempts.Where(x => x.UserId == otherUser.Id).ToListAsync(token));
        Assert.Single(await db.UserExerciseMistakes.Where(x => x.UserId == otherUser.Id).ToListAsync(token));
        Assert.Single(await db.Courses.ToListAsync(token));
        Assert.Single(await db.Exercises.ToListAsync(token));
        Assert.Equal(2, await db.Users.CountAsync(token));
        var assignment = await db.UserCourseAssignments.SingleAsync(x => x.UserId == currentUser.Id, token);
        Assert.Equal(UserCourseAssignmentStatus.Assigned, assignment.Status);
        Assert.Equal(lesson.Unit.Course.Id, assignment.CourseId);
        Assert.Null(assignment.StartedAt);
        Assert.Null(assignment.LastAccessedAt);
        Assert.Null(assignment.CompletedAt);
    }

    [Fact]
    public async Task NonDevelopmentHandler_DoesNotExecuteReset()
    {
        await using var db = Db();
        var handler = new ResetLearningProgressCommandHandler(db, new CurrentUser(Guid.NewGuid()),
            new TestEnvironment(Environments.Production), DefaultResolver(db, "test"),
            NullLogger<ResetLearningProgressCommandHandler>.Instance);

        var result = await handler.Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal(ResetLearningProgressCommandHandler.NotAvailable, result.Error);
    }

    [Fact]
    public void Controller_IsAuthenticatedHasDeleteRouteAndIsDiscoveredOnlyInDevelopment()
    {
        var controller = typeof(TestLearningProgressController);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("learning-progress", controller.GetMethod(nameof(TestLearningProgressController.Reset))!
            .GetCustomAttribute<HttpDeleteAttribute>()!.Template);
        Assert.True(DevelopmentControllerFeatureProvider.IsAvailableInEnvironment(controller,
            new TestEnvironment(Environments.Development)));
        Assert.False(DevelopmentControllerFeatureProvider.IsAvailableInEnvironment(controller,
            new TestEnvironment(Environments.Production)));
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IDefaultCourseResolver DefaultResolver(ApplicationDbContext db, string code) =>
        new DefaultCourseResolver(db, new LearningOptions { DefaultCourseCode = code });

    private static Lesson Catalog()
    {
        var course = new Course { Code = "test", Title = "Test", DisplayOrder = 1, IsPublished = true, CefrLevel = CefrLevel.A1 };
        var unit = new LanguageLearning.Common.Entities.LearningCatalog.Unit { Course = course, Code = "unit", Title = "Unit", DisplayOrder = 1 };
        return new Lesson { Unit = unit, Code = "lesson", Title = "Lesson", DisplayOrder = 1, Status = LessonStatus.Published, DifficultyLevel = DifficultyLevel.Beginner, EstimatedDurationMinutes = 10 };
    }

    private static Exercise Exercise(Lesson lesson) => new()
    {
        Lesson = lesson,
        Type = ExerciseType.MultipleChoice,
        Title = "Exercise",
        Instruction = "Choose",
        Difficulty = DifficultyLevel.Beginner,
        DisplayOrder = 1,
        ContentJson = "{}",
        Version = 1,
        IsRequired = true,
        IsActive = true
    };

    private static LessonAttempt Attempt(User user, Lesson lesson, Exercise exercise)
    {
        var attempt = new LessonAttempt { User = user, Lesson = lesson, StartedAt = DateTime.UtcNow, TotalActivityCount = 1 };
        var activity = new LessonAttemptExercise { LessonAttempt = attempt, Exercise = exercise, ExerciseVersion = 1, ActivityType = ActivityType.Lesson, DisplayOrder = 1, IsRequired = true, SourceLesson = lesson };
        activity.ExerciseAttempts.Add(new() { LessonAttemptExercise = activity, SubmissionId = Guid.NewGuid(), ExerciseVersion = 1, AttemptNumber = 1, AnswerJson = "{}", ResultJson = "{}", SubmittedAt = DateTime.UtcNow });
        attempt.Activities.Add(activity);
        return attempt;
    }

    private static UserExerciseMistake Mistake(User user, Exercise exercise) => new()
    {
        User = user,
        Exercise = exercise,
        ExerciseVersion = 1,
        Status = UserExerciseMistakeStatus.Pending,
        FirstFailedAt = DateTime.UtcNow,
        LastFailedAt = DateTime.UtcNow,
        FailureCount = 1
    };

    private sealed class CurrentUser(Guid id) : ICurrentUserContext { public Guid? UserId => id; }

    private sealed class TestEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
