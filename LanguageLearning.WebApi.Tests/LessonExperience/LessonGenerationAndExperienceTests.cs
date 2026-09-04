using System.Text.Json;
using FluentValidation;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Evaluation;
using LanguageLearning.Common.ExerciseEngine.PublicContent;
using LanguageLearning.Common.ExerciseEngine.Serialization;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.Admin.LessonGenerationSettings;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using LanguageLearning.WebApi.Features.LessonExperience;
using LanguageLearning.WebApi.Features.LessonGeneration;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.LessonExperience;

public sealed class LessonGenerationAndExperienceTests
{
    [Fact]
    public async Task ValidGeneration_PersistsOneLessonAndExactlyTenOrderedExercises()
    {
        await using var db = Db();
        var unit = await SeedUnitAsync(db, existingLesson: true);
        var generator = new StubLessonGenerator(ValidLesson());

        var result = await GenerationHandler(db, generator).Handle(new(unit.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var lesson = await db.Lessons.Include(x => x.Exercises).SingleAsync(x => x.Id == result.Value.LessonId, TestContext.Current.CancellationToken);
        Assert.Equal(unit.Id, lesson.UnitId);
        Assert.Equal(2, lesson.DisplayOrder);
        Assert.Equal(10, lesson.Exercises.Count);
        Assert.Equal(Enumerable.Range(1, 10), lesson.Exercises.OrderBy(x => x.DisplayOrder).Select(x => x.DisplayOrder));
        Assert.Single(generator.Context!.ExistingLessons);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    public async Task InvalidExerciseCount_PersistsNothing(int count)
    {
        await using var db = Db();
        var unit = await SeedUnitAsync(db);
        var before = await db.Lessons.CountAsync(TestContext.Current.CancellationToken);
        var result = await GenerationHandler(db, new StubLessonGenerator(ValidLesson(count)))
            .Handle(new(unit.Id), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(before, await db.Lessons.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(db.Exercises);
    }

    [Fact]
    public async Task DuplicateOrderOrUnsupportedType_PersistsNothing()
    {
        await using var db = Db();
        var unit = await SeedUnitAsync(db);
        var invalid = ValidLesson() with { Exercises = ValidLesson().Exercises.Select((x, i) =>
            i == 1 ? new GeneratedLessonExercise(1, x.Exercise with { Type = ExerciseType.Speaking }) : x).ToArray() };
        var result = await GenerationHandler(db, new StubLessonGenerator(invalid)).Handle(new(unit.Id), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Empty(db.Exercises);
    }

    [Fact]
    public async Task DisabledGeneration_DoesNotCallProvider()
    {
        await using var db = Db();
        var unit = await SeedUnitAsync(db);
        var settings = new SystemSettings();
        settings.SetLessonGenerationEnabled(false, DateTime.UtcNow, Guid.NewGuid());
        db.SystemSettings.Add(settings);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var generator = new StubLessonGenerator(ValidLesson());
        var result = await GenerationHandler(db, generator).Handle(new(unit.Id), TestContext.Current.CancellationToken);
        Assert.Equal(LessonGenerationErrors.Disabled, result.Error);
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task ProviderFailure_PersistsNothingAndCancellationPropagates()
    {
        await using var db = Db();
        var unit = await SeedUnitAsync(db);
        var failed = await GenerationHandler(db, new StubLessonGenerator(new ExerciseGenerationException("down")))
            .Handle(new(unit.Id), TestContext.Current.CancellationToken);
        Assert.Equal(LessonGenerationErrors.ProviderFailure, failed.Error);
        Assert.Empty(db.Exercises);
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GenerationHandler(db, new CancellingGenerator()).Handle(new(unit.Id), source.Token));
    }

    [Fact]
    public async Task NextLesson_ReturnsTenOrderedExercisesWithoutAnswers()
    {
        await using var db = Db();
        var (user, lesson, exercise) = await SeedPlayableLessonAsync(db);
        var handler = NextHandler(db, user.Id);
        var result = await handler.Handle(new(), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Exercises.Count);
        Assert.Equal(Enumerable.Range(1, 10), result.Value.Exercises.Select(x => x.Order));
        Assert.DoesNotContain("correct", JsonSerializer.Serialize(result.Value.Exercises[0].Content), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(exercise.Id, result.Value.Exercises[0].Id);
    }

    [Fact]
    public async Task SubmitAnswer_IsStatelessAndCompleteLessonIsIdempotent()
    {
        await using var db = Db();
        var (user, lesson, exercise) = await SeedPlayableLessonAsync(db);
        using var answer = JsonDocument.Parse($"{{\"selectedOptionId\":\"{Guid.Parse("10000000-0000-0000-0000-000000000001")}\"}}");
        var submitted = await AnswerHandler(db).Handle(new(exercise.Id, 1, answer.RootElement.Clone()), TestContext.Current.CancellationToken);
        Assert.True(submitted.IsSuccess);
        Assert.Equal(EvaluationStatus.Correct, submitted.Value.Status);
        Assert.Empty(db.UserLessonProgress);

        var completion = CompleteHandler(db, user.Id);
        var first = await completion.Handle(new(lesson.Id), TestContext.Current.CancellationToken);
        var second = await completion.Handle(new(lesson.Id), TestContext.Current.CancellationToken);
        Assert.True(first.IsSuccess);
        Assert.True(second.Value.AlreadyCompleted);
        Assert.Equal(1, await db.UserLessonProgress.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LessonGenerationSetting_ReadsDefaultsAndUpdatesSingleton()
    {
        await using var db = Db();
        var initial = await new GetLessonGenerationSettingsQueryHandler(db).Handle(new(), TestContext.Current.CancellationToken);
        Assert.True(initial.Value.Enabled);
        var update = new UpdateLessonGenerationSettingsCommandHandler(db, new CurrentUser(Guid.NewGuid()), TimeProvider.System,
            NullLogger<UpdateLessonGenerationSettingsCommandHandler>.Instance);
        Assert.False((await update.Handle(new(false), TestContext.Current.CancellationToken)).Value.Enabled);
        Assert.True((await update.Handle(new(true), TestContext.Current.CancellationToken)).Value.Enabled);
        Assert.Equal(1, await db.SystemSettings.CountAsync(TestContext.Current.CancellationToken));
    }

    private static GenerateLessonCommandHandler GenerationHandler(ApplicationDbContext db, ILessonGenerator generator) => new(
        db, generator, new GeneratedExerciseValidator(), Serializer(), Definitions(), NullLogger<GenerateLessonCommandHandler>.Instance);
    private static GetNextLessonQueryHandler NextHandler(ApplicationDbContext db, Guid userId) => new(db, new CurrentUser(userId),
        Serializer(), Definitions(), PublicMapper(), NullLogger<GetNextLessonQueryHandler>.Instance);
    private static SubmitExerciseAnswerCommandHandler AnswerHandler(ApplicationDbContext db) => new(db, Serializer(),
        new ExerciseAnswerSerializer(), Definitions(), Answers(), Evaluators());
    private static CompleteLessonCommandHandler CompleteHandler(ApplicationDbContext db, Guid userId) => new(db,
        new CurrentUser(userId), TimeProvider.System, NullLogger<CompleteLessonCommandHandler>.Instance);
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ExerciseContentSerializer Serializer() => new();
    private static ExerciseDefinitionValidatorResolver Definitions() => new([new MultipleChoiceDefinitionValidator(), new ImageMatchingDefinitionValidator(),
        new AudioMatchingDefinitionValidator(), new TypingDefinitionValidator(), new SentenceOrderingDefinitionValidator(), new CategorizationDefinitionValidator(), new SpeakingDefinitionValidator()]);
    private static ExerciseAnswerValidatorResolver Answers() => new([new MultipleChoiceAnswerValidator(), new ImageMatchingAnswerValidator(),
        new AudioMatchingAnswerValidator(), new TypingAnswerValidator(), new SentenceOrderingAnswerValidator(), new CategorizationAnswerValidator(), new SpeakingAnswerValidator()]);
    private static ExerciseEvaluatorResolver Evaluators() => new([new MultipleChoiceEvaluator(), new ImageMatchingEvaluator(), new AudioMatchingEvaluator(),
        new TypingEvaluator(), new SentenceOrderingEvaluator(), new CategorizationEvaluator(), new SpeakingEvaluator()]);
    private static ExercisePublicContentMapper PublicMapper() => new([new MultipleChoicePublicMapper(), new ImageMatchingPublicMapper(),
        new AudioMatchingPublicMapper(), new TypingPublicMapper(), new SentenceOrderingPublicMapper(), new CategorizationPublicMapper(), new SpeakingPublicMapper()]);

    private static async Task<Unit> SeedUnitAsync(ApplicationDbContext db, bool existingLesson = false)
    {
        var course = new Course { Code = Guid.NewGuid().ToString("N"), Title = "Course", CefrLevel = CefrLevel.A1, DisplayOrder = 1, IsPublished = true };
        var unit = new Unit { Course = course, Code = "UNIT", Title = "Unit", Description = "Greetings", DisplayOrder = 1 };
        if (existingLesson) db.Lessons.Add(new() { Unit = unit, Code = "OLD", Title = "Old", Description = "Old topic", LearningObjectiveSummary = "Old objective",
            EstimatedDurationMinutes = 10, DifficultyLevel = DifficultyLevel.Beginner, DisplayOrder = 1, Status = LessonStatus.Published });
        else db.Units.Add(unit);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return unit;
    }

    private static GeneratedLesson ValidLesson(int count = 10) => new("New lesson", "New topic", "Learn a new skill",
        Enumerable.Range(1, count).Select(order => new GeneratedLessonExercise(order,
            order % 2 == 0
                ? new(ExerciseType.Typing, $"Type answer {order}", [], $"answer {order}", "Explanation")
                : new(ExerciseType.MultipleChoice, $"Question {order}", ["A", "B"], "A", "Explanation"))).ToArray());

    private static async Task<(User User, Lesson Lesson, Exercise First)> SeedPlayableLessonAsync(ApplicationDbContext db)
    {
        var user = new User { Email = "u@example.com", FullName = "User", PasswordHash = "x" };
        var course = new Course { Code = "C", Title = "Course", CefrLevel = CefrLevel.A1, DisplayOrder = 1, IsPublished = true };
        var unit = new Unit { Course = course, Code = "U", Title = "Unit", DisplayOrder = 1 };
        var lesson = new Lesson { Unit = unit, Code = "L", Title = "Lesson", EstimatedDurationMinutes = 10,
            DifficultyLevel = DifficultyLevel.Beginner, DisplayOrder = 1, Status = LessonStatus.Published };
        var correct = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var json = Serializer().Serialize(ExerciseType.MultipleChoice, new MultipleChoiceContent("Question",
            [new(correct, "Yes"), new(Guid.NewGuid(), "No")], correct, "Because")).Value;
        var exercises = Enumerable.Range(1, 10).Select(order => new Exercise { Lesson = lesson, Type = ExerciseType.MultipleChoice,
            Title = $"Exercise {order}", Instruction = "Choose", Difficulty = DifficultyLevel.Beginner,
            DisplayOrder = order, ContentJson = json, Version = 1, IsRequired = true, IsActive = true }).ToArray();
        db.AddRange(user, lesson);
        db.Exercises.AddRange(exercises);
        db.UserCourseAssignments.Add(new() { User = user, Course = course, AssignedAt = DateTime.UtcNow, Status = UserCourseAssignmentStatus.Assigned });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (user, lesson, exercises[0]);
    }

    private sealed class StubLessonGenerator : ILessonGenerator
    {
        private readonly GeneratedLesson? lesson; private readonly Exception? exception;
        public StubLessonGenerator(GeneratedLesson lesson) => this.lesson = lesson;
        public StubLessonGenerator(Exception exception) => this.exception = exception;
        public int Calls { get; private set; } public LessonGenerationContext? Context { get; private set; }
        public Task<GeneratedLesson> GenerateAsync(LessonGenerationContext context, CancellationToken token)
        { Calls++; Context = context; if (exception is not null) throw exception; return Task.FromResult(lesson!); }
    }
    private sealed class CancellingGenerator : ILessonGenerator
    { public Task<GeneratedLesson> GenerateAsync(LessonGenerationContext context, CancellationToken token) => Task.FromCanceled<GeneratedLesson>(token); }
    private sealed class CurrentUser(Guid id) : ICurrentUserContext { public Guid? UserId => id; }
}
