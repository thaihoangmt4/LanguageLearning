using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
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

        return settings is null
            ? Result<ExerciseGenerationSettingsResponse>.Failure(
                ExerciseGenerationSettingsErrors.NotFound)
            : Result<ExerciseGenerationSettingsResponse>.Success(ToResponse(settings));
    }

    internal static ExerciseGenerationSettingsResponse ToResponse(
        ExerciseGenerationSettingsEntity settings) =>
        new(
            settings.InitialDelayMinutes,
            settings.IntervalHours,
            settings.MinimumExerciseThreshold,
            settings.TargetExerciseCount,
            settings.MaxExercisesPerLessonPerRun,
            settings.GenerationBatchSize,
            DateTime.SpecifyKind(settings.UpdatedAtUtc, DateTimeKind.Utc),
            settings.UpdatedByUserId,
            settings.Version);
}
