using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using LanguageLearning.WebApi.Features.LearningCatalog.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

/// <summary>
/// Provides learner-facing access to available lessons.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class LessonsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LessonsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns an available lesson with its catalog context and ordered sections.
    /// </summary>
    [HttpGet("{lessonId:guid}")]
    [ProducesResponseType(typeof(GetLessonDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetLessonDetailResponse>> GetLessonDetail(
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetLessonDetailQuery { LessonId = lessonId },
            cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

}
