using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.LearningProgress.Queries;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class LearningProgressReadQueryTests
{
    [Fact]
    public async Task History_UsesLessonMetadataAndPreservesAttemptOrdering()
    {
        await using var db = Db();
        var setup = await SeedAsync(db);
        var older = new LessonAttempt
        {
            UserId = setup.UserId,
            LessonId = setup.LessonId,
            Status = LessonAttemptStatus.Completed,
            StartedAt = DateTime.UtcNow.AddDays(-2),
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.LessonAttempts.Add(older);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetLearningHistoryQueryHandler(db, new CurrentUser(setup.UserId))
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(setup.AttemptId, result.Value.Items[0].LessonAttemptId);
        Assert.All(result.Value.Items, item => Assert.Equal("Lesson", item.LessonTitle));
    }

    [Fact]
    public async Task AttemptResult_UsesLatestSubmissionForEachActivity()
    {
        await using var db = Db();
        var setup = await SeedAsync(db);

        var result = await new GetLessonAttemptResultQueryHandler(db, new CurrentUser(setup.UserId))
            .Handle(new(setup.AttemptId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lesson", result.Value.LessonTitle);
        var activity = Assert.Single(result.Value.Activities);
        Assert.Equal("Exercise", activity.ExerciseTitle);
        Assert.Equal(2, activity.AttemptNumber);
        Assert.Equal(EvaluationStatus.Correct, activity.EvaluationStatus);
    }

    private static async Task<Setup> SeedAsync(ApplicationDbContext db)
    {
        var user = new User { Email = "progress-read@test.local", FullName = "Learner" };
        var course = new Course
        {
            Code = "COURSE",
            Title = "Course",
            CefrLevel = CefrLevel.A1,
            DisplayOrder = 1,
            IsPublished = true
        };
        var unit = new Unit { Course = course, Code = "UNIT", Title = "Unit", DisplayOrder = 1 };
        var lesson = new Lesson
        {
            Unit = unit,
            Code = "LESSON",
            Title = "Lesson",
            Status = LessonStatus.Published,
            DisplayOrder = 1,
            DifficultyLevel = DifficultyLevel.Beginner,
            EstimatedDurationMinutes = 10
        };
        var exercise = new Exercise
        {
            Lesson = lesson,
            Type = ExerciseType.Typing,
            Title = "Exercise",
            Instruction = "Type",
            Difficulty = DifficultyLevel.Beginner,
            DisplayOrder = 1,
            ContentJson = "{}",
            Version = 1,
            IsRequired = true,
            IsActive = true
        };
        var attempt = new LessonAttempt
        {
            User = user,
            Lesson = lesson,
            Status = LessonAttemptStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            TotalActivityCount = 1,
            CompletedActivityCount = 1
        };
        var activity = new LessonAttemptExercise
        {
            LessonAttempt = attempt,
            Exercise = exercise,
            ExerciseVersion = 1,
            ActivityType = ActivityType.Lesson,
            DisplayOrder = 1,
            IsRequired = true,
            SourceLesson = lesson,
            CompletedAt = DateTime.UtcNow
        };
        db.AddRange(user, course, unit, lesson, exercise, attempt, activity);
        db.ExerciseAttempts.AddRange(
            Submission(activity, 1, EvaluationStatus.Incorrect),
            Submission(activity, 2, EvaluationStatus.Correct));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new(user.Id, lesson.Id, attempt.Id);
    }

    private static ExerciseAttempt Submission(
        LessonAttemptExercise activity,
        int attemptNumber,
        EvaluationStatus status) => new()
    {
        SubmissionId = Guid.NewGuid(),
        LessonAttemptExercise = activity,
        ExerciseVersion = 1,
        AttemptNumber = attemptNumber,
        AnswerJson = "{}",
        EvaluationStatus = status,
        Score = status == EvaluationStatus.Correct ? 100 : 0,
        SubmittedAt = DateTime.UtcNow.AddMinutes(attemptNumber)
    };

    private static ApplicationDbContext Db() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed record Setup(Guid UserId, Guid LessonId, Guid AttemptId);
    private sealed class CurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId => userId;
    }
}
