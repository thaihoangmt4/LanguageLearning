using LanguageLearning.Common.Constants;
using LanguageLearning.WebApi.Features.Admin.Logs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/admin/logs")]
[Authorize(Policy = AppConstants.Policies.AdminOnly)]
public sealed class AdminLogsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AdminLogPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminLogPageResponse>> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] int limit = 100,
        [FromQuery] DateTimeOffset? beforeUtc = null,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(
            new GetAdminLogsQuery(level, search, fromUtc, toUtc, limit, beforeUtc),
            cancellationToken));
}
