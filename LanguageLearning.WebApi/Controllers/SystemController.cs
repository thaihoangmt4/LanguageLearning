using LanguageLearning.Common.Constants;
using LanguageLearning.WebApi.Features.System.DatabaseMigration;
using LanguageLearning.WebApi.Features.System.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(IMediator mediator) : ControllerBase
{
    [Authorize(Policy = AppConstants.Policies.AdminOnly)]
    [HttpGet("settings")]
    [ProducesResponseType(typeof(SystemSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemSettingsResponse>> GetSettings(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSystemSettingsQuery(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "System settings could not be loaded.");
    }

    [Authorize(Policy = AppConstants.Policies.AdminOnly)]
    [HttpPut("settings")]
    [ProducesResponseType(typeof(SystemSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemSettingsResponse>> UpdateSettings(
        [FromBody] UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateSystemSettingsCommand(request.MinimumLogLevel),
            cancellationToken);

        if (result.IsSuccess)
            return Ok(result.Value);

        return result.Error == SystemSettingsErrors.CurrentUserUnavailable
            ? Unauthorized()
            : Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "System settings could not be updated.");
    }

    /// <summary>Applies pending EF Core migrations when manual migrations are enabled.</summary>
    /// <param name="migrationKey">The deployment secret configured in Migration:ApiKey.</param>
    [AllowAnonymous]
    [HttpPost("database/migrate")]
    [ProducesResponseType(typeof(DatabaseMigrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DatabaseMigrationResponse>> MigrateDatabase(
        [FromHeader(Name = "X-Migration-Key")] string? migrationKey,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MigrateDatabaseCommand(migrationKey), cancellationToken);

        return result.Status switch
        {
            DatabaseMigrationStatus.Completed => Ok(result.Response),
            DatabaseMigrationStatus.Disabled => StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Manual database migration is disabled." }),
            DatabaseMigrationStatus.Unauthorized => StatusCode(StatusCodes.Status403Forbidden,
                new { error = "Migration authorization failed." }),
            DatabaseMigrationStatus.Conflict => Conflict(
                new { error = "A database migration is already running." }),
            DatabaseMigrationStatus.Failed => StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Database migration failed." }),
            _ => throw new InvalidOperationException("Unknown database migration result.")
        };
    }
}
