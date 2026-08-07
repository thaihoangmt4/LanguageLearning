using FluentValidation;
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
        var items = await query.OrderByDescending(x => x.CompletedAt ?? x.LastAccessedAt ?? x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new LessonHistoryItemDto(x.Id, x.LessonId, x.Lesson.Title, x.Status, x.StartedAt,
                x.LastAccessedAt, x.CompletedAt, x.TotalScore, x.CompletedActivityCount, x.TotalActivityCount))
            .ToListAsync(cancellationToken);
        return Result<LearningHistoryResponse>.Success(new(request.PageNumber, request.PageSize, total, items));
    }
}
