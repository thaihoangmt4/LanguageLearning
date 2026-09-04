using System.Text.Json;
using FluentValidation;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.LearningCatalog;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LessonExperience;

public sealed record LessonExerciseResponse(Guid Id, ExerciseType Type, string Title, string Instruction,
    int Order, int Version, object Content);
public sealed record NextLessonResponse(Guid LessonId, string Title, string? Topic, string? LearningObjective,
    int Order, IReadOnlyList<LessonExerciseResponse> Exercises);
public sealed record SubmitExerciseAnswerRequest(int ExerciseVersion, JsonElement Answer);
public sealed record SubmitExerciseAnswerResponse(Guid ExerciseId, EvaluationStatus Status, decimal? Score,
    string? Feedback, object? CorrectAnswer);
public sealed record CompleteLessonResponse(Guid LessonId, DateTime CompletedAt, bool AlreadyCompleted);

public static class LessonExperienceErrors
{
    public const string CurrentUserUnavailable = "lesson_experience.current_user_unavailable";
    public const string NoActiveAssignment = "lesson_experience.no_active_assignment";
    public const string PathCompleted = "lesson_experience.path_completed";
    public const string InvalidLessonContent = "lesson_experience.invalid_lesson_content";
    public const string ExerciseNotFound = "lesson_experience.exercise_not_found";
    public const string ExerciseVersionMismatch = "lesson_experience.exercise_version_mismatch";
    public const string LessonNotFound = "lesson_experience.lesson_not_found";
}

public sealed record GetNextLessonQuery : IRequest<Result<NextLessonResponse>>;

public sealed class GetNextLessonQueryHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    IExerciseContentSerializer serializer,
    IExerciseDefinitionValidatorResolver definitionValidator,
    IExercisePublicContentMapper publicMapper,
    ILogger<GetNextLessonQueryHandler> logger)
    : IRequestHandler<GetNextLessonQuery, Result<NextLessonResponse>>
{
    public async Task<Result<NextLessonResponse>> Handle(GetNextLessonQuery request, CancellationToken token)
    {
        if (currentUser.UserId is not { } userId) return Result<NextLessonResponse>.Failure(LessonExperienceErrors.CurrentUserUnavailable);
        var courseId = await dbContext.UserCourseAssignments.AsNoTracking().Where(x => x.UserId == userId)
            .OrderBy(x => x.Status == UserCourseAssignmentStatus.Completed)
            .ThenByDescending(x => x.LastAccessedAt ?? x.AssignedAt).Select(x => (Guid?)x.CourseId).FirstOrDefaultAsync(token);
        if (courseId is null) return Result<NextLessonResponse>.Failure(LessonExperienceErrors.NoActiveAssignment);
        var completed = await dbContext.UserLessonProgress.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => x.LessonId).ToListAsync(token);
        var lesson = await dbContext.Lessons.AsNoTracking()
            .Where(x => x.Unit.CourseId == courseId && x.Status == LessonStatus.Published && !completed.Contains(x.Id))
            .OrderBy(x => x.Unit.DisplayOrder).ThenBy(x => x.DisplayOrder)
            .Select(x => new { x.Id, x.Title, Topic = x.Description, Objective = x.LearningObjectiveSummary, Order = x.DisplayOrder })
            .FirstOrDefaultAsync(token);
        if (lesson is null) return Result<NextLessonResponse>.Failure(LessonExperienceErrors.PathCompleted);
        var exercises = await dbContext.Exercises.AsNoTracking().Where(x => x.LessonId == lesson.Id && x.IsActive && x.IsRequired)
            .OrderBy(x => x.DisplayOrder).ToListAsync(token);

        var response = new List<LessonExerciseResponse>();
        foreach (var exercise in exercises)
        {
            var content = serializer.Deserialize(exercise.Type, exercise.ContentJson);
            if (content.IsFailure || definitionValidator.Validate(exercise.Type, content.Value).IsFailure)
                return Result<NextLessonResponse>.Failure(LessonExperienceErrors.InvalidLessonContent);
            var publicContent = publicMapper.Map(exercise.Type, content.Value);
            if (publicContent.IsFailure) return Result<NextLessonResponse>.Failure(publicContent.Error);
            response.Add(new(exercise.Id, exercise.Type, exercise.Title, exercise.Instruction,
                exercise.DisplayOrder, exercise.Version, publicContent.Value));
        }
        return Result<NextLessonResponse>.Success(new(lesson.Id, lesson.Title, lesson.Topic, lesson.Objective, lesson.Order, response));
    }
}

public sealed record SubmitExerciseAnswerCommand(Guid ExerciseId, int ExerciseVersion, JsonElement Answer)
    : IRequest<Result<SubmitExerciseAnswerResponse>>;

public sealed class SubmitExerciseAnswerCommandValidator : AbstractValidator<SubmitExerciseAnswerCommand>
{
    public SubmitExerciseAnswerCommandValidator()
    {
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.ExerciseVersion).GreaterThan(0);
        RuleFor(x => x.Answer).Must(x => x.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null);
    }
}

public sealed class SubmitExerciseAnswerCommandHandler(
    ApplicationDbContext dbContext,
    IExerciseContentSerializer contentSerializer,
    IExerciseAnswerSerializer answerSerializer,
    IExerciseDefinitionValidatorResolver definitionValidator,
    IExerciseAnswerValidatorResolver answerValidator,
    IExerciseEvaluatorResolver evaluator)
    : IRequestHandler<SubmitExerciseAnswerCommand, Result<SubmitExerciseAnswerResponse>>
{
    public async Task<Result<SubmitExerciseAnswerResponse>> Handle(SubmitExerciseAnswerCommand request, CancellationToken token)
    {
        var exercise = await dbContext.Exercises.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ExerciseId && x.IsActive, token);
        if (exercise is null) return Result<SubmitExerciseAnswerResponse>.Failure(LessonExperienceErrors.ExerciseNotFound);
        if (exercise.Version != request.ExerciseVersion) return Result<SubmitExerciseAnswerResponse>.Failure(LessonExperienceErrors.ExerciseVersionMismatch);
        var content = contentSerializer.Deserialize(exercise.Type, exercise.ContentJson);
        if (content.IsFailure || definitionValidator.Validate(exercise.Type, content.Value).IsFailure)
            return Result<SubmitExerciseAnswerResponse>.Failure(ExerciseEngineErrors.InvalidDefinition);
        var answer = answerSerializer.Deserialize(exercise.Type, request.Answer.GetRawText());
        if (answer.IsFailure || answerValidator.Validate(exercise.Type, content.Value, answer.Value).IsFailure)
            return Result<SubmitExerciseAnswerResponse>.Failure(ExerciseEngineErrors.InvalidAnswer);
        var evaluated = evaluator.Evaluate(exercise.Type, content.Value, answer.Value);
        if (evaluated.IsFailure) return Result<SubmitExerciseAnswerResponse>.Failure(evaluated.Error);
        return Result<SubmitExerciseAnswerResponse>.Success(new(exercise.Id, evaluated.Value.Status,
            evaluated.Value.Score, evaluated.Value.Feedback, evaluated.Value.CorrectAnswer));
    }
}

public sealed record CompleteLessonCommand(Guid LessonId) : IRequest<Result<CompleteLessonResponse>>;

public sealed class CompleteLessonCommandValidator : AbstractValidator<CompleteLessonCommand>
{
    public CompleteLessonCommandValidator() => RuleFor(x => x.LessonId).NotEmpty();
}

public sealed class CompleteLessonCommandHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    TimeProvider timeProvider,
    ILogger<CompleteLessonCommandHandler> logger)
    : IRequestHandler<CompleteLessonCommand, Result<CompleteLessonResponse>>
{
    public async Task<Result<CompleteLessonResponse>> Handle(CompleteLessonCommand request, CancellationToken token)
    {
        if (currentUser.UserId is not { } userId) return Result<CompleteLessonResponse>.Failure(LessonExperienceErrors.CurrentUserUnavailable);
        var lesson = await dbContext.Lessons.AsNoTracking().Where(x => x.Id == request.LessonId)
            .Select(x => new { x.Id, CourseId = x.Unit.CourseId }).SingleOrDefaultAsync(token);
        if (lesson is null) return Result<CompleteLessonResponse>.Failure(LessonExperienceErrors.LessonNotFound);
        var existing = await dbContext.UserLessonProgress.SingleOrDefaultAsync(x => x.UserId == userId && x.LessonId == request.LessonId, token);
        if (existing is not null) return Result<CompleteLessonResponse>.Success(new(existing.LessonId, existing.CompletedAt, true));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        dbContext.UserLessonProgress.Add(new() { UserId = userId, LessonId = request.LessonId, CompletedAt = now });
        var assignment = await dbContext.UserCourseAssignments.SingleOrDefaultAsync(x => x.UserId == userId && x.CourseId == lesson.CourseId, token);
        if (assignment is not null)
        {
            assignment.Status = UserCourseAssignmentStatus.InProgress;
            assignment.StartedAt ??= now;
            assignment.LastAccessedAt = now;
        }
        await dbContext.SaveChangesAsync(token);
        var lessonIds = await dbContext.Lessons.AsNoTracking().Where(x => x.Unit.CourseId == lesson.CourseId && x.Status == LessonStatus.Published).Select(x => x.Id).ToListAsync(token);
        var completedCount = await dbContext.UserLessonProgress.AsNoTracking().CountAsync(x => x.UserId == userId && lessonIds.Contains(x.LessonId), token);
        if (assignment is not null && lessonIds.Count > 0 && completedCount == lessonIds.Count)
        {
            assignment.Status = UserCourseAssignmentStatus.Completed;
            assignment.CompletedAt = now;
            await dbContext.SaveChangesAsync(token);
        }
        logger.LogInformation("Lesson completion recorded. UserId: {UserId}, LessonId: {LessonId}", userId, request.LessonId);
        return Result<CompleteLessonResponse>.Success(new(request.LessonId, now, false));
    }
}