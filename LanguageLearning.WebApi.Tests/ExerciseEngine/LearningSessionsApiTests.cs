using System.Reflection;
using System.Text.Json;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.ExerciseEngine.PublicContent;
using LanguageLearning.Common.ExerciseEngine.Serialization;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.ExerciseEngine.Commands;
using LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;
using LanguageLearning.WebApi.Features.ExerciseEngine.Queries;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class LearningSessionsApiTests
{
    [Fact]
    public void Controller_IsAuthenticatedAndExposesVersionedRoutes()
    {
        Assert.NotNull(typeof(LearningSessionsController).GetCustomAttribute<AuthorizeAttribute>());
        var templates = typeof(LearningSessionsController).GetMethods()
            .SelectMany(x => x.GetCustomAttributes<HttpMethodAttribute>()).Select(x => x.Template).ToArray();
        Assert.Contains("learning-sessions", templates);
        Assert.Contains("lesson-attempts/{lessonAttemptId:guid}", templates);
        Assert.Contains("lesson-attempts/{lessonAttemptId:guid}/activities/{activityId:guid}/submissions", templates);
        Assert.DoesNotContain(typeof(SubmitActivityAnswerRequest).GetProperties(), x =>
            x.Name is "UserId" or "LessonId" or "ExerciseId" or "ExerciseType");
    }

    [Fact]
    public async Task StartEndpoint_ReturnsCreatedForStartedAndOkForDomainStates()
    {
        var attemptId = Guid.NewGuid(); var lessonId = Guid.NewGuid();
        var started = Controller(new FakeMediator(Result<StartLearningSessionResponse>.Success(new("Started", attemptId, lessonId))));
        Assert.IsType<CreatedAtActionResult>((await started.StartOrContinue(TestContext.Current.CancellationToken)).Result);
        var completed = Controller(new FakeMediator(Result<StartLearningSessionResponse>.Success(new("PathCompleted", null, null))));
        Assert.IsType<OkObjectResult>((await completed.StartOrContinue(TestContext.Current.CancellationToken)).Result);
        var noContent = Controller(new FakeMediator(Result<StartLearningSessionResponse>.Success(new("NoPublishedContent", null, null))));
        Assert.IsType<OkObjectResult>((await noContent.StartOrContinue(TestContext.Current.CancellationToken)).Result);
    }

    [Fact]
    public async Task PlayerQuery_ReturnsOrderedReviewAndLessonActivitiesWithoutAnswers()
    {
        await using var db = Db();
        var user = new User { Email = "player@test.local", FullName = "Player" };
        var lesson = Lesson();
        var one = Guid.NewGuid(); var two = Guid.NewGuid();
        var content = new MultipleChoiceContent("Question", [new(one, "A"), new(two, "B")], one, "Secret explanation");
        var reviewExercise = Exercise(lesson, 1, JsonSerializer.Serialize(content));
        var lessonExercise = Exercise(lesson, 2, JsonSerializer.Serialize(content));
        var attempt = new LessonAttempt { User = user, Lesson = lesson, StartedAt = DateTime.UtcNow, TotalActivityCount = 2 };
        attempt.Activities.Add(new() { LessonAttempt = attempt, Exercise = lessonExercise, ExerciseVersion = 1, ActivityType = ActivityType.Lesson, DisplayOrder = 2, IsRequired = true, SourceLesson = lesson });
        attempt.Activities.Add(new() { LessonAttempt = attempt, Exercise = reviewExercise, ExerciseVersion = 1, ActivityType = ActivityType.Review, DisplayOrder = 1, IsRequired = true, SourceLesson = lesson });
        db.AddRange(attempt, reviewExercise, lessonExercise);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var definitions = new ExerciseDefinitionValidatorResolver([new MultipleChoiceDefinitionValidator()]);
        var publicMapper = new ExercisePublicContentMapper([new MultipleChoicePublicMapper()]);
        var handler = new GetLessonAttemptPlayerQueryHandler(db, new CurrentUser(user.Id), new ExerciseContentSerializer(),
            definitions, publicMapper, NullLogger<GetLessonAttemptPlayerQueryHandler>.Instance);
        var result = await handler.Handle(new(attempt.Id), TestContext.Current.CancellationToken);

        Assert.Equal([ActivityType.Review, ActivityType.Lesson], result.Value.Activities.Select(x => x.ActivityType));
        Assert.Equal(attempt.Activities.Single(x => x.DisplayOrder == 1).Id, result.Value.Attempt.CurrentActivityId);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("correctOptionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret explanation", json);
    }

    [Fact]
    public async Task PlayerQuery_HidesAnotherUsersAttempt()
    {
        await using var db = Db();
        var handler = new GetLessonAttemptPlayerQueryHandler(db, new CurrentUser(Guid.NewGuid()),
            new ExerciseContentSerializer(), new ExerciseDefinitionValidatorResolver([]),
            new ExercisePublicContentMapper([]), NullLogger<GetLessonAttemptPlayerQueryHandler>.Instance);
        var result = await handler.Handle(new(Guid.NewGuid()), TestContext.Current.CancellationToken);
        Assert.Equal(ExerciseWorkflowErrors.LessonAttemptNotFound, result.Error);
    }

    [Fact]
    public async Task SubmitHandler_MapsTypedPartialDetailsAndControllerUsesReplayStatus()
    {
        var item = Guid.NewGuid(); var selected = Guid.NewGuid(); var correct = Guid.NewGuid();
        var core = new ExerciseEvaluationResult(EvaluationStatus.PartiallyCorrect, 50, "Partial", "Explanation",
            new[] { new CategoryAssignment(item, correct) }, new[] { new ItemEvaluationDetail(item, selected, correct, false) });
        var serviceResult = new ExerciseSubmissionResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), ExerciseType.Categorization, 1, 1, false, core, 1, 2, Guid.NewGuid(),
            LessonAttemptStatus.InProgress, DateTime.UtcNow);
        var handler = new SubmitActivityAnswerCommandHandler(new FakeSubmissionService(serviceResult), NullLogger<SubmitActivityAnswerCommandHandler>.Instance);
        using var document = JsonDocument.Parse("{\"assignments\":[]}");
        var mapped = await handler.Handle(new(serviceResult.LessonAttemptId, serviceResult.ActivityId,
            serviceResult.SubmissionId, 1, document.RootElement.Clone()), TestContext.Current.CancellationToken);
        Assert.IsType<CorrectAssignmentsDto>(mapped.Value.Evaluation.CorrectAnswer);
        Assert.IsType<CategorizationDetailsDto>(mapped.Value.Evaluation.Details);

        var created = Controller(new FakeMediator(mapped));
        Assert.Equal(StatusCodes.Status201Created, ((ObjectResult)(await created.Submit(serviceResult.LessonAttemptId,
            serviceResult.ActivityId, new() { SubmissionId = serviceResult.SubmissionId, ExerciseVersion = 1, Answer = document.RootElement.Clone() },
            TestContext.Current.CancellationToken)).Result!).StatusCode);
        var replayResponse = mapped.Value with { IsIdempotentReplay = true };
        var replay = Controller(new FakeMediator(Result<SubmitActivityAnswerResponse>.Success(replayResponse)));
        Assert.IsType<OkObjectResult>((await replay.Submit(serviceResult.LessonAttemptId, serviceResult.ActivityId,
            new() { SubmissionId = serviceResult.SubmissionId, ExerciseVersion = 1, Answer = document.RootElement.Clone() },
            TestContext.Current.CancellationToken)).Result);
    }

    [Theory]
    [InlineData(ExerciseWorkflowErrors.ExerciseVersionMismatch, 409)]
    [InlineData(ExerciseEngineErrors.InvalidAnswer, 400)]
    [InlineData(ExerciseWorkflowErrors.LessonAttemptNotFound, 404)]
    public async Task SubmitEndpoint_MapsApplicationErrors(string error, int status)
    {
        using var document = JsonDocument.Parse("{}");
        var controller = Controller(new FakeMediator(Result<SubmitActivityAnswerResponse>.Failure(error)));
        var response = (ObjectResult)(await controller.Submit(Guid.NewGuid(), Guid.NewGuid(),
            new() { SubmissionId = Guid.NewGuid(), ExerciseVersion = 1, Answer = document.RootElement.Clone() }, TestContext.Current.CancellationToken)).Result!;
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(error, Assert.IsType<ProblemDetails>(response.Value).Extensions["code"]);
    }

    private static LearningSessionsController Controller(IMediator mediator) => new(mediator)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Lesson Lesson()
    {
        var course = new Course { Code = "c", Title = "Course", IsPublished = true, DisplayOrder = 1, CefrLevel = CefrLevel.A1 };
        var unit = new LanguageLearning.Common.Entities.LearningCatalog.Unit { Course = course, Code = "u", Title = "Unit", DisplayOrder = 1 };
        return new Lesson
        {
            Unit = unit,
            Code = "l",
            Title = "Lesson",
            Status = LessonStatus.Published,
            DisplayOrder = 1,
            EstimatedDurationMinutes = 10,
            DifficultyLevel = DifficultyLevel.Beginner
        };
    }
    private static Exercise Exercise(Lesson lesson, int order, string json) => new()
    {
        Lesson = lesson,
        Type = ExerciseType.MultipleChoice,
        Title = "Exercise",
        Instruction = "Choose",
        Difficulty = DifficultyLevel.Beginner,
        DisplayOrder = order,
        ContentJson = json,
        Version = 1,
        IsRequired = true,
        IsActive = true
    };
    private sealed class CurrentUser(Guid id) : ICurrentUserContext { public Guid? UserId => id; }
    private sealed class FakeSubmissionService(ExerciseSubmissionResult value) : IExerciseSubmissionService
    {
        public Task<Result<ExerciseSubmissionResult>> SubmitAsync(ExerciseSubmission submission, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ExerciseSubmissionResult>.Success(value));
    }
    private sealed class FakeMediator(object response) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => Task.FromResult((TResponse)response);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(response);
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => Empty<TResponse>();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => Empty<object?>();
        private static async IAsyncEnumerable<T> Empty<T>() { await Task.CompletedTask; yield break; }
    }
}
