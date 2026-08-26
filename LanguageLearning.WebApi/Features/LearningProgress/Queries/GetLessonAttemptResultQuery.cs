using FluentValidation;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.LearningProgress.DTOs;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LearningProgress.Queries;

public sealed record GetLessonAttemptResultQuery(Guid LessonAttemptId)
    : IRequest<Result<LessonAttemptResultResponse>>;

public sealed class GetLessonAttemptResultQueryValidator : AbstractValidator<GetLessonAttemptResultQuery>
{
    public GetLessonAttemptResultQueryValidator() => RuleFor(x => x.LessonAttemptId).NotEmpty();
}

public sealed class GetLessonAttemptResultQueryHandler(ApplicationDbContext dbContext, ICurrentUserContext currentUser)
    : IRequestHandler<GetLessonAttemptResultQuery, Result<LessonAttemptResultResponse>>
{
    public async Task<Result<LessonAttemptResultResponse>> Handle(GetLessonAttemptResultQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result<LessonAttemptResultResponse>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);
        var attempt = await dbContext.LessonAttempts.AsNoTracking()
            .Where(x => x.Id == request.LessonAttemptId && x.UserId == userId)
            .Select(x => new AttemptRow(
                x.Id,
                x.LessonId,
                x.Status,
                x.StartedAt,
                x.CompletedAt,
                x.TotalScore,
                x.CorrectCount,
                x.IncorrectCount,
                x.CompletedActivityCount,
                x.TotalActivityCount))
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
            return Result<LessonAttemptResultResponse>.Failure(ExerciseWorkflowErrors.LessonAttemptNotFound);

        var lessonTitle = await dbContext.Lessons.AsNoTracking()
            .Where(lesson => lesson.Id == attempt.LessonId)
            .Select(lesson => lesson.Title)
            .SingleAsync(cancellationToken);
        var activities = await dbContext.LessonAttemptExercises.AsNoTracking()
            .Where(x => x.LessonAttemptId == attempt.Id).OrderBy(x => x.DisplayOrder)
            .Select(x => new ActivityRow(x.Id, x.ExerciseId, x.DisplayOrder, x.CompletedAt))
            .ToListAsync(cancellationToken);
        var exerciseIds = activities.Select(activity => activity.ExerciseId).Distinct().ToList();
        var exerciseTitles = exerciseIds.Count == 0
            ? []
            : await dbContext.Exercises.AsNoTracking()
                .Where(exercise => exerciseIds.Contains(exercise.Id))
                .Select(exercise => new { exercise.Id, exercise.Title })
                .ToDictionaryAsync(exercise => exercise.Id, exercise => exercise.Title, cancellationToken);
        var ids = activities.Select(x => x.Id).ToArray();
        var submissionRows = ids.Length == 0
            ? []
            : await dbContext.ExerciseAttempts.AsNoTracking()
                .Where(x => ids.Contains(x.LessonAttemptExerciseId))
                .OrderByDescending(x => x.AttemptNumber)
                .Select(x => new SubmissionRow(
                    x.LessonAttemptExerciseId,
                    x.EvaluationStatus,
                    x.Score,
                    x.AttemptNumber,
                    x.SubmittedAt))
                .ToListAsync(cancellationToken);
        var latest = submissionRows
            .GroupBy(x => x.LessonAttemptExerciseId).ToDictionary(x => x.Key, x => x.First());
        var results = activities.Select(activity =>
        {
            latest.TryGetValue(activity.Id, out var value);
            return new ActivityResultDto(activity.Id, activity.ExerciseId, exerciseTitles[activity.ExerciseId], activity.DisplayOrder,
                activity.CompletedAt is not null, value?.EvaluationStatus, value?.Score, value?.AttemptNumber, value?.SubmittedAt);
        }).ToArray();
        return Result<LessonAttemptResultResponse>.Success(new(attempt.Id, attempt.LessonId, lessonTitle,
            attempt.Status, attempt.StartedAt, attempt.CompletedAt, attempt.TotalScore, attempt.CorrectCount,
            attempt.IncorrectCount, attempt.CompletedActivityCount, attempt.TotalActivityCount, results));
    }

    private sealed record AttemptRow(
        Guid Id,
        Guid LessonId,
        LessonAttemptStatus Status,
        DateTime StartedAt,
        DateTime? CompletedAt,
        decimal TotalScore,
        int CorrectCount,
        int IncorrectCount,
        int CompletedActivityCount,
        int TotalActivityCount);

    private sealed record ActivityRow(Guid Id, Guid ExerciseId, int DisplayOrder, DateTime? CompletedAt);

    private sealed record SubmissionRow(
        Guid LessonAttemptExerciseId,
        EvaluationStatus EvaluationStatus,
        decimal? Score,
        int AttemptNumber,
        DateTime SubmittedAt);
}
