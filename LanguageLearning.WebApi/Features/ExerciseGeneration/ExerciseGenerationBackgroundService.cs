using LanguageLearning.Common.Configuration;
using LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;
using MediatR;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public sealed class ExerciseGenerationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ExerciseGenerationOptions options,
    ILogger<ExerciseGenerationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.InitialDelayMinutes > 0)
            await Task.Delay(TimeSpan.FromMinutes(options.InitialDelayMinutes), stoppingToken);

        await RunSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.IntervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunSafelyAsync(stoppingToken);
    }

    private async Task RunSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new GenerateExercisesCommand(), stoppingToken);

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
}
