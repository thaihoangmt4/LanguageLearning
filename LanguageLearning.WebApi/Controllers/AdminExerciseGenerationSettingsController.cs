using LanguageLearning.Common.Constants;
using LanguageLearning.WebApi.Features.Admin.ExerciseGenerationSettings;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/admin/settings/exercise-generation")]
[Authorize(Policy = AppConstants.Policies.AdminOnly)]
public sealed class AdminExerciseGenerationSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ExerciseGenerationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExerciseGenerationSettingsResponse>> Get(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExerciseGenerationSettingsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ExerciseGenerationSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExerciseGenerationSettingsResponse>> Update(
        [FromBody] UpdateExerciseGenerationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateExerciseGenerationSettingsCommand(
                request.InitialDelayMinutes,
                request.IntervalHours,
                request.MinimumExerciseThreshold,
                request.TargetExerciseCount,
                request.MaxExercisesPerLessonPerRun,
                request.Version,
                request.Enabled),
            cancellationToken);

        if (result.IsSuccess)
            return Ok(result.Value);

        return result.Error switch
        {
            ExerciseGenerationSettingsErrors.CurrentUserUnavailable => Unauthorized(),
            ExerciseGenerationSettingsErrors.ConcurrencyConflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Exercise generation settings were modified by another administrator.",
                Detail = "Reload the current settings and retry the update.",
                Instance = HttpContext.Request.Path
            }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Exercise generation settings could not be updated.")
        };
    }
}
