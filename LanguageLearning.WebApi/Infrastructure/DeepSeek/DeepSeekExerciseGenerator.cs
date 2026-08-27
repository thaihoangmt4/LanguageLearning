using System.Text.Json;
using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.ExerciseGeneration;

namespace LanguageLearning.WebApi.Infrastructure.DeepSeek;

public sealed class DeepSeekExerciseGenerator(
    DeepSeekClient client,
    ExerciseGenerationPromptBuilder promptBuilder,
    DeepSeekOptions options,
    ILogger<DeepSeekExerciseGenerator> logger) : IExerciseGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GeneratedExerciseBatch> GenerateAsync(
        ExerciseGenerationContext context,
        CancellationToken cancellationToken)
    {
        var prompt = promptBuilder.Build(context);
        var response = await client.CompleteAsync(new DeepSeekChatRequest(
            options.Model,
            [new("system", prompt.SystemPrompt), new("user", prompt.UserPrompt)],
            new("json_object"),
            options.MaxOutputTokens,
            false), cancellationToken);

        if (response.Choices is not { Count: > 0 })
        {
            LogFailure(context, 0, "NoChoices");
            throw new DeepSeekGenerationException("DeepSeek returned no completion choices.");
        }

        var choice = response.Choices[0];
        var content = choice.Message?.Content;
        if (string.Equals(choice.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
        {
            LogFailure(context, content?.Length ?? 0, "OutputTokenLimitReached");
            throw new DeepSeekGenerationException(
                "DeepSeek output was truncated because the maximum output token limit was reached.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            LogFailure(context, 0, "EmptyContent");
            throw new DeepSeekGenerationException("DeepSeek returned an empty completion message.");
        }

        DeepSeekGeneratedBatch providerBatch;
        try
        {
            providerBatch = JsonSerializer.Deserialize<DeepSeekGeneratedBatch>(content, JsonOptions)
                ?? throw new DeepSeekGenerationException("DeepSeek returned an empty generated exercise payload.");
        }
        catch (JsonException exception)
        {
            LogFailure(context, content.Length, "MalformedJson");
            throw new DeepSeekGenerationException("DeepSeek returned malformed generated exercise JSON.", exception);
        }

        if (providerBatch.Exercises is not { Count: > 0 })
        {
            LogFailure(context, content.Length, "NoExercises");
            throw new DeepSeekGenerationException("DeepSeek returned no generated exercises.");
        }

        return new GeneratedExerciseBatch(providerBatch.Exercises.Select(Map).ToArray());
    }

    private static GeneratedExercise Map(DeepSeekGeneratedExercise exercise)
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
        "DeepSeek exercise generation response failed for LessonId {LessonId}, RequestedCount {RequestedCount}, SupportedExerciseTypes {SupportedExerciseTypes}, ResponseLength {ResponseLength}, MaxOutputTokens {MaxOutputTokens}, FailureReason {FailureReason}",
        context.LessonId,
        context.RequestedCount,
        string.Join(',', context.SupportedExerciseTypes),
        responseLength,
        options.MaxOutputTokens,
        failureReason);
}
