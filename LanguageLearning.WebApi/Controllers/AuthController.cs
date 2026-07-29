using LanguageLearning.WebApi.Features.Authentication.Commands;
using LanguageLearning.WebApi.Features.Authentication.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

/// <summary>
/// Handles authentication operations: Google OAuth login, token refresh, and logout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Authenticates a user via Google OAuth. Creates an account if the user does not exist.
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin(
        [FromBody] GoogleLoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access token and rotated refresh token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken(
        [FromBody] RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Revokes a refresh token, logging the user out of that session.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
}