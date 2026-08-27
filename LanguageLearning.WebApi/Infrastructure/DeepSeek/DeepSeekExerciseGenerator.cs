using System.Text.Json;
using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.ExerciseGeneration;

namespace LanguageLearning.WebApi.Infrastructure.DeepSeek;

public sealed class DeepSeekExerciseGenerator(
    DeepSeekClient client,
    ExerciseGenerationPromptBuilder promptBuilder,
    DeepSeekOptions options) : IExerciseGenerator
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

        var content = response.Choices?.FirstOrDefault()?.Message?.Content;
        if (response.Choices is not { Count: > 0 })
            throw new DeepSeekGenerationException("DeepSeek returned no completion choices.");
        if (string.IsNullOrWhiteSpace(content))
            throw new DeepSeekGenerationException("DeepSeek returned an empty completion message.");

        DeepSeekGeneratedBatch providerBatch;
        try
        {
            providerBatch = JsonSerializer.Deserialize<DeepSeekGeneratedBatch>(content, JsonOptions)
                ?? throw new DeepSeekGenerationException("DeepSeek returned an empty generated exercise payload.");
        }
        catch (JsonException exception)
        {
            throw new DeepSeekGenerationException("DeepSeek returned malformed generated exercise JSON.", exception);
        }

        if (providerBatch.Exercises is not { Count: > 0 })
            throw new DeepSeekGenerationException("DeepSeek returned no generated exercises.");

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
}
