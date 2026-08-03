using LanguageLearning.WebApi.Features.Authentication.DTOs;
using LanguageLearning.WebApi.Features.Authentication.Commands;
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

    /// <summary>
    /// Returns the authenticated user's learning profile.
    /// </summary>
    [HttpGet("me/profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetMyProfile(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Invalid or missing user identity." });

        var result = await _mediator.Send(
            new GetMyProfileQuery { UserId = userId },
            cancellationToken);

        if (result.IsFailure)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Updates the authenticated user's learning profile.
    /// </summary>
    [HttpPut("me/profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> UpdateMyProfile(
        [FromBody] UpdateUserProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Invalid or missing user identity." });

        command.UserId = userId;

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == "user_profile.not_found")
                return NotFound(new { error = result.Error });

            if (result.Error == "user_profile.username_already_exists")
                return Conflict(new { error = result.Error });

            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out userId);
    }
}
