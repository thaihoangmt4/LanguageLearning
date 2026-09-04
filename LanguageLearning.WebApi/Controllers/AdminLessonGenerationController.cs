using LanguageLearning.Common.Constants;
using LanguageLearning.WebApi.Features.LessonGeneration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/admin/units/{unitId:guid}/lessons")]
[Authorize(Policy = AppConstants.Policies.AdminOnly)]
public sealed class AdminLessonGenerationController(ISender sender) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateLessonResponse>> Generate(Guid unitId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateLessonCommand(unitId), cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error switch
        {
            LessonGenerationErrors.Disabled => Conflict(new ProblemDetails { Status = 409, Title = "AI lesson generation is disabled.", Detail = result.Error }),
            LessonGenerationErrors.UnitNotFound => NotFound(),
            LessonGenerationErrors.InvalidAiContent => UnprocessableEntity(new ProblemDetails { Status = 422, Title = "AI generated invalid lesson content.", Detail = result.Error }),
            _ => Problem(statusCode: 503, title: "Lesson generation failed.", detail: result.Error)
        };
    }
}
