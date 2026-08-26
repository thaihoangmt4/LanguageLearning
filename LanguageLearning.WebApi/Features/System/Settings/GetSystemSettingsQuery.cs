using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.System.Settings;

public sealed record GetSystemSettingsQuery : IRequest<Result<SystemSettingsResponse>>;

public sealed class GetSystemSettingsQueryHandler(ApplicationDbContext dbContext)
    : IRequestHandler<GetSystemSettingsQuery, Result<SystemSettingsResponse>>
{
    public async Task<Result<SystemSettingsResponse>> Handle(
        GetSystemSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == SystemSettings.SingletonId,
                cancellationToken);

        return settings is null
            ? Result<SystemSettingsResponse>.Failure(SystemSettingsErrors.NotFound)
            : Result<SystemSettingsResponse>.Success(ToResponse(settings));
    }

    internal static SystemSettingsResponse ToResponse(SystemSettings settings) => new(
        settings.MinimumLogLevel,
        DateTime.SpecifyKind(settings.UpdatedAtUtc, DateTimeKind.Utc),
        settings.UpdatedByUserId);
}
