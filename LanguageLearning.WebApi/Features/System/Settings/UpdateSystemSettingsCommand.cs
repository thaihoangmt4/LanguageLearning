using FluentValidation;
using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;

namespace LanguageLearning.WebApi.Features.System.Settings;

public sealed record UpdateSystemSettingsCommand(SystemLogLevel MinimumLogLevel)
    : IRequest<Result<SystemSettingsResponse>>;

public sealed class UpdateSystemSettingsCommandValidator
    : AbstractValidator<UpdateSystemSettingsCommand>
{
    public UpdateSystemSettingsCommandValidator() =>
        RuleFor(command => command.MinimumLogLevel).IsInEnum();
}

public sealed class UpdateSystemSettingsCommandHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider,
    LoggingLevelSwitch loggingLevelSwitch,
    ILogger<UpdateSystemSettingsCommandHandler> logger)
    : IRequestHandler<UpdateSystemSettingsCommand, Result<SystemSettingsResponse>>
{
    public async Task<Result<SystemSettingsResponse>> Handle(
        UpdateSystemSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserContext.UserId is not { } adminUserId)
        {
            return Result<SystemSettingsResponse>.Failure(
                SystemSettingsErrors.CurrentUserUnavailable);
        }

        var settings = await dbContext.SystemSettings.SingleOrDefaultAsync(
            value => value.Id == SystemSettings.SingletonId,
            cancellationToken);
        if (settings is null)
            return Result<SystemSettingsResponse>.Failure(SystemSettingsErrors.NotFound);

        var oldMinimumLogLevel = settings.MinimumLogLevel;
        settings.Update(
            request.MinimumLogLevel,
            timeProvider.GetUtcNow().UtcDateTime,
            adminUserId);

        await dbContext.SaveChangesAsync(cancellationToken);

        loggingLevelSwitch.MinimumLevel = request.MinimumLogLevel.ToSerilogLevel();
        logger.LogInformation(
            "System settings updated from OldMinimumLogLevel {OldMinimumLogLevel} to NewMinimumLogLevel {NewMinimumLogLevel} by AdminUserId {AdminUserId}",
            oldMinimumLogLevel,
            request.MinimumLogLevel,
            adminUserId);

        return Result<SystemSettingsResponse>.Success(
            GetSystemSettingsQueryHandler.ToResponse(settings));
    }
}
