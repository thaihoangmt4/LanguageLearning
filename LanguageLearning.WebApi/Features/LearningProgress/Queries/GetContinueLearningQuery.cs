using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.LearningProgress.DTOs;
using MediatR;

namespace LanguageLearning.WebApi.Features.LearningProgress.Queries;

public sealed record GetContinueLearningQuery : IRequest<Result<ContinueLearningResponse>>;

public sealed class GetContinueLearningQueryHandler(ILearningPathResolver resolver)
    : IRequestHandler<GetContinueLearningQuery, Result<ContinueLearningResponse>>
{
    public async Task<Result<ContinueLearningResponse>> Handle(GetContinueLearningQuery request, CancellationToken cancellationToken)
    {
        var result = await resolver.ResolveAsync(cancellationToken);
        if (result.IsFailure) return Result<ContinueLearningResponse>.Failure(result.Error);
        var value = result.Value;
        var lesson = value.LessonId is { } lessonId && value.LessonTitle is { } title &&
            value.UnitTitle is { } unitTitle && value.EstimatedDurationMinutes is { } duration
            ? new NextLessonDto(lessonId, title, unitTitle, duration)
            : null;
        return Result<ContinueLearningResponse>.Success(new(
            value.State.ToString(), value.CourseId, value.LessonAttemptId, value.NextActivityId, lesson));
    }
}
