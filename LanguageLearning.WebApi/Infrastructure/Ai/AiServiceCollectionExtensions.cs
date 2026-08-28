using LanguageLearning.WebApi.Features.ExerciseGeneration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Http.Resilience;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.Timeout;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace LanguageLearning.WebApi.Infrastructure.Ai;

public static class AiServiceCollectionExtensions
{
    public const string HttpClientName = "ai-openai-compatible";

    public static IServiceCollection AddAiExerciseGeneration(
        this IServiceCollection services,
        AiOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ExerciseGenerationPromptBuilder>();
        services.AddTransient<IExerciseGenerator, AiExerciseGenerator>();

        services.AddHttpClient(HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddResilienceHandler("ai", pipeline =>
            {
                if (options.MaxRetryAttempts > 0)
                {
                    pipeline.AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = Math.Min(options.MaxRetryAttempts, 10),
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
                }
                pipeline.AddTimeout(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));
            });

        services.AddSingleton<IChatClient>(provider =>
        {
            var endpoint = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var configuredEndpoint) &&
                configuredEndpoint.Scheme is "http" or "https"
                ? configuredEndpoint
                : new Uri(AiOptions.DefaultBaseUrl);
            var model = string.IsNullOrWhiteSpace(options.Model) ? AiOptions.DefaultModel : options.Model;
            var apiKey = string.IsNullOrWhiteSpace(options.ApiKey) ? "not-configured" : options.ApiKey;
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = endpoint,
                RetryPolicy = new ClientRetryPolicy(0),
                NetworkTimeout = Timeout.InfiniteTimeSpan,
                Transport = new HttpClientPipelineTransport(httpClient)
            };
            return new ChatClient(model, new ApiKeyCredential(apiKey), clientOptions).AsIChatClient();
        });

        return services;
    }

    private static bool IsTransientStatus(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.RequestTimeout
            or System.Net.HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
