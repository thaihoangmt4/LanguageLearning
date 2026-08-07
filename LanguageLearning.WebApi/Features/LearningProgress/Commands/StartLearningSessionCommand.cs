using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.ExerciseEngine.Queries;
using LanguageLearning.WebApi.Features.LearningProgress.DTOs;
using MediatR;

namespace LanguageLearning.WebApi.Features.LearningProgress.Commands;

public sealed record StartLearningSessionCommand : IRequest<Result<LearningSessionResponse>>;

public sealed class StartLearningSessionCommandHandler(ILearningSessionService sessionService, ISender sender)
    : IRequestHandler<StartLearningSessionCommand, Result<LearningSessionResponse>>
{
    public async Task<Result<LearningSessionResponse>> Handle(StartLearningSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionService.StartOrResumeAsync(cancellationToken);
        if (session.IsFailure) return Result<LearningSessionResponse>.Failure(session.Error);
        var player = await sender.Send(new GetLessonAttemptPlayerQuery(session.Value.LessonAttemptId), cancellationToken);
        if (player.IsFailure) return Result<LearningSessionResponse>.Failure(player.Error);
        return Result<LearningSessionResponse>.Success(new(session.Value.Mode.ToString(), player.Value));
    }
}
