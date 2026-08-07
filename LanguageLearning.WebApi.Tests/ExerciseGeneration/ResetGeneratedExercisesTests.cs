using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;
using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseGeneration;

public sealed class ResetGeneratedExercisesTests
{
    [Fact]
    public void ManualController_IsAuthorizedAndDevelopmentOnly()
    {
        var controller = typeof(TestExerciseGenerationController);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("generate-exercises", controller.GetMethod(nameof(TestExerciseGenerationController.GenerateExercises))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("reset-generated-exercises", controller.GetMethod(nameof(TestExerciseGenerationController.ResetGeneratedExercises))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.True(DevelopmentControllerFeatureProvider.IsAvailableInEnvironment(
            controller, new TestEnvironment(Environments.Development)));
        Assert.False(DevelopmentControllerFeatureProvider.IsAvailableInEnvironment(
            controller, new TestEnvironment(Environments.Production)));
    }

    [Fact]
    public async Task DevelopmentReset_DeletesOnlyUnreferencedHashedExercises()
    {
        await using var db = Db();
        var lesson = Catalog();
        var seed = Exercise(lesson, 1, null);
        var generated = Exercise(lesson, 2, "A".PadLeft(64, 'A'));
        var referenced = Exercise(lesson, 3, "B".PadLeft(64, 'B'));
        var user = new User { Email = "test@example.com", FullName = "Test User", PasswordHash = "not-used" };
        var attempt = new LessonAttempt { User = user, Lesson = lesson, StartedAt = DateTime.UtcNow };
        attempt.Activities.Add(new LessonAttemptExercise
        {
            LessonAttempt = attempt,
            Exercise = referenced,
            ExerciseVersion = 1,
            ActivityType = ActivityType.Lesson,
            DisplayOrder = 1,
            IsRequired = true,
            SourceLesson = lesson
        });
        db.AddRange(seed, generated, referenced, user, attempt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, Environments.Development)
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.DetectedGeneratedExercises);
        Assert.Equal(1, result.Value.DeletedExercises);
        Assert.Equal(1, result.Value.PreservedReferencedExercises);
        Assert.True(await db.Exercises.AnyAsync(x => x.Id == seed.Id, TestContext.Current.CancellationToken));
        Assert.False(await db.Exercises.AnyAsync(x => x.Id == generated.Id, TestContext.Current.CancellationToken));
        Assert.True(await db.Exercises.AnyAsync(x => x.Id == referenced.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProductionReset_IsRejectedWithoutChangingData()
    {
        await using var db = Db();
        var exercise = Exercise(Catalog(), 1, "C".PadLeft(64, 'C'));
        db.Add(exercise);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Handler(db, Environments.Production)
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ResetGeneratedExercisesCommandHandler.NotAvailable, result.Error);
        Assert.Equal(1, await db.Exercises.CountAsync(TestContext.Current.CancellationToken));
    }

    private static ResetGeneratedExercisesCommandHandler Handler(ApplicationDbContext db, string environment) =>
        new(db, new TestEnvironment(environment), NullLogger<ResetGeneratedExercisesCommandHandler>.Instance);

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Lesson Catalog()
    {
        var course = new Course { Code = "course", Title = "Course", DisplayOrder = 1, IsPublished = true, CefrLevel = CefrLevel.A1 };
        var unit = new Unit { Course = course, Code = "unit", Title = "Unit", DisplayOrder = 1 };
        return new Lesson { Unit = unit, Code = "lesson", Title = "Lesson", DisplayOrder = 1,
            Status = LessonStatus.Published, DifficultyLevel = DifficultyLevel.Beginner, EstimatedDurationMinutes = 10 };
    }

    private static Exercise Exercise(Lesson lesson, int order, string? hash) => new()
    {
        Lesson = lesson,
        Type = ExerciseType.Typing,
        Title = $"Exercise {order}",
        Instruction = "Type",
        Difficulty = DifficultyLevel.Beginner,
        DisplayOrder = order,
        ContentJson = "{}",
        ContentHash = hash,
        Version = 1,
        IsRequired = true,
        IsActive = true
    };

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
