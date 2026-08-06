using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;
using MediatR;
using System.Diagnostics;

namespace LanguageLearning.WebApi.Features.ExerciseEngine.Commands;

public sealed record StartOrContinueLearningCommand : IRequest<Result<StartLearningSessionResponse>>;

public sealed class StartOrContinueLearningCommandHandler(
    ILearningSessionService learningSessionService,
    ILogger<StartOrContinueLearningCommandHandler> logger)
    : IRequestHandler<StartOrContinueLearningCommand, Result<StartLearningSessionResponse>>
{
    public async Task<Result<StartLearningSessionResponse>> Handle(StartOrContinueLearningCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await learningSessionService.StartOrResumeAsync(cancellationToken);
        if (result.IsSuccess)
        {
            var status = result.Value.Mode.ToString();
            logger.LogInformation("Learning session API state {Status} for LessonAttemptId {LessonAttemptId}, LessonId {LessonId}",
                status, result.Value.LessonAttemptId, result.Value.LessonId);
            logger.LogInformation("Start or continue learning completed in {DurationMs}ms", stopwatch.ElapsedMilliseconds);
            return Result<StartLearningSessionResponse>.Success(new(status, result.Value.LessonAttemptId, result.Value.LessonId));
        }
        if (result.Error == ExerciseWorkflowErrors.LearningPathCompleted)
            return Result<StartLearningSessionResponse>.Success(new("PathCompleted", null, null));
        if (result.Error == ExerciseWorkflowErrors.NoPublishedContent)
            return Result<StartLearningSessionResponse>.Success(new("NoPublishedContent", null, null));
        return Result<StartLearningSessionResponse>.Failure(result.Error);
    }
}
