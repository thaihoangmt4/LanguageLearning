using LanguageLearning.WebApi.Features.ExerciseEngine;
using LanguageLearning.WebApi.Features.ExerciseEngine.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/test")]
[Authorize]
public sealed class TestLearningProgressController(IMediator mediator) : ControllerBase
{
    [HttpDelete("learning-progress")]
    [ProducesResponseType(typeof(ResetLearningProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResetLearningProgressResponse>> Reset(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResetLearningProgressCommand(), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.Error == ExerciseWorkflowErrors.CurrentUserUnavailable
            ? Unauthorized()
            : NotFound();
    }
}
