using FluentValidation;
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
            .Select(x => new { x.Id, x.LessonId, LessonTitle = x.Lesson.Title, x.Status, x.StartedAt,
                x.CompletedAt, x.TotalScore, x.CorrectCount, x.IncorrectCount,
                x.CompletedActivityCount, x.TotalActivityCount })
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
            return Result<LessonAttemptResultResponse>.Failure(ExerciseWorkflowErrors.LessonAttemptNotFound);

        var activities = await dbContext.LessonAttemptExercises.AsNoTracking()
            .Where(x => x.LessonAttemptId == attempt.Id).OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.Id, x.ExerciseId, ExerciseTitle = x.Exercise.Title, x.DisplayOrder, x.CompletedAt })
            .ToListAsync(cancellationToken);
        var ids = activities.Select(x => x.Id).ToArray();
        var latest = (await dbContext.ExerciseAttempts.AsNoTracking()
            .Where(x => ids.Contains(x.LessonAttemptExerciseId))
            .OrderByDescending(x => x.AttemptNumber)
            .Select(x => new { x.LessonAttemptExerciseId, x.EvaluationStatus, x.Score, x.AttemptNumber, x.SubmittedAt })
            .ToListAsync(cancellationToken))
            .GroupBy(x => x.LessonAttemptExerciseId).ToDictionary(x => x.Key, x => x.First());
        var results = activities.Select(activity =>
        {
            latest.TryGetValue(activity.Id, out var value);
            return new ActivityResultDto(activity.Id, activity.ExerciseId, activity.ExerciseTitle, activity.DisplayOrder,
                activity.CompletedAt is not null, value?.EvaluationStatus, value?.Score, value?.AttemptNumber, value?.SubmittedAt);
        }).ToArray();
        return Result<LessonAttemptResultResponse>.Success(new(attempt.Id, attempt.LessonId, attempt.LessonTitle,
            attempt.Status, attempt.StartedAt, attempt.CompletedAt, attempt.TotalScore, attempt.CorrectCount,
            attempt.IncorrectCount, attempt.CompletedActivityCount, attempt.TotalActivityCount, results));
    }
}
