using LanguageLearning.Common.Constants;
using LanguageLearning.WebApi.Features.Admin.LessonGenerationSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LanguageLearning.WebApi.Controllers;

[ApiController]
[Route("api/admin/settings/lesson-generation")]
[Authorize(Policy = AppConstants.Policies.AdminOnly)]
public sealed class AdminLessonGenerationSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LessonGenerationSettingsResponse>> Get(CancellationToken token)
    {
        var result = await sender.Send(new GetLessonGenerationSettingsQuery(), token);
        return Ok(result.Value);
    }

    [HttpPut]
    public async Task<ActionResult<LessonGenerationSettingsResponse>> Put(UpdateLessonGenerationSettingsRequest request, CancellationToken token)
    {
        var result = await sender.Send(new UpdateLessonGenerationSettingsCommand(request.Enabled), token);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized();
    }
}
