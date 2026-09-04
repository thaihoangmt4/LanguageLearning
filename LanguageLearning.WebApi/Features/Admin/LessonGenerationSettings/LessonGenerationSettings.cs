using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.Admin.LessonGenerationSettings;

public sealed record LessonGenerationSettingsResponse(bool Enabled);
public sealed record UpdateLessonGenerationSettingsRequest(bool Enabled);
public sealed record GetLessonGenerationSettingsQuery : IRequest<Result<LessonGenerationSettingsResponse>>;
public sealed record UpdateLessonGenerationSettingsCommand(bool Enabled) : IRequest<Result<LessonGenerationSettingsResponse>>;

public sealed class GetLessonGenerationSettingsQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetLessonGenerationSettingsQuery, Result<LessonGenerationSettingsResponse>>
{
    public async Task<Result<LessonGenerationSettingsResponse>> Handle(GetLessonGenerationSettingsQuery request, CancellationToken token)
    {
        var enabled = await dbContext.SystemSettings.AsNoTracking().Where(x => x.Id == SystemSettings.SingletonId)
            .Select(x => (bool?)x.LessonGenerationEnabled).SingleOrDefaultAsync(token) ?? true;
        return Result<LessonGenerationSettingsResponse>.Success(new(enabled));
    }
}

public sealed class UpdateLessonGenerationSettingsCommandHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    TimeProvider timeProvider,
    ILogger<UpdateLessonGenerationSettingsCommandHandler> logger)
    : IRequestHandler<UpdateLessonGenerationSettingsCommand, Result<LessonGenerationSettingsResponse>>
{
    public async Task<Result<LessonGenerationSettingsResponse>> Handle(UpdateLessonGenerationSettingsCommand request, CancellationToken token)
    {
        if (currentUser.UserId is not { } userId)
            return Result<LessonGenerationSettingsResponse>.Failure("lesson_generation.current_user_unavailable");
        var settings = await dbContext.SystemSettings.SingleOrDefaultAsync(x => x.Id == SystemSettings.SingletonId, token) ?? new SystemSettings();
        if (dbContext.Entry(settings).State == EntityState.Detached) dbContext.SystemSettings.Add(settings);
        settings.SetLessonGenerationEnabled(request.Enabled, timeProvider.GetUtcNow().UtcDateTime, userId);
        await dbContext.SaveChangesAsync(token);
        logger.LogInformation("Lesson generation setting updated. AdminUserId: {AdminUserId}, Enabled: {Enabled}", userId, request.Enabled);
        return Result<LessonGenerationSettingsResponse>.Success(new(request.Enabled));
    }
}
