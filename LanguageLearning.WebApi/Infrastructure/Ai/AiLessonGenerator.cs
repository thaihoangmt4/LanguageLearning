using System.ClientModel;
using System.Text.Json;
using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using LanguageLearning.WebApi.Features.LessonGeneration;
using Microsoft.Extensions.AI;

namespace LanguageLearning.WebApi.Infrastructure.Ai;

public sealed class AiLessonGenerator(
    IChatClient chatClient,
    LessonGenerationPromptBuilder promptBuilder,
    AiOptions options,
    ILogger<AiLessonGenerator> logger) : ILessonGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GeneratedLesson> GenerateAsync(
        LessonGenerationContext context,
        CancellationToken cancellationToken)
    {
        if (options.ConfigurationError() is { } key)
            throw new ExerciseGenerationException($"AI configuration is invalid: {key}.");

        var prompt = promptBuilder.Build(context);
        logger.LogInformation("AI lesson generation started. Model: {Model}, UnitId: {UnitId}", options.Model, context.UnitId);
        try
        {
            var response = await chatClient.GetResponseAsync(
                [new(ChatRole.System, prompt.SystemPrompt), new(ChatRole.User, prompt.UserPrompt)],
                new ChatOptions { ModelId = options.Model, MaxOutputTokens = options.MaxOutputTokens, ResponseFormat = ChatResponseFormat.Json },
                cancellationToken);
            if (response.FinishReason == ChatFinishReason.Length || string.IsNullOrWhiteSpace(response.Text))
                throw new ExerciseGenerationException("AI returned an incomplete lesson.");
            var payload = JsonSerializer.Deserialize<AiLesson>(response.Text, JsonOptions)
                ?? throw new ExerciseGenerationException("AI returned an empty lesson.");
            return new(
                payload.Title ?? string.Empty,
                payload.Topic ?? string.Empty,
                payload.LearningObjective ?? string.Empty,
                (payload.Exercises ?? []).Select(Map).ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ExerciseGenerationException) { throw; }
        catch (JsonException exception) { throw new ExerciseGenerationException("AI returned malformed lesson JSON.", exception); }
        catch (ClientResultException exception) { throw new ExerciseGenerationException($"AI request failed with HTTP {exception.Status}.", exception); }
        catch (Exception exception) { throw new ExerciseGenerationException("AI lesson request failed.", exception); }
    }

    private static GeneratedLessonExercise Map(AiExercise value) => new(value.Order, new(
        Enum.TryParse<ExerciseType>(value.Type, true, out var type) ? type : (ExerciseType)0,
        value.Question ?? string.Empty,
        value.Options ?? [],
        value.CorrectAnswer,
        value.Explanation,
        value.PronunciationText,
        value.ImageMatches?.Select(x => new GeneratedImageMatch(x.ImageMediaId, x.Target ?? string.Empty)).ToArray(),
        value.OrderedSegments,
        value.Categories?.Select(x => new GeneratedCategory(x.Name ?? string.Empty, x.Items ?? [])).ToArray(),
        value.ReferenceText));

    private sealed record AiLesson(string? Title, string? Topic, string? LearningObjective, List<AiExercise>? Exercises);
    private sealed record AiExercise(int Order, string? Type, string? Question, List<string>? Options,
        string? CorrectAnswer, string? Explanation, string? PronunciationText,
        List<AiImageMatch>? ImageMatches, List<string>? OrderedSegments,
        List<AiCategory>? Categories, string? ReferenceText);
    private sealed record AiImageMatch(Guid ImageMediaId, string? Target);
    private sealed record AiCategory(string? Name, List<string>? Items);
}
