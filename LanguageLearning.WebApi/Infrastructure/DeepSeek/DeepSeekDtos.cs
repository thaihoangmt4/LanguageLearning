using System.Text.Json.Serialization;

namespace LanguageLearning.WebApi.Infrastructure.DeepSeek;

internal sealed record DeepSeekChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<DeepSeekMessage> Messages,
    [property: JsonPropertyName("response_format")] DeepSeekResponseFormat ResponseFormat,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("stream")] bool Stream);

internal sealed record DeepSeekMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record DeepSeekResponseFormat(
    [property: JsonPropertyName("type")] string Type);

internal sealed record DeepSeekChatResponse(
    [property: JsonPropertyName("choices")] IReadOnlyList<DeepSeekChoice>? Choices);

internal sealed record DeepSeekChoice(
    [property: JsonPropertyName("message")] DeepSeekResponseMessage? Message);

internal sealed record DeepSeekResponseMessage(
    [property: JsonPropertyName("content")] string? Content);

internal sealed record DeepSeekGeneratedBatch(
    [property: JsonPropertyName("exercises")] IReadOnlyList<DeepSeekGeneratedExercise>? Exercises);

internal sealed record DeepSeekGeneratedExercise(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("question")] string? Question,
    [property: JsonPropertyName("options")] IReadOnlyList<string>? Options,
    [property: JsonPropertyName("correctAnswer")] string? CorrectAnswer,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("pronunciationText")] string? PronunciationText,
    [property: JsonPropertyName("imageMatches")] IReadOnlyList<DeepSeekGeneratedImageMatch>? ImageMatches,
    [property: JsonPropertyName("orderedSegments")] IReadOnlyList<string>? OrderedSegments,
    [property: JsonPropertyName("categories")] IReadOnlyList<DeepSeekGeneratedCategory>? Categories,
    [property: JsonPropertyName("referenceText")] string? ReferenceText);

internal sealed record DeepSeekGeneratedImageMatch(
    [property: JsonPropertyName("imageMediaId")] Guid ImageMediaId,
    [property: JsonPropertyName("target")] string? Target);

internal sealed record DeepSeekGeneratedCategory(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("items")] IReadOnlyList<string>? Items);
