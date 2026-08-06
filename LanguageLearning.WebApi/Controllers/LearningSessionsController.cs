using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.ExerciseEngine.Commands;
using LanguageLearning.WebApi.Features.ExerciseEngine.DTOs;
using LanguageLearning.WebApi.Features.ExerciseEngine.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class LearningSessionsController(IMediator mediator) : ControllerBase
{
    [HttpPost("learning-sessions")]
    [ProducesResponseType(typeof(StartLearningSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StartLearningSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StartLearningSessionResponse>> StartOrContinue(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new StartOrContinueLearningCommand(), cancellationToken);
        if (result.IsFailure) return Problem(result.Error);
        return result.Value.Status == "Started"
            ? CreatedAtAction(nameof(GetAttempt), new { lessonAttemptId = result.Value.LessonAttemptId }, result.Value)
            : Ok(result.Value);
    }

    [HttpGet("lesson-attempts/{lessonAttemptId:guid}")]
    [ProducesResponseType(typeof(LessonAttemptPlayerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LessonAttemptPlayerResponse>> GetAttempt(Guid lessonAttemptId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLessonAttemptPlayerQuery(lessonAttemptId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpPost("lesson-attempts/{lessonAttemptId:guid}/activities/{activityId:guid}/submissions")]
    [RequestSizeLimit(ExerciseLimits.MaximumSubmissionBytes)]
    [ProducesResponseType(typeof(SubmitActivityAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SubmitActivityAnswerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitActivityAnswerResponse>> Submit(
        Guid lessonAttemptId, Guid activityId, SubmitActivityAnswerRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SubmitActivityAnswerCommand(lessonAttemptId, activityId,
            request.SubmissionId, request.ExerciseVersion, request.Answer), cancellationToken);
        if (result.IsFailure) return Problem(result.Error);
        return result.Value.IsIdempotentReplay ? Ok(result.Value) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private ObjectResult Problem(string code)
    {
        var status = code switch
        {
            ExerciseWorkflowErrors.LessonAttemptNotFound or ExerciseWorkflowErrors.LessonAttemptForbidden or
                ExerciseWorkflowErrors.LessonAttemptExerciseNotFound or ExerciseWorkflowErrors.ExerciseNotPartOfAttempt => StatusCodes.Status404NotFound,
            ExerciseWorkflowErrors.LessonAttemptCompleted or ExerciseWorkflowErrors.ExerciseInactive or
                ExerciseWorkflowErrors.ExerciseVersionMismatch or ExerciseWorkflowErrors.SubmissionPayloadMismatch or
                ExerciseWorkflowErrors.ActiveLessonAttemptConflict => StatusCodes.Status409Conflict,
            ExerciseEngineErrors.AnswerDeserializationFailed or ExerciseEngineErrors.InvalidAnswer => StatusCodes.Status400BadRequest,
            ExerciseWorkflowErrors.CurrentUserUnavailable => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };
        var details = new ProblemDetails
        {
            Status = status,
            Title = status >= 500 ? "Exercise processing failed" : "Request could not be completed",
            Detail = code,
            Instance = HttpContext.Request.Path
        };
        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(status, details);
    }
}