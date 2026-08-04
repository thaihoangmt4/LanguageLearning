using System.Reflection;
using System.Text.Json;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using LanguageLearning.WebApi.Features.LearningCatalog.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Features.LearningCatalog;

public sealed class GetLessonLearningFlowQueryTests
{
    [Fact]
    public async Task PublishedLesson_ReturnsSafeOrderedFlow()
    {
        await using var db = CreateDbContext();
        var lesson = await SeedLessonAsync(db, LessonStatus.Published, includeFlow: true);
        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new GetLessonLearningFlowQuery { LessonId = lesson.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var steps = result.Value.Steps.ToArray();
        Assert.Equal([1, 2, 3], steps.Select(step => step.DisplayOrder));
        Assert.Equal([1, 2], steps[1].Question!.Options.Select(option => option.DisplayOrder));
        Assert.Equal("apple", steps[0].Instruction!.Vocabulary!.Word);
        Assert.Equal("/media/vocabulary/apple.webp", steps[1].Question!.PromptImageUrl);
        Assert.Equal("/media/audio/apple.mp3", steps[1].Question!.PromptAudioUrl);
        Assert.Empty(steps[2].Question!.Options);
    }

    [Fact]
    public async Task MissingLesson_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var result = await CreateHandler(db).Handle(
            new GetLessonLearningFlowQuery { LessonId = Guid.NewGuid() }, CancellationToken.None);
        Assert.Equal("lesson.not_found", result.Error);
    }

    [Theory]
    [InlineData(LessonStatus.Draft)]
    [InlineData(LessonStatus.Archived)]
    public async Task UnavailableLesson_ReturnsNotFound(LessonStatus status)
    {
        await using var db = CreateDbContext();
        var lesson = await SeedLessonAsync(db, status, includeFlow: true);
        var result = await CreateHandler(db).Handle(
            new GetLessonLearningFlowQuery { LessonId = lesson.Id }, CancellationToken.None);
        Assert.Equal("lesson.not_found", result.Error);
    }

    [Fact]
    public async Task PublishedLessonWithoutSteps_ReturnsInvalidFlow()
    {
        await using var db = CreateDbContext();
        var lesson = await SeedLessonAsync(db, LessonStatus.Published, includeFlow: false);
        var result = await CreateHandler(db).Handle(
            new GetLessonLearningFlowQuery { LessonId = lesson.Id }, CancellationToken.None);
        Assert.Equal("lesson.invalid_learning_flow", result.Error);
    }

    [Fact]
    public async Task PublishedLessonWithUnsupportedQuestionType_ReturnsInvalidFlow()
    {
        await using var db = CreateDbContext();
        var lesson = await SeedLessonAsync(db, LessonStatus.Published, includeFlow: true);
        var question = await db.Questions.FirstAsync(TestContext.Current.CancellationToken);
        question.QuestionType = (QuestionType)999;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateHandler(db).Handle(
            new GetLessonLearningFlowQuery { LessonId = lesson.Id },
            TestContext.Current.CancellationToken);

        Assert.Equal("lesson.invalid_learning_flow", result.Error);
    }

    [Fact]
    public void ResponseContract_DoesNotContainAnswerKeys()
    {
        var dtoProperties = typeof(QuestionStepDto).GetProperties()
            .Concat(typeof(QuestionOptionDto).GetProperties())
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain("IsCorrect", dtoProperties);
        Assert.DoesNotContain("TextAnswer", dtoProperties);
        Assert.DoesNotContain("CorrectOptionId", dtoProperties);

        var json = JsonSerializer.Serialize(new LessonLearningFlowResponse());
        Assert.DoesNotContain("IsCorrect", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TextAnswer", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Endpoint_RequiresAuthentication()
    {
        Assert.NotNull(typeof(LessonsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Handler_PropagatesCancellation()
    {
        await using var db = CreateDbContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateHandler(db).Handle(
                new GetLessonLearningFlowQuery { LessonId = Guid.NewGuid() }, cancellation.Token));
    }

    private static GetLessonLearningFlowQuery.Handler CreateHandler(ApplicationDbContext db) =>
        new(db, NullLogger<GetLessonLearningFlowQuery.Handler>.Instance);

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<Lesson> SeedLessonAsync(
        ApplicationDbContext db,
        LessonStatus status,
        bool includeFlow)
    {
        var lesson = new Lesson
        {
            UnitId = Guid.NewGuid(),
            Code = Guid.NewGuid().ToString(),
            Title = "Test lesson",
            DifficultyLevel = DifficultyLevel.Beginner,
            EstimatedDurationMinutes = 10,
            DisplayOrder = 1,
            Status = status
        };
        db.Lessons.Add(lesson);

        if (!includeFlow)
        {
            await db.SaveChangesAsync();
            return lesson;
        }

        var vocabulary = new Vocabulary
        {
            Word = "apple",
            Meaning = "quả táo",
            PartOfSpeech = PartOfSpeech.Noun,
            DifficultyLevel = DifficultyLevel.Beginner,
            ImageUrl = "/media/vocabulary/apple.webp",
            AudioUrl = "/media/audio/apple.mp3"
        };
        db.Vocabularies.Add(vocabulary);

        var instruction = new LearningStep
        {
            LessonId = lesson.Id,
            StepType = LearningStepType.Instruction,
            DisplayOrder = 1,
            IsRequired = true,
            VocabularyId = vocabulary.Id,
            InstructionTitle = "Apple",
            InstructionText = "Learn apple."
        };
        var multipleChoiceStep = new LearningStep
        {
            LessonId = lesson.Id,
            StepType = LearningStepType.Question,
            DisplayOrder = 2,
            IsRequired = true,
            VocabularyId = vocabulary.Id
        };
        var textInputStep = new LearningStep
        {
            LessonId = lesson.Id,
            StepType = LearningStepType.Question,
            DisplayOrder = 3,
            IsRequired = true,
            VocabularyId = vocabulary.Id
        };
        db.LearningSteps.AddRange(instruction, multipleChoiceStep, textInputStep);

        var multipleChoice = new Question
        {
            LearningStepId = multipleChoiceStep.Id,
            QuestionType = QuestionType.ImageMultipleChoice,
            Prompt = "Find apple",
            PromptImageUrl = vocabulary.ImageUrl,
            PromptAudioUrl = vocabulary.AudioUrl,
            TargetVocabularyId = vocabulary.Id
        };
        var textInput = new Question
        {
            LearningStepId = textInputStep.Id,
            QuestionType = QuestionType.TextInput,
            Prompt = "Type apple",
            TextAnswer = "apple",
            TargetVocabularyId = vocabulary.Id
        };
        db.Questions.AddRange(multipleChoice, textInput);
        db.QuestionOptions.AddRange(
            new QuestionOption { QuestionId = multipleChoice.Id, Text = "wrong", IsCorrect = false, DisplayOrder = 2 },
            new QuestionOption { QuestionId = multipleChoice.Id, Text = "apple", IsCorrect = true, DisplayOrder = 1 });

        await db.SaveChangesAsync();
        return lesson;
    }
}
