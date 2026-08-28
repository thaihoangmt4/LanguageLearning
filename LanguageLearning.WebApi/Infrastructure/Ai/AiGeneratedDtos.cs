using System.Text.Json.Serialization;

namespace LanguageLearning.WebApi.Infrastructure.Ai;

internal sealed record AiGeneratedBatch(
    [property: JsonPropertyName("exercises")] IReadOnlyList<AiGeneratedExercise>? Exercises);

internal sealed record AiGeneratedExercise(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("question")] string? Question,
    [property: JsonPropertyName("options")] IReadOnlyList<string>? Options,
    [property: JsonPropertyName("correctAnswer")] string? CorrectAnswer,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("pronunciationText")] string? PronunciationText,
    [property: JsonPropertyName("imageMatches")] IReadOnlyList<AiGeneratedImageMatch>? ImageMatches,
    [property: JsonPropertyName("orderedSegments")] IReadOnlyList<string>? OrderedSegments,
    [property: JsonPropertyName("categories")] IReadOnlyList<AiGeneratedCategory>? Categories,
    [property: JsonPropertyName("referenceText")] string? ReferenceText);

internal sealed record AiGeneratedImageMatch(
    [property: JsonPropertyName("imageMediaId")] Guid ImageMediaId,
    [property: JsonPropertyName("target")] string? Target);

internal sealed record AiGeneratedCategory(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("items")] IReadOnlyList<string>? Items);
