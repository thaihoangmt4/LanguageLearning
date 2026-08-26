using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.ExerciseGeneration;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine.Serialization;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseGeneration;

public sealed class ExerciseGenerationTests
{
    [Theory]
    [InlineData(20, 20, 40, 50, 0)]
    [InlineData(21, 20, 40, 50, 0)]
    [InlineData(5, 20, 40, 50, 35)]
    [InlineData(0, 20, 100, 50, 50)]
    public void Policy_ComputesRequiredQuantity(
        int current, int minimum, int target, int maximum, int expected)
    {
        Assert.Equal(expected, ExerciseGenerationPolicy.RequiredCount(current, minimum, target, maximum));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(21)]
    public async Task InventoryAtOrAboveThreshold_DoesNotGenerate(int inventory)
    {
        await using var db = CreateDb();
        await SeedLessonAsync(db, "NO-GEN", inventory);
        var generator = new StubGenerator(_ => ValidBatch(1));

        var result = await CreateHandler(db, generator).Handle(new(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.EligibleLessons);
        Assert.Equal(1, result.Value.SkippedLessons);
        Assert.Equal(0, generator.CallCount);
    }

    [Fact]
    public async Task CandidateLoading_RequiresPublishedCourseAndPublishedLesson()
    {
        await using var db = CreateDb();
        var unpublishedCourseLesson = await SeedLessonAsync(db, "UNPUBLISHED-COURSE", 0);
        unpublishedCourseLesson.Unit.Course.IsPublished = false;
        var draftLesson = await SeedLessonAsync(db, "DRAFT-LESSON", 0);
        draftLesson.Status = LessonStatus.Draft;
        await SeedLessonAsync(db, "ELIGIBLE", 0);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var generator = new StubGenerator(context => ValidBatch(context.RequestedCount));

        var result = await CreateHandler(db, generator, Options(target: 1, minimum: 1, maximum: 1, batchSize: 1))
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EligibleLessons);
        Assert.Equal(1, result.Value.ProcessedLessons);
        Assert.Equal(0, result.Value.SkippedLessons);
        Assert.Equal(1, generator.CallCount);
    }

    [Fact]
    public async Task CandidateCalculation_CountsOnlyActiveExercisesAndUsesMaximumDisplayOrderFromAllExercises()
    {
        await using var db = CreateDb();
        var lesson = await SeedLessonAsync(db, "INACTIVE", 0);
        var inactiveExercise = Exercise(lesson.Id, 9);
        inactiveExercise.IsActive = false;
        db.Exercises.Add(inactiveExercise);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var generator = new StubGenerator(context => ValidBatch(context.RequestedCount));

        var result = await CreateHandler(db, generator, Options(target: 1, minimum: 1, maximum: 1, batchSize: 1))
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EligibleLessons);
        var generatedExercise = await db.Exercises.SingleAsync(
            exercise => exercise.LessonId == lesson.Id && exercise.IsActive,
            TestContext.Current.CancellationToken);
        Assert.Equal(10, generatedExercise.DisplayOrder);
    }

    [Fact]
    public async Task BelowThreshold_RequestsTargetQuantityInConfiguredBatches()
    {
        await using var db = CreateDb();
        await SeedLessonAsync(db, "BELOW", 5);
        var requested = new List<int>();
        var generator = new StubGenerator(context =>
        {
            var offset = requested.Sum();
            requested.Add(context.RequestedCount);
            return ValidBatch(context.RequestedCount, offset);
        });

        var result = await CreateHandler(db, generator, Options(batchSize: 20)).Handle(new(), CancellationToken.None);

        Assert.Equal([20, 15], requested);
        Assert.Equal(35, result.Value.RequestedExercises);
        Assert.Equal(35, result.Value.AcceptedExercises);
    }

    [Fact]
    public async Task InvalidItemIsRejectedWhileValidItemIsPersisted()
    {
        await using var db = CreateDb();
        var lesson = await SeedLessonAsync(db, "PARTIAL", 0);
        var generator = new StubGenerator(_ => new GeneratedExerciseBatch([
            Valid("Valid question"),
            new(ExerciseType.MultipleChoice, "Invalid question", ["A", "B"], "C", null)
        ]));

        var result = await CreateHandler(db, generator, Options(target: 2, minimum: 1, batchSize: 2))
            .Handle(new(), CancellationToken.None);

        Assert.Equal(1, result.Value.AcceptedExercises);
        Assert.Equal(1, result.Value.RejectedExercises);
        Assert.Equal(1, await db.Exercises.CountAsync(
            x => x.LessonId == lesson.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DuplicateGeneratedItemsAreFiltered()
    {
        await using var db = CreateDb();
        await SeedLessonAsync(db, "DUP-BATCH", 0);
        var generator = new StubGenerator(_ => new GeneratedExerciseBatch([
            Valid("Hello, WORLD!"), Valid(" hello world ")
        ]));

        var result = await CreateHandler(db, generator, Options(target: 2, minimum: 1, batchSize: 2))
            .Handle(new(), CancellationToken.None);

        Assert.Equal(1, result.Value.AcceptedExercises);
        Assert.Equal(1, result.Value.RejectedExercises);
    }

    [Fact]
    public async Task ExistingContentHashDuplicateIsFiltered()
    {
        await using var db = CreateDb();
        var lesson = await SeedLessonAsync(db, "DUP-DB", 0);
        db.Exercises.Add(Exercise(lesson.Id, 1, ExerciseContentHasher.Compute(ExerciseType.MultipleChoice, "Existing?")));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var call = 0;
        var generator = new StubGenerator(_ => new GeneratedExerciseBatch([
            Valid(call++ == 0 ? "existing" : "new question")
        ]));

        var result = await CreateHandler(db, generator, Options(target: 3, minimum: 2, batchSize: 1))
            .Handle(new(), CancellationToken.None);

        Assert.Equal(1, result.Value.AcceptedExercises);
        Assert.Equal(1, result.Value.RejectedExercises);
    }

    [Fact]
    public async Task OneLessonProviderFailure_DoesNotPreventNextLesson()
    {
        await using var db = CreateDb();
        var failedLesson = await SeedLessonAsync(db, "FAIL", 0);
        await SeedLessonAsync(db, "SUCCEED", 0);
        var generator = new StubGenerator(context => context.LessonId == failedLesson.Id
            ? throw new ExerciseGenerationException("provider unavailable")
            : ValidBatch(context.RequestedCount));

        var result = await CreateHandler(db, generator, Options(target: 1, minimum: 1, batchSize: 1))
            .Handle(new(), CancellationToken.None);

        Assert.Equal(1, result.Value.FailedLessons);
        Assert.Equal(1, result.Value.ProcessedLessons);
        Assert.Equal(1, result.Value.AcceptedExercises);
    }

    [Fact]
    public async Task UnexpectedGeneratorFailure_IsNotSilentlySwallowed()
    {
        await using var db = CreateDb();
        await SeedLessonAsync(db, "SYSTEM-FAIL", 0);
        var generator = new StubGenerator(_ => throw new InvalidOperationException("implementation defect"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler(db, generator, Options(target: 1, minimum: 1, batchSize: 1))
                .Handle(new(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationFromGeneratorPropagates()
    {
        await using var db = CreateDb();
        await SeedLessonAsync(db, "CANCEL", 0);
        using var source = new CancellationTokenSource();
        var generator = new StubGenerator(_ =>
        {
            source.Cancel();
            throw new OperationCanceledException(source.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateHandler(db, generator, Options(target: 1, minimum: 1, batchSize: 1))
                .Handle(new(), source.Token));
    }

    [Fact]
    public async Task GenerationRun_UsesDatabaseBackedSettings()
    {
        await using var db = CreateDb();
        await SeedLessonAsync(db, "DATABASE-SETTINGS", 0);
        var settings = new ExerciseGenerationSettings();
        settings.Update(0, 24, 1, 3, 3, 2, DateTime.UtcNow, Guid.NewGuid());
        db.ExerciseGenerationSettings.Add(settings);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var requested = new List<int>();
        var generator = new StubGenerator(context =>
        {
            requested.Add(context.RequestedCount);
            return ValidBatch(context.RequestedCount, requested.Sum() - context.RequestedCount);
        });
        var result = await CreateHandler(db, generator)
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([2, 1], requested);
        Assert.Equal(3, result.Value.AcceptedExercises);
    }

    [Fact]
    public async Task GenerationRun_UsesOneImmutableSettingsSnapshot()
    {
        await using var db = CreateDb();
        await SeedLessonAsync(db, "SNAPSHOT", 0);
        var requested = new List<int>();
        var generator = new StubGenerator(context =>
        {
            requested.Add(context.RequestedCount);
            var settings = db.ExerciseGenerationSettings.Single();
            settings.Update(0, 24, 1, 3, 3, 3, DateTime.UtcNow, Guid.NewGuid());
            db.SaveChanges();
            return ValidBatch(context.RequestedCount, requested.Count - 1);
        });

        var handler = CreateHandler(
            db,
            generator,
            Options(minimum: 1, target: 3, maximum: 3, batchSize: 1));

        await handler
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.Equal([1, 1, 1], requested);
    }

    [Fact]
    public void BackgroundService_DependsOnScopeFactoryInsteadOfSender()
    {
        var parameters = typeof(ExerciseGenerationBackgroundService).GetConstructors().Single().GetParameters();
        Assert.Contains(parameters, x => x.ParameterType == typeof(IServiceScopeFactory));
        Assert.DoesNotContain(parameters, x => x.ParameterType.FullName == "MediatR.ISender");
    }

    private static GenerateExercisesCommandHandler CreateHandler(
        ApplicationDbContext db,
        IExerciseGenerator generator,
        ExerciseGenerationOptions? options = null)
    {
        if (!db.ExerciseGenerationSettings.Any())
        {
            var configured = options ?? Options();
            var settings = new ExerciseGenerationSettings();
            settings.Update(
                configured.InitialDelayMinutes,
                configured.IntervalHours,
                configured.MinimumExerciseThreshold,
                configured.TargetExerciseCount,
                configured.MaxExercisesPerLessonPerRun,
                configured.GenerationBatchSize,
                DateTime.UtcNow,
                Guid.NewGuid());
            db.ExerciseGenerationSettings.Add(settings);
            db.SaveChanges();
        }

        return new(
            db,
            generator,
            new GeneratedExerciseValidator(),
            new ExerciseContentSerializer(),
            new ExerciseDefinitionValidatorResolver([
                new MultipleChoiceDefinitionValidator(), new TypingDefinitionValidator()
            ]),
            NullLogger<GenerateExercisesCommandHandler>.Instance);
    }

    private static ExerciseGenerationOptions Options(
        int minimum = 20,
        int target = 40,
        int maximum = 50,
        int batchSize = 20) => new()
    {
        InitialDelayMinutes = 0,
        IntervalHours = 24,
        MinimumExerciseThreshold = minimum,
        TargetExerciseCount = target,
        MaxExercisesPerLessonPerRun = maximum,
        GenerationBatchSize = batchSize
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<Lesson> SeedLessonAsync(ApplicationDbContext db, string code, int exerciseCount)
    {
        var course = new Course
        {
            Code = $"COURSE-{code}", Title = "Course", CefrLevel = CefrLevel.A1,
            DisplayOrder = 1, IsPublished = true
        };
        var unit = new Unit { Course = course, CourseId = course.Id, Code = $"UNIT-{code}", Title = "Unit", DisplayOrder = 1 };
        var lesson = new Lesson
        {
            Unit = unit, UnitId = unit.Id, Code = code, Title = "Lesson",
            LearningObjectiveSummary = "Learn greetings", EstimatedDurationMinutes = 5,
            DifficultyLevel = DifficultyLevel.Beginner, DisplayOrder = 1, Status = LessonStatus.Published
        };
        db.AddRange(course, unit, lesson);
        for (var index = 1; index <= exerciseCount; index++)
            db.Exercises.Add(Exercise(lesson.Id, index));
        await db.SaveChangesAsync();
        return lesson;
    }

    private static Exercise Exercise(Guid lessonId, int order, string? hash = null) => new()
    {
        LessonId = lessonId,
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

    private static GeneratedExerciseBatch ValidBatch(int count, int offset = 0) =>
        new(Enumerable.Range(1, count).Select(index => Valid($"Question {offset + index}")).ToArray());

    private static GeneratedExercise Valid(string question) =>
        new(ExerciseType.MultipleChoice, question, ["Yes", "No"], "Yes", "Explanation");

    private sealed class StubGenerator(Func<ExerciseGenerationContext, GeneratedExerciseBatch> generate)
        : IExerciseGenerator
    {
        public int CallCount { get; private set; }

        public Task<GeneratedExerciseBatch> GenerateAsync(
            ExerciseGenerationContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(generate(context));
        }
    }

}
