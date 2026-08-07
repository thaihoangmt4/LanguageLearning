using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Polly.Timeout;

namespace LanguageLearning.WebApi.Infrastructure.DeepSeek;

public sealed class DeepSeekClient(
    HttpClient httpClient,
    DeepSeekOptions options,
    ILogger<DeepSeekClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumErrorLength = 2048;

    internal async Task<DeepSeekChatResponse> CompleteAsync(
        DeepSeekChatRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or OperationCanceledException)
        {
            throw new DeepSeekGenerationException("DeepSeek could not be reached after the configured resilience policy completed.", exception);
        }

        using (response)
        {

            logger.LogInformation(
                "AI provider request completed for Provider {Provider}, Model {Model}, StatusCode {StatusCode}, RequestDurationMs {RequestDurationMs}",
                "DeepSeek", options.Model, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
                throw await CreateHttpExceptionAsync(response, cancellationToken);

            try
            {
                var value = await response.Content.ReadFromJsonAsync<DeepSeekChatResponse>(
                    JsonOptions, cancellationToken);
                return value ?? throw new DeepSeekGenerationException("DeepSeek returned an empty response body.");
            }
            catch (JsonException exception)
            {
                throw new DeepSeekGenerationException("DeepSeek returned an invalid response envelope.", exception);
            }
        }
    }

    private static async Task<DeepSeekGenerationException> CreateHttpExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        if (details.Length > MaximumErrorLength) details = details[..MaximumErrorLength];
        var status = (int)response.StatusCode;

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new DeepSeekGenerationException(
                $"DeepSeek authentication failed with HTTP {status}. Verify DeepSeek configuration. Provider detail: {details}");
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || status >= 500)
            return new DeepSeekGenerationException(
                $"DeepSeek is temporarily unavailable after retries (HTTP {status}). Provider detail: {details}");
        return new DeepSeekGenerationException(
            $"DeepSeek rejected the request with HTTP {status}. Provider detail: {details}");
    }
}
