using LanguageLearning.WebApi.Features.System.DatabaseMigration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(IMediator mediator) : ControllerBase
{
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
