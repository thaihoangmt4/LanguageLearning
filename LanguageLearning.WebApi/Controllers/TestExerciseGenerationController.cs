using LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

/// <summary>Temporary development-only endpoints for manually verifying Sprint 8.</summary>
[ApiController]
[Route("api/test")]
[Authorize]
public sealed class TestExerciseGenerationController(ISender sender) : ControllerBase
{
    [HttpPost("generate-exercises")]
    [ProducesResponseType(typeof(GenerateExercisesResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerateExercisesResult>> GenerateExercises(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateExercisesCommand(), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.Error == ExerciseGenerationSettingsErrors.Disabled
            ? Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "AI exercise generation is disabled.",
                Detail = "An administrator has disabled AI exercise generation.",
                Instance = HttpContext.Request.Path
            })
            : Problem(result.Error);
    }

    [HttpPost("reset-generated-exercises")]
    [ProducesResponseType(typeof(ResetGeneratedExercisesResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResetGeneratedExercisesResult>> ResetGeneratedExercises(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResetGeneratedExercisesCommand(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
