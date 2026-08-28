using System.ClientModel;
using System.Text.Json;
using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using Microsoft.Extensions.AI;

namespace LanguageLearning.WebApi.Infrastructure.Ai;

public sealed class AiExerciseGenerator(
    IChatClient chatClient,
    ExerciseGenerationPromptBuilder promptBuilder,
    AiOptions options,
    ILogger<AiExerciseGenerator> logger) : IExerciseGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GeneratedExerciseBatch> GenerateAsync(
        ExerciseGenerationContext context,
        CancellationToken cancellationToken)
    {
        if (options.ConfigurationError() is { } configurationKey)
        {
            logger.LogWarning(
                "AI configuration is missing or invalid. Exercise generation skipped. ConfigurationKey: {ConfigurationKey}",
                configurationKey);
            throw new ExerciseGenerationException(
                "AI configuration is missing or invalid. Exercise generation skipped.");
        }

        var prompt = promptBuilder.Build(context);
        logger.LogInformation(
            "AI exercise generation started. Model: {Model}, LessonId: {LessonId}, RequestedCount: {RequestedCount}",
            options.Model,
            context.LessonId,
            context.RequestedCount);

        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, prompt.SystemPrompt),
                    new ChatMessage(ChatRole.User, prompt.UserPrompt)
                ],
                new ChatOptions
                {
                    ModelId = options.Model,
                    MaxOutputTokens = options.MaxOutputTokens,
                    ResponseFormat = ChatResponseFormat.Json
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ClientResultException exception)
        {
            logger.LogError(
                exception,
                "AI request failed. Model: {Model}, StatusCode: {StatusCode}",
                options.Model,
                exception.Status);
            throw new ExerciseGenerationException(
                $"AI request failed with HTTP {exception.Status}.",
                exception);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "AI request failed. Model: {Model}", options.Model);
            throw new ExerciseGenerationException(
                "AI request failed after the configured resilience policy completed.",
                exception);
        }

        var content = response.Text;
        if (response.FinishReason == ChatFinishReason.Length)
        {
            LogFailure(context, content.Length, "OutputTokenLimitReached");
            throw new ExerciseGenerationException(
                "AI output was truncated because the maximum output token limit was reached.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            LogFailure(context, 0, "EmptyContent");
            throw new ExerciseGenerationException("AI returned an empty completion message.");
        }

        AiGeneratedBatch generatedBatch;
        try
        {
            generatedBatch = JsonSerializer.Deserialize<AiGeneratedBatch>(content, JsonOptions)
                ?? throw new ExerciseGenerationException("AI returned an empty generated exercise payload.");
        }
        catch (JsonException exception)
        {
            LogFailure(context, content.Length, "MalformedJson");
            throw new ExerciseGenerationException("AI returned malformed generated exercise JSON.", exception);
        }

        if (generatedBatch.Exercises is not { Count: > 0 })
        {
            LogFailure(context, content.Length, "NoExercises");
            throw new ExerciseGenerationException("AI returned no generated exercises.");
        }

        var result = new GeneratedExerciseBatch(generatedBatch.Exercises.Select(Map).ToArray());
        logger.LogInformation(
            "AI exercise generation completed. Model: {Model}, LessonId: {LessonId}, GeneratedCount: {GeneratedCount}",
            options.Model,
            context.LessonId,
            result.Exercises.Count);
        return result;
    }

    private static GeneratedExercise Map(AiGeneratedExercise exercise)
    {
        var type = Enum.TryParse<ExerciseType>(exercise.Type, true, out var parsed)
            ? parsed
            : (ExerciseType)0;

        return new GeneratedExercise(
            type,
            exercise.Question ?? string.Empty,
            exercise.Options ?? [],
            exercise.CorrectAnswer,
            exercise.Explanation,
            exercise.PronunciationText,
            exercise.ImageMatches?.Select(match => new GeneratedImageMatch(
                match.ImageMediaId,
                match.Target ?? string.Empty)).ToArray(),
            exercise.OrderedSegments,
            exercise.Categories?.Select(category => new GeneratedCategory(
                category.Name ?? string.Empty,
                category.Items ?? [])).ToArray(),
            exercise.ReferenceText);
    }

    private void LogFailure(
        ExerciseGenerationContext context,
        int responseLength,
        string failureReason) => logger.LogWarning(
        "AI exercise generation response failed for LessonId {LessonId}, RequestedCount {RequestedCount}, SupportedExerciseTypes {SupportedExerciseTypes}, ResponseLength {ResponseLength}, MaxOutputTokens {MaxOutputTokens}, FailureReason {FailureReason}",
        context.LessonId,
        context.RequestedCount,
        string.Join(',', context.SupportedExerciseTypes),
        responseLength,
        options.MaxOutputTokens,
        failureReason);
}
