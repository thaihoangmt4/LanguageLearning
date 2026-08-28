using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Entities.ExerciseGeneration;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public sealed class ExerciseGenerationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ExerciseGenerationOptions bootstrapOptions,
    ILogger<ExerciseGenerationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var startupSettings = await GetSchedulingSettingsAsync(stoppingToken);
            var startupDelay = ExerciseGenerationSchedule.StartupDelay(startupSettings);
            if (startupDelay > TimeSpan.Zero)
                await Task.Delay(startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunSafelyAsync(stoppingToken);

                var latestSettings = await GetSchedulingSettingsAsync(stoppingToken);
                await Task.Delay(ExerciseGenerationSchedule.NextDelay(latestSettings), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
    }

    private async Task RunSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new GenerateExercisesCommand(IsScheduled: true), stoppingToken);

            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Exercise generation background execution succeeded with EligibleLessons {EligibleLessons}, ProcessedLessons {ProcessedLessons}, FailedLessons {FailedLessons}, AcceptedExercises {AcceptedExercises}, RejectedExercises {RejectedExercises}",
                    result.Value.EligibleLessons, result.Value.ProcessedLessons, result.Value.FailedLessons,
                    result.Value.AcceptedExercises, result.Value.RejectedExercises);
            }
            else
            {
                logger.LogWarning("Exercise generation background execution failed with Error {Error}", result.Error);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exercise generation background execution failed; the next scheduled run will continue");
        }
    }

    private async Task<ExerciseGenerationSettingsSnapshot> GetSchedulingSettingsAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await dbContext.ExerciseGenerationSettings
                .AsNoTracking()
                .Where(value => value.Id == ExerciseGenerationSettings.SingletonId)
                .Select(value => new ExerciseGenerationSettingsSnapshot(
                    value.InitialDelayMinutes,
                    value.IntervalHours,
                    value.MinimumExerciseThreshold,
                    value.TargetExerciseCount,
                    value.MaxExercisesPerLessonPerRun,
                    value.UpdatedAtUtc,
                    value.UpdatedByUserId,
                    value.Version))
                .SingleOrDefaultAsync(stoppingToken)
                ?? throw new InvalidOperationException(ExerciseGenerationSettingsErrors.NotFound);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Exercise generation scheduling settings could not be loaded; bootstrap scheduling defaults will be used for this boundary");
            return new(
                bootstrapOptions.InitialDelayMinutes,
                bootstrapOptions.IntervalHours,
                bootstrapOptions.MinimumExerciseThreshold,
                bootstrapOptions.TargetExerciseCount,
                bootstrapOptions.MaxExercisesPerLessonPerRun,
                DateTime.UnixEpoch,
                null,
                Guid.Empty);
        }
    }
}

public static class ExerciseGenerationSchedule
{
    public static TimeSpan StartupDelay(ExerciseGenerationSettingsSnapshot settings) =>
        TimeSpan.FromMinutes(settings.InitialDelayMinutes);

    public static TimeSpan NextDelay(ExerciseGenerationSettingsSnapshot settings) =>
        TimeSpan.FromHours(settings.IntervalHours);
}
