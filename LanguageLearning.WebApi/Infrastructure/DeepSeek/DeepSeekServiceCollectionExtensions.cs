using LanguageLearning.WebApi.Features.ExerciseGeneration;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace LanguageLearning.WebApi.Infrastructure.DeepSeek;

public static class DeepSeekServiceCollectionExtensions
{
    public static IServiceCollection AddDeepSeekExerciseGeneration(
        this IServiceCollection services,
        DeepSeekOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ExerciseGenerationPromptBuilder>();
        services.AddTransient<IExerciseGenerator, DeepSeekExerciseGenerator>();

        services.AddHttpClient<DeepSeekClient>(client =>
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"))
            .AddResilienceHandler("deepseek", pipeline =>
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(response => IsTransientStatus(response.StatusCode)),
                    DelayGenerator = arguments =>
                    {
                        var retryAfter = arguments.Outcome.Result?.Headers.RetryAfter;
                        var delay = retryAfter?.Delta;
                        if (delay is null && retryAfter?.Date is { } date)
                            delay = date - DateTimeOffset.UtcNow;
                        return new ValueTask<TimeSpan?>(delay > TimeSpan.Zero ? delay : null);
                    }
                });
                pipeline.AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds));
            });

        return services;
    }

    private static bool IsTransientStatus(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.RequestTimeout
            or System.Net.HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
