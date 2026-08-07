using System.Text.Json;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.Identity;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Evaluation;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.ExerciseEngine.Serialization;
using LanguageLearning.Common.ExerciseEngine.Validation;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.ExerciseEngine;

public sealed class LearningWorkflowTests
{
    [Fact]
    public async Task Path_ResumesExistingAttemptBeforeSelectingAnotherLesson()
    {
        await using var db = Db();
        var data = await SeedCatalogAsync(db, 1);
        var attempt = new LessonAttempt { UserId = data.User.Id, LessonId = data.Lessons[0].Id, StartedAt = DateTime.UtcNow };
        db.Add(attempt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Resolver(db, data.User.Id).ResolveAsync(TestContext.Current.CancellationToken);
        Assert.True(result.Value.IsResume);
        Assert.Equal(attempt.Id, result.Value.LessonAttemptId);
    }

    [Fact]
    public async Task Path_SelectsFirstUncompletedLessonInCurriculumOrder()
    {
        await using var db = Db();
        var data = await SeedCatalogAsync(db, 3);
        db.Add(new LessonAttempt
        {
            UserId = data.User.Id,
            LessonId = data.Lessons[0].Id,
            StartedAt = DateTime.UtcNow,
            Status = LessonAttemptStatus.Completed,
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Resolver(db, data.User.Id).ResolveAsync(TestContext.Current.CancellationToken);
        Assert.False(result.Value.IsResume);
        Assert.Equal(data.Lessons[1].Id, result.Value.LessonId);
    }

    [Fact]
    public async Task Path_OrdersUnitsBeforeLessons()
    {
        await using var db = Db();
        var data = await SeedCatalogAsync(db, 1);
        var course = data.Lessons[0].Unit.Course;
        data.Lessons[0].Unit.DisplayOrder = 2;
        var firstUnit = new Unit { Course = course, Code = Guid.NewGuid().ToString(), Title = "First Unit", DisplayOrder = 1 };
        var firstLesson = new Lesson { Unit = firstUnit, Code = Guid.NewGuid().ToString(), Title = "First Lesson",
            DisplayOrder = 5, Status = LessonStatus.Published, DifficultyLevel = DifficultyLevel.Beginner, EstimatedDurationMinutes = 10 };
        db.AddRange(firstUnit, firstLesson, Exercise(firstLesson, 1));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Resolver(db, data.User.Id).ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LearningPathState.StartNextLesson, result.Value.State);
        Assert.Equal(firstLesson.Id, result.Value.LessonId);
    }

    [Fact]
    public async Task Path_RespectsCourseDisplayOrder()
    {
        await using var db = Db();
        var later = await SeedCatalogAsync(db, 1);
        var earlier = await SeedCatalogAsync(db, 1);
        later.Lessons[0].Unit.Course.DisplayOrder = 2;
        earlier.Lessons[0].Unit.Course.DisplayOrder = 1;
        var assignment = await db.UserCourseAssignments.SingleAsync(x => x.UserId == later.User.Id, TestContext.Current.CancellationToken);
        assignment.Course = earlier.Lessons[0].Unit.Course;
        assignment.CourseId = earlier.Lessons[0].Unit.Course.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await Resolver(db, later.User.Id).ResolveAsync(TestContext.Current.CancellationToken);
        Assert.Equal(earlier.Lessons[0].Id, result.Value.LessonId);
    }

    [Fact]
    public async Task Path_DistinguishesNoPublishedContentFromCompletedPath()
    {
        await using var empty = Db();
        var userId = Guid.NewGuid();
        Assert.Equal(LearningPathState.NoActiveAssignment,
            (await Resolver(empty, userId).ResolveAsync(TestContext.Current.CancellationToken)).Value.State);

        await using var completed = Db();
        var data = await SeedCatalogAsync(completed, 1);
        completed.Add(new LessonAttempt
        {
            UserId = data.User.Id,
            LessonId = data.Lessons[0].Id,
            StartedAt = DateTime.UtcNow,
            Status = LessonAttemptStatus.Completed,
            CompletedAt = DateTime.UtcNow
        });
        await completed.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LearningPathState.CourseCompleted,
            (await Resolver(completed, data.User.Id).ResolveAsync(TestContext.Current.CancellationToken)).Value.State);
    }

    [Fact]
    public async Task Session_AddsOldestThreeEligibleReviewsBeforeCoreActivities()
    {
        await using var db = Db();
        var data = await SeedCatalogAsync(db, 2);
        var currentCore = data.Exercises[0];
        var oldExercises = new List<Exercise>();
        for (var i = 0; i < 5; i++)
        {
            var exercise = Exercise(data.Lessons[1], i + 1, isActive: i != 4);
            oldExercises.Add(exercise);
            db.Add(exercise);
            db.Add(new UserExerciseMistake
            {
                UserId = data.User.Id,
                Exercise = exercise,
                ExerciseVersion = 1,
                Status = UserExerciseMistakeStatus.Pending,
                FirstFailedAt = DateTime.UtcNow.AddDays(-10 + i),
                LastFailedAt = DateTime.UtcNow.AddDays(-10 + i),
                FailureCount = 1
            });
        }
        db.Add(new UserExerciseMistake
        {
            UserId = data.User.Id,
            Exercise = currentCore,
            ExerciseVersion = 1,
            Status = UserExerciseMistakeStatus.Pending,
            FirstFailedAt = DateTime.UtcNow.AddDays(-20),
            LastFailedAt = DateTime.UtcNow.AddDays(-20),
            FailureCount = 1
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var current = new FakeCurrentUser(data.User.Id);
        var service = new LearningSessionService(db, Resolver(db, data.User.Id), current, NullLogger<LearningSessionService>.Instance);
        var result = await service.StartOrResumeAsync(TestContext.Current.CancellationToken);
        var activities = await db.LessonAttemptExercises.Where(x => x.LessonAttemptId == result.Value.LessonAttemptId)
            .OrderBy(x => x.DisplayOrder).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, activities.Count);
        Assert.All(activities.Take(3), x => Assert.Equal(ActivityType.Review, x.ActivityType));
        Assert.Equal(oldExercises.Take(3).Select(x => x.Id), activities.Take(3).Select(x => x.ExerciseId));
        Assert.Equal(ActivityType.Lesson, activities[3].ActivityType);
        Assert.Equal(currentCore.Id, activities[3].ExerciseId);
        Assert.Equal(4, activities.Select(x => x.DisplayOrder).Distinct().Count());
    }

    [Fact]
    public void Model_HasOneInProgressAttemptPerUserAndLessonPartialUniqueIndex()
    {
        using var db = Db();
        var index = db.Model.FindEntityType(typeof(LessonAttempt))!.GetIndexes()
            .Single(x => x.GetDatabaseName() == "IX_lesson_attempts_UserId_LessonId_InProgress");
        Assert.True(index.IsUnique);
        Assert.Equal(
            [nameof(LessonAttempt.UserId), nameof(LessonAttempt.LessonId)],
            index.Properties.Select(x => x.Name));
        Assert.Equal("\"Status\" = 'InProgress'", index.GetFilter());
    }

    [Fact]
    public async Task RepeatedSessionStart_ResumesWithoutCreatingAnotherAttempt()
    {
        await using var db = Db();
        var data = await SeedCatalogAsync(db, 1);
        var current = new FakeCurrentUser(data.User.Id);
        var service = new LearningSessionService(db, Resolver(db, data.User.Id), current, NullLogger<LearningSessionService>.Instance);
        var started = await service.StartOrResumeAsync(TestContext.Current.CancellationToken);
        var resumed = await service.StartOrResumeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LearningSessionMode.Started, started.Value.Mode);
        Assert.Equal(LearningSessionMode.Resumed, resumed.Value.Mode);
        Assert.Equal(started.Value.LessonAttemptId, resumed.Value.LessonAttemptId);
        Assert.Equal(1, await db.LessonAttempts.CountAsync(TestContext.Current.CancellationToken));
        var assignment = await db.UserCourseAssignments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UserCourseAssignmentStatus.InProgress, assignment.Status);
        Assert.NotNull(assignment.StartedAt);
        Assert.NotNull(assignment.LastAccessedAt);
    }

    [Fact]
    public async Task Submission_IsIdempotentAndRejectsCompletedActivityRetry()
    {
        await using var db = Db();
        var setup = await SeedAttemptAsync(db, ExerciseType.MultipleChoice, activityCount: 2);
        var service = SubmissionService(db, setup.UserId);
        var wrong = JsonSerializer.Serialize(new MultipleChoiceAnswer(setup.IncorrectOptionId));
        var firstId = Guid.NewGuid();

        var first = await service.SubmitAsync(Request(setup, firstId, wrong), TestContext.Current.CancellationToken);
        var replayJson = $"{{  \"SelectedOptionId\" : \"{setup.IncorrectOptionId}\" }}";
        var replay = await service.SubmitAsync(Request(setup, firstId, replayJson), TestContext.Current.CancellationToken);
        Assert.True(replay.Value.IsReplay);
        Assert.Equal(first.Value.ExerciseAttemptId, replay.Value.ExerciseAttemptId);

        var mismatch = await service.SubmitAsync(Request(setup, firstId, JsonSerializer.Serialize(new MultipleChoiceAnswer(setup.CorrectOptionId))), TestContext.Current.CancellationToken);
        Assert.Equal(ExerciseWorkflowErrors.SubmissionPayloadMismatch, mismatch.Error);

        var second = await service.SubmitAsync(Request(setup, Guid.NewGuid(), wrong), TestContext.Current.CancellationToken);
        var third = await service.SubmitAsync(Request(setup, Guid.NewGuid(), JsonSerializer.Serialize(new MultipleChoiceAnswer(setup.CorrectOptionId))), TestContext.Current.CancellationToken);
        Assert.Equal(ExerciseWorkflowErrors.ExerciseNotCurrent, second.Error);
        Assert.Equal(ExerciseWorkflowErrors.ExerciseNotCurrent, third.Error);

        var mistake = await db.UserExerciseMistakes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, mistake.FailureCount);
        Assert.Equal(UserExerciseMistakeStatus.Pending, mistake.Status);
        var attempt = await db.LessonAttempts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0m, attempt.TotalScore);
        Assert.Equal(0, attempt.CorrectCount);
        Assert.Equal(1, attempt.IncorrectCount);
    }

    [Fact]
    public async Task CorrectReview_ResolvesMistakeAndCompletesLesson()
    {
        await using var db = Db();
        var setup = await SeedAttemptAsync(db, ExerciseType.MultipleChoice, activityCount: 1, ActivityType.Review);
        var mistake = new UserExerciseMistake
        {
            UserId = setup.UserId,
            ExerciseId = setup.ExerciseId,
            ExerciseVersion = 1,
            Status = UserExerciseMistakeStatus.Pending,
            FirstFailedAt = DateTime.UtcNow.AddDays(-1),
            LastFailedAt = DateTime.UtcNow.AddDays(-1),
            FailureCount = 1
        };
        db.Add(mistake);
        var activity = await db.LessonAttemptExercises.SingleAsync(TestContext.Current.CancellationToken);
        activity.UserExerciseMistakeId = mistake.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await SubmissionService(db, setup.UserId).SubmitAsync(
            Request(setup, Guid.NewGuid(), JsonSerializer.Serialize(new MultipleChoiceAnswer(setup.CorrectOptionId))), TestContext.Current.CancellationToken);
        Assert.Equal(LessonAttemptStatus.Completed, result.Value.LessonAttemptStatus);
        Assert.Equal(UserExerciseMistakeStatus.Resolved, mistake.Status);
        Assert.Equal(1, mistake.SuccessfulReviewCount);
        Assert.NotNull(mistake.ResolvedAt);
        var assignment = await db.UserCourseAssignments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UserCourseAssignmentStatus.Completed, assignment.Status);
        Assert.NotNull(assignment.CompletedAt);
    }

    [Fact]
    public async Task IncorrectReview_KeepsMistakePendingAndIncrementsFailureCount()
    {
        await using var db = Db();
        var setup = await SeedAttemptAsync(db, ExerciseType.MultipleChoice, 1, ActivityType.Review);
        var mistake = new UserExerciseMistake
        {
            UserId = setup.UserId,
            ExerciseId = setup.ExerciseId,
            ExerciseVersion = 1,
            Status = UserExerciseMistakeStatus.Pending,
            FirstFailedAt = DateTime.UtcNow.AddDays(-1),
            LastFailedAt = DateTime.UtcNow.AddDays(-1),
            FailureCount = 1
        };
        db.Add(mistake);
        (await db.LessonAttemptExercises.SingleAsync(TestContext.Current.CancellationToken)).UserExerciseMistakeId = mistake.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await SubmissionService(db, setup.UserId).SubmitAsync(
            Request(setup, Guid.NewGuid(), JsonSerializer.Serialize(new MultipleChoiceAnswer(setup.IncorrectOptionId))), TestContext.Current.CancellationToken);
        Assert.Equal(UserExerciseMistakeStatus.Pending, mistake.Status);
        Assert.Equal(2, mistake.FailureCount);
        Assert.Null(mistake.ResolvedAt);
    }

    [Fact]
    public async Task SpeakingDoesNotCreateMistakeAndVersionMismatchCreatesNoAttempt()
    {
        await using var speakingDb = Db();
        var speaking = await SeedAttemptAsync(speakingDb, ExerciseType.Speaking, 1);
        var result = await SubmissionService(speakingDb, speaking.UserId).SubmitAsync(
            Request(speaking, Guid.NewGuid(), JsonSerializer.Serialize(new SpeakingAnswer(true))), TestContext.Current.CancellationToken);
        Assert.Equal(EvaluationStatus.NotEvaluated, result.Value.Evaluation.Status);
        Assert.Empty(speakingDb.UserExerciseMistakes);

        await using var mismatchDb = Db();
        var mismatch = await SeedAttemptAsync(mismatchDb, ExerciseType.MultipleChoice, 1);
        var request = Request(mismatch, Guid.NewGuid(), JsonSerializer.Serialize(new MultipleChoiceAnswer(mismatch.CorrectOptionId))) with { ExerciseVersion = 2 };
        Assert.Equal(ExerciseWorkflowErrors.ExerciseVersionMismatch,
            (await SubmissionService(mismatchDb, mismatch.UserId).SubmitAsync(request, TestContext.Current.CancellationToken)).Error);
        Assert.Empty(mismatchDb.ExerciseAttempts);
    }

    private static SequentialLearningPathResolver Resolver(ApplicationDbContext db, Guid userId) =>
        new(db, new FakeCurrentUser(userId), NullLogger<SequentialLearningPathResolver>.Instance);

    private static ExerciseSubmissionService SubmissionService(ApplicationDbContext db, Guid userId)
    {
        IExerciseDefinitionValidator[] definitions = [new MultipleChoiceDefinitionValidator(), new SpeakingDefinitionValidator()];
        IExerciseAnswerValidator[] answers = [new MultipleChoiceAnswerValidator(), new SpeakingAnswerValidator()];
        IExerciseEvaluationStrategy[] evaluators = [new MultipleChoiceEvaluator(), new SpeakingEvaluator()];
        return new(db, new FakeCurrentUser(userId), new ExerciseContentSerializer(), new ExerciseAnswerSerializer(),
            new ExerciseDefinitionValidatorResolver(definitions), new ExerciseAnswerValidatorResolver(answers),
            new ExerciseEvaluatorResolver(evaluators), Resolver(db, userId), NullLogger<ExerciseSubmissionService>.Instance);
    }

    private static ExerciseSubmission Request(AttemptSetup setup, Guid submissionId, string json) =>
        new(setup.AttemptId, setup.ActivityId, 1, submissionId, json);

    private static async Task<AttemptSetup> SeedAttemptAsync(ApplicationDbContext db, ExerciseType type, int activityCount, ActivityType activityType = ActivityType.Lesson)
    {
        var data = await SeedCatalogAsync(db, 1, type);
        var exercise = data.Exercises[0];
        var attempt = new LessonAttempt
        {
            UserId = data.User.Id,
            LessonId = data.Lessons[0].Id,
            StartedAt = DateTime.UtcNow,
            TotalActivityCount = activityCount
        };
        for (var i = 0; i < activityCount; i++)
        {
            var selected = i == 0 ? exercise : Exercise(data.Lessons[0], i + 1, contentType: type);
            if (i > 0) db.Add(selected);
            attempt.Activities.Add(new LessonAttemptExercise
            {
                LessonAttempt = attempt,
                Exercise = selected,
                ExerciseVersion = 1,
                ActivityType = activityType,
                DisplayOrder = i + 1,
                IsRequired = true,
                SourceLessonId = data.Lessons[0].Id
            });
        }
        db.Add(attempt);
        await db.SaveChangesAsync();
        var activity = attempt.Activities.OrderBy(x => x.DisplayOrder).First();
        var content = (MultipleChoiceContent?)null;
        if (type == ExerciseType.MultipleChoice)
            content = JsonSerializer.Deserialize<MultipleChoiceContent>(exercise.ContentJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(data.User.Id, attempt.Id, activity.Id, exercise.Id, content?.CorrectOptionId ?? Guid.Empty,
            content?.Options.Single(x => x.Id != content.CorrectOptionId).Id ?? Guid.Empty);
    }

    private static async Task<CatalogData> SeedCatalogAsync(ApplicationDbContext db, int lessonCount, ExerciseType type = ExerciseType.MultipleChoice)
    {
        var user = new User { Email = Guid.NewGuid() + "@test.local", FullName = "Learner" };
        var course = new Course { Code = Guid.NewGuid().ToString(), Title = "Course", DisplayOrder = 1, IsPublished = true, CefrLevel = CefrLevel.A1 };
        var unit = new Unit { Course = course, Code = Guid.NewGuid().ToString(), Title = "Unit", DisplayOrder = 1 };
        var lessons = new List<Lesson>();
        var exercises = new List<Exercise>();
        for (var i = 0; i < lessonCount; i++)
        {
            var lesson = new Lesson
            {
                Unit = unit,
                Code = Guid.NewGuid().ToString(),
                Title = $"Lesson {i + 1}",
                DisplayOrder = i + 1,
                Status = LessonStatus.Published,
                DifficultyLevel = DifficultyLevel.Beginner,
                EstimatedDurationMinutes = 10
            };
            var exercise = Exercise(lesson, 1, contentType: type);
            lessons.Add(lesson); exercises.Add(exercise); db.AddRange(lesson, exercise);
        }
        db.AddRange(user, course, unit);
        db.Add(new UserCourseAssignment
        {
            User = user,
            Course = course,
            Status = UserCourseAssignmentStatus.Assigned,
            AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return new(user, lessons, exercises);
    }

    private static Exercise Exercise(Lesson lesson, int order, bool isActive = true, ExerciseType contentType = ExerciseType.MultipleChoice)
    {
        var one = Guid.NewGuid(); var two = Guid.NewGuid();
        object content = contentType == ExerciseType.Speaking
            ? new SpeakingContent("Speak", "Reference", null)
            : new MultipleChoiceContent("Question", [new(one, "Correct"), new(two, "Wrong")], one, "Explanation");
        return new Exercise
        {
            Lesson = lesson,
            Type = contentType,
            Title = "Exercise",
            Instruction = "Answer",
            Difficulty = DifficultyLevel.Beginner,
            DisplayOrder = order,
            ContentJson = JsonSerializer.Serialize(content, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Version = 1,
            IsRequired = true,
            IsActive = isActive
        };
    }

    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed record CatalogData(User User, List<Lesson> Lessons, List<Exercise> Exercises);
    private sealed record AttemptSetup(Guid UserId, Guid AttemptId, Guid ActivityId, Guid ExerciseId, Guid CorrectOptionId, Guid IncorrectOptionId);
    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext { public Guid? UserId => userId; }
}
