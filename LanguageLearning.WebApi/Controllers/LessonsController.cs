using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using LanguageLearning.WebApi.Features.LearningCatalog.Queries;
using LanguageLearning.WebApi.Features.LearningCatalog.Commands;
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

    /// <summary>
    /// Returns the safe, ordered interactive flow for a published lesson.
    /// </summary>
    [HttpGet("{lessonId:guid}/learning-flow")]
    [ProducesResponseType(typeof(LessonLearningFlowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LessonLearningFlowResponse>> GetLearningFlow(
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetLessonLearningFlowQuery { LessonId = lessonId },
            cancellationToken);

        if (result.IsFailure)
        {
            var error = new { error = result.Error };
            return result.Error == "lesson.invalid_learning_flow"
                ? Conflict(error)
                : NotFound(error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Evaluates one answer without persisting an attempt or learner progress.
    /// </summary>
    [HttpPost("{lessonId:guid}/questions/{questionId:guid}/evaluate")]
    [ProducesResponseType(typeof(EvaluateQuestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluateQuestionResponse>> EvaluateQuestion(
        Guid lessonId,
        Guid questionId,
        EvaluateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new EvaluateQuestionCommand
        {
            LessonId = lessonId,
            QuestionId = questionId,
            SelectedOptionId = request.SelectedOptionId,
            TextAnswer = request.TextAnswer
        }, cancellationToken);

        if (result.IsFailure)
        {
            var error = new { error = result.Error };
            return result.Error == "question.not_found"
                ? NotFound(error)
                : BadRequest(error);
        }

        return Ok(result.Value);
    }
}
