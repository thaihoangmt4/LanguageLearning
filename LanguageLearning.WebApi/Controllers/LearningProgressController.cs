using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.LearningProgress.Commands;
using LanguageLearning.WebApi.Features.LearningProgress.DTOs;
using LanguageLearning.WebApi.Features.LearningProgress.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class LearningProgressController(IMediator mediator) : ControllerBase
{
    [HttpGet("learning/continue")]
    public async Task<ActionResult<ContinueLearningResponse>> Continue(CancellationToken cancellationToken) =>
        ToActionResult(await mediator.Send(new GetContinueLearningQuery(), cancellationToken));

    [HttpPost("learning/session")]
    public async Task<ActionResult<LearningSessionResponse>> Session(CancellationToken cancellationToken) =>
        ToActionResult(await mediator.Send(new StartLearningSessionCommand(), cancellationToken));

    [HttpGet("learning/progress")]
    public async Task<ActionResult<LearningProgressResponse>> Progress(CancellationToken cancellationToken) =>
        ToActionResult(await mediator.Send(new GetLearningProgressQuery(), cancellationToken));

    [HttpGet("learning/history")]
    public async Task<ActionResult<LearningHistoryResponse>> History(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        ToActionResult(await mediator.Send(new GetLearningHistoryQuery(pageNumber, pageSize), cancellationToken));

    [HttpGet("lesson-attempts/{lessonAttemptId:guid}/result")]
    public async Task<ActionResult<LessonAttemptResultResponse>> Result(Guid lessonAttemptId, CancellationToken cancellationToken) =>
        ToActionResult(await mediator.Send(new GetLessonAttemptResultQuery(lessonAttemptId), cancellationToken));

    private ActionResult<T> ToActionResult<T>(LanguageLearning.Common.Results.Result<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        var status = result.Error switch
        {
            ExerciseWorkflowErrors.CurrentUserUnavailable => StatusCodes.Status401Unauthorized,
            ExerciseWorkflowErrors.LessonAttemptNotFound => StatusCodes.Status404NotFound,
            ExerciseWorkflowErrors.ActiveLessonAttemptConflict or ExerciseWorkflowErrors.LearningPathCompleted => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        var details = new ProblemDetails { Status = status, Title = "Request could not be completed",
            Detail = result.Error, Instance = HttpContext.Request.Path };
        details.Extensions["code"] = result.Error;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(status, details);
    }
}
