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

public sealed record GetLearningHistoryQuery(int PageNumber = 1, int PageSize = 20)
    : IRequest<Result<LearningHistoryResponse>>;

public sealed class GetLearningHistoryQueryValidator : AbstractValidator<GetLearningHistoryQuery>
{
    public GetLearningHistoryQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetLearningHistoryQueryHandler(ApplicationDbContext dbContext, ICurrentUserContext currentUser)
    : IRequestHandler<GetLearningHistoryQuery, Result<LearningHistoryResponse>>
{
    public async Task<Result<LearningHistoryResponse>> Handle(GetLearningHistoryQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
            return Result<LearningHistoryResponse>.Failure(ExerciseWorkflowErrors.CurrentUserUnavailable);
        var query = dbContext.LessonAttempts.AsNoTracking().Where(x => x.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var attempts = await query.OrderByDescending(x => x.CompletedAt ?? x.LastAccessedAt ?? x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new AttemptRow(
                x.Id,
                x.LessonId,
                x.Status,
                x.StartedAt,
                x.LastAccessedAt,
                x.CompletedAt,
                x.TotalScore,
                x.CompletedActivityCount,
                x.TotalActivityCount))
            .ToListAsync(cancellationToken);

        var lessonIds = attempts.Select(attempt => attempt.LessonId).Distinct().ToList();
        var lessonTitles = lessonIds.Count == 0
            ? []
            : await dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lessonIds.Contains(lesson.Id))
                .Select(lesson => new { lesson.Id, lesson.Title })
                .ToDictionaryAsync(lesson => lesson.Id, lesson => lesson.Title, cancellationToken);
        var items = attempts.Select(attempt => new LessonHistoryItemDto(
            attempt.Id,
            attempt.LessonId,
            lessonTitles[attempt.LessonId],
            attempt.Status,
            attempt.StartedAt,
            attempt.LastAccessedAt,
            attempt.CompletedAt,
            attempt.TotalScore,
            attempt.CompletedActivityCount,
            attempt.TotalActivityCount)).ToList();

        return Result<LearningHistoryResponse>.Success(new(request.PageNumber, request.PageSize, total, items));
    }

    private sealed record AttemptRow(
        Guid Id,
        Guid LessonId,
        LessonAttemptStatus Status,
        DateTime StartedAt,
        DateTime? LastAccessedAt,
        DateTime? CompletedAt,
        decimal TotalScore,
        int CompletedActivityCount,
        int TotalActivityCount);
}
