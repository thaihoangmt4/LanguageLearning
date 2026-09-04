using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.WebApi.Features.LessonExperience;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/learning")]
[Authorize]
public sealed class LessonExperienceController(ISender sender) : ControllerBase
{
    [HttpGet("next-lesson")]
    public async Task<ActionResult<NextLessonResponse>> Next(CancellationToken token) =>
        Map(await sender.Send(new GetNextLessonQuery(), token));

    [HttpPost("exercises/{exerciseId:guid}/answer")]
    public async Task<ActionResult<SubmitExerciseAnswerResponse>> Answer(Guid exerciseId, SubmitExerciseAnswerRequest request, CancellationToken token) =>
        Map(await sender.Send(new SubmitExerciseAnswerCommand(exerciseId, request.ExerciseVersion, request.Answer), token));

    [HttpPost("lessons/{lessonId:guid}/complete")]
    public async Task<ActionResult<CompleteLessonResponse>> Complete(Guid lessonId, CancellationToken token) =>
        Map(await sender.Send(new CompleteLessonCommand(lessonId), token));

    private ActionResult<T> Map<T>(LanguageLearning.Common.Results.Result<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        var status = result.Error switch
        {
            LessonExperienceErrors.CurrentUserUnavailable => 401,
            LessonExperienceErrors.ExerciseNotFound or LessonExperienceErrors.LessonNotFound => 404,
            LessonExperienceErrors.PathCompleted or LessonExperienceErrors.InvalidLessonContent or LessonExperienceErrors.ExerciseVersionMismatch => 409,
            ExerciseEngineErrors.InvalidAnswer => 400,
            _ => 500
        };
        return StatusCode(status, new ProblemDetails { Status = status, Title = "Request could not be completed", Detail = result.Error });
    }
}
