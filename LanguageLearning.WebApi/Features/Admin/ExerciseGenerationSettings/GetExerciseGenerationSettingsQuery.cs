using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ExerciseGenerationSettingsEntity = LanguageLearning.Common.Entities.ExerciseGeneration.ExerciseGenerationSettings;

namespace LanguageLearning.WebApi.Features.Admin.ExerciseGenerationSettings;

public sealed record GetExerciseGenerationSettingsQuery
    : IRequest<Result<ExerciseGenerationSettingsResponse>>;

public sealed class GetExerciseGenerationSettingsQueryHandler(
    ApplicationDbContext dbContext)
    : IRequestHandler<GetExerciseGenerationSettingsQuery, Result<ExerciseGenerationSettingsResponse>>
{
    public async Task<Result<ExerciseGenerationSettingsResponse>> Handle(
        GetExerciseGenerationSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.ExerciseGenerationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == ExerciseGenerationSettingsEntity.SingletonId,
                cancellationToken);

        var enabled = await dbContext.SystemSettings
            .AsNoTracking()
            .Where(value => value.Id == SystemSettings.SingletonId)
            .Select(value => (bool?)value.ExerciseGenerationEnabled)
            .SingleOrDefaultAsync(cancellationToken)
            ?? true;

        return settings is null
            ? Result<ExerciseGenerationSettingsResponse>.Failure(
                ExerciseGenerationSettingsErrors.NotFound)
            : Result<ExerciseGenerationSettingsResponse>.Success(ToResponse(settings, enabled));
    }

    internal static ExerciseGenerationSettingsResponse ToResponse(
        ExerciseGenerationSettingsEntity settings,
        bool enabled) =>
        new(
            enabled,
            settings.InitialDelayMinutes,
            settings.IntervalHours,
            settings.MinimumExerciseThreshold,
            settings.TargetExerciseCount,
            settings.MaxExercisesPerLessonPerRun,
            DateTime.SpecifyKind(settings.UpdatedAtUtc, DateTimeKind.Utc),
            settings.UpdatedByUserId,
            settings.Version);
}
