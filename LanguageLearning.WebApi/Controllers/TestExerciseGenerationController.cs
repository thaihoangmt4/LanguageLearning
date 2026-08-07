using LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;
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
    public async Task<ActionResult<GenerateExercisesResult>> GenerateExercises(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateExercisesCommand(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpPost("reset-generated-exercises")]
    [ProducesResponseType(typeof(ResetGeneratedExercisesResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResetGeneratedExercisesResult>> ResetGeneratedExercises(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResetGeneratedExercisesCommand(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
