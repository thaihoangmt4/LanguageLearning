using System.Reflection;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Features.LearningCatalog.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Features.LearningCatalog;

public sealed class EvaluateQuestionCommandTests
{
    [Fact]
    public async Task TextMultipleChoice_ReturnsCorrectAndIncorrectFeedback()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextMultipleChoice);
        var handler = CreateHandler(db);

        var correct = await handler.Handle(Command(data, selectedOptionId: data.CorrectOptionId), TestContext.Current.CancellationToken);
        var incorrect = await handler.Handle(Command(data, selectedOptionId: data.IncorrectOptionId), TestContext.Current.CancellationToken);

        Assert.True(correct.Value.IsCorrect);
        Assert.False(incorrect.Value.IsCorrect);
        Assert.Equal(data.CorrectOptionId, incorrect.Value.CorrectAnswer.OptionId);
        Assert.Equal("correct", incorrect.Value.CorrectAnswer.Text);
        Assert.Equal("Explanation", incorrect.Value.Explanation);
    }

    [Theory]
    [InlineData(QuestionType.ImageMultipleChoice)]
    [InlineData(QuestionType.AudioMultipleChoice)]
    public async Task MediaMultipleChoice_EvaluatesCorrectOption(QuestionType type)
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, type, correctText: null);
        var result = await CreateHandler(db).Handle(
            Command(data, selectedOptionId: data.CorrectOptionId), TestContext.Current.CancellationToken);

        Assert.True(result.Value.IsCorrect);
        Assert.Equal("Accessible correct answer", result.Value.CorrectAnswer.Text);
    }

    [Fact]
    public async Task TextInput_TrimsAndIgnoresCaseWhenConfigured()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextInput, isCaseSensitive: false);
        var result = await CreateHandler(db).Handle(Command(data, textAnswer: " Banana "), TestContext.Current.CancellationToken);
        Assert.True(result.Value.IsCorrect);
        Assert.Equal("banana", result.Value.CorrectAnswer.Text);
    }

    [Fact]
    public async Task TextInput_RespectsCaseWhenConfigured()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextInput, isCaseSensitive: true);
        var result = await CreateHandler(db).Handle(Command(data, textAnswer: "Banana"), TestContext.Current.CancellationToken);
        Assert.False(result.Value.IsCorrect);
    }

    [Fact]
    public async Task TextInput_BlankAnswerIsRejected()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextInput);
        var result = await CreateHandler(db).Handle(
            Command(data, textAnswer: "   "), TestContext.Current.CancellationToken);
        Assert.Equal("question.answer_invalid", result.Error);
    }

    [Fact]
    public async Task OptionFromAnotherQuestion_IsRejected()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextMultipleChoice);
        var other = await SeedQuestionAsync(db, QuestionType.TextMultipleChoice);
        var result = await CreateHandler(db).Handle(
            Command(data, selectedOptionId: other.CorrectOptionId), TestContext.Current.CancellationToken);
        Assert.Equal("question.option_invalid", result.Error);
    }

    [Fact]
    public async Task QuestionFromAnotherLesson_IsNotFound()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextMultipleChoice);
        var result = await CreateHandler(db).Handle(new EvaluateQuestionCommand
        {
            LessonId = Guid.NewGuid(),
            QuestionId = data.QuestionId,
            SelectedOptionId = data.CorrectOptionId
        }, TestContext.Current.CancellationToken);
        Assert.Equal("question.not_found", result.Error);
    }

    [Theory]
    [InlineData(LessonStatus.Draft)]
    [InlineData(LessonStatus.Archived)]
    public async Task UnavailableLessonQuestion_IsNotFound(LessonStatus status)
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextMultipleChoice, status: status);
        var result = await CreateHandler(db).Handle(
            Command(data, selectedOptionId: data.CorrectOptionId), TestContext.Current.CancellationToken);
        Assert.Equal("question.not_found", result.Error);
    }

    [Fact]
    public async Task TextAnswerForChoice_IsRejected()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextMultipleChoice);
        var result = await CreateHandler(db).Handle(Command(data, textAnswer: "correct"), TestContext.Current.CancellationToken);
        Assert.Equal("question.answer_invalid", result.Error);
    }

    [Fact]
    public async Task OptionForTextInput_IsRejected()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextInput);
        var result = await CreateHandler(db).Handle(
            Command(data, selectedOptionId: Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal("question.answer_invalid", result.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validator_RejectsBothOrNoAnswer(bool supplyBoth)
    {
        var validator = new EvaluateQuestionCommandValidator();
        var command = new EvaluateQuestionCommand
        {
            LessonId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            SelectedOptionId = supplyBoth ? Guid.NewGuid() : null,
            TextAnswer = supplyBoth ? "answer" : null
        };
        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public async Task UnsupportedType_FailsSafely()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, (QuestionType)999);
        var result = await CreateHandler(db).Handle(
            Command(data, selectedOptionId: data.CorrectOptionId), TestContext.Current.CancellationToken);
        Assert.Equal("question.type_not_supported", result.Error);
    }

    [Fact]
    public void Endpoint_RequiresAuthentication()
    {
        Assert.NotNull(typeof(LessonsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Evaluation_DoesNotPersistAttemptOrProgressData()
    {
        await using var db = CreateDbContext();
        var data = await SeedQuestionAsync(db, QuestionType.TextMultipleChoice);
        var countsBefore = await CountsAsync(db);

        await CreateHandler(db).Handle(Command(data, selectedOptionId: data.CorrectOptionId), TestContext.Current.CancellationToken);

        Assert.Equal(countsBefore, await CountsAsync(db));
        Assert.DoesNotContain(
            db.ChangeTracker.Entries(),
            entry => entry.State != EntityState.Unchanged);
    }

    private static EvaluateQuestionCommand.Handler CreateHandler(ApplicationDbContext db) =>
        new(db, NullLogger<EvaluateQuestionCommand.Handler>.Instance);

    private static EvaluateQuestionCommand Command(
        SeededQuestion data,
        Guid? selectedOptionId = null,
        string? textAnswer = null) => new()
        {
            LessonId = data.LessonId,
            QuestionId = data.QuestionId,
            SelectedOptionId = selectedOptionId,
            TextAnswer = textAnswer
        };

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<SeededQuestion> SeedQuestionAsync(
        ApplicationDbContext db,
        QuestionType type,
        bool isCaseSensitive = false,
        string? correctText = "correct",
        LessonStatus status = LessonStatus.Published)
    {
        var lesson = new Lesson
        {
            UnitId = Guid.NewGuid(), Code = Guid.NewGuid().ToString(), Title = "Lesson",
            DifficultyLevel = DifficultyLevel.Beginner, EstimatedDurationMinutes = 10,
            DisplayOrder = 1, Status = status
        };
        var step = new LearningStep
        {
            LessonId = lesson.Id, Lesson = lesson, StepType = LearningStepType.Question,
            DisplayOrder = 1, IsRequired = true
        };
        var question = new Question
        {
            LearningStepId = step.Id, LearningStep = step, QuestionType = type,
            Prompt = "Prompt", Explanation = "Explanation", TextAnswer = "banana",
            IsCaseSensitive = isCaseSensitive
        };
        var correct = new QuestionOption
        {
            QuestionId = question.Id, Question = question, Text = correctText,
            AccessibilityText = "Accessible correct answer", IsCorrect = true, DisplayOrder = 1
        };
        var incorrect = new QuestionOption
        {
            QuestionId = question.Id, Question = question, Text = "wrong",
            IsCorrect = false, DisplayOrder = 2
        };
        db.AddRange(lesson, step, question, correct, incorrect);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new(lesson.Id, question.Id, correct.Id, incorrect.Id);
    }

    private static async Task<(int Lessons, int Steps, int Questions, int Options)> CountsAsync(
        ApplicationDbContext db) =>
        (await db.Lessons.CountAsync(), await db.LearningSteps.CountAsync(),
            await db.Questions.CountAsync(), await db.QuestionOptions.CountAsync());

    private sealed record SeededQuestion(
        Guid LessonId,
        Guid QuestionId,
        Guid CorrectOptionId,
        Guid IncorrectOptionId);
}
