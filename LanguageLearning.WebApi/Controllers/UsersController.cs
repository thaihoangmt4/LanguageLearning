using LanguageLearning.WebApi.Features.Authentication.DTOs;
using LanguageLearning.WebApi.Features.Authentication.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LanguageLearning.WebApi.Controllers;

/// <summary>
/// Handles user profile operations for authenticated users.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid or missing user identity." });

        var query = new GetCurrentUserQuery { UserId = userId.ToString() };

        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }
}