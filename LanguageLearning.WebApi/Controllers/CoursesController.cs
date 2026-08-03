using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using LanguageLearning.WebApi.Features.LearningCatalog.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

/// <summary>
/// Provides learner-facing access to the published learning catalog.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CoursesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoursesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns all published courses ordered for display.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GetCoursesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GetCoursesResponse>> GetCourses(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCoursesQuery(), cancellationToken);

        return Ok(result.Value);
    }

    /// <summary>
    /// Returns a published course and its learner-visible curriculum structure.
    /// </summary>
    [HttpGet("{courseId:guid}")]
    [ProducesResponseType(typeof(GetCourseDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetCourseDetailResponse>> GetCourseDetail(
        Guid courseId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetCourseDetailQuery { CourseId = courseId },
            cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
