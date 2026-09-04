using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public sealed record ExerciseGenerationImageAsset(
    Guid ImageMediaId,
    string AltText,
    string Word,
    string Meaning);

public sealed record GeneratedExercise(
    ExerciseType Type,
    string Question,
    IReadOnlyList<string> Options,
    string? CorrectAnswer,
    string? Explanation,
    string? PronunciationText = null,
    IReadOnlyList<GeneratedImageMatch>? ImageMatches = null,
    IReadOnlyList<string>? OrderedSegments = null,
    IReadOnlyList<GeneratedCategory>? Categories = null,
    string? ReferenceText = null);

public sealed record GeneratedImageMatch(Guid ImageMediaId, string Target);
public sealed record GeneratedCategory(string Name, IReadOnlyList<string> Items);

public class ExerciseGenerationException : Exception
{
    public ExerciseGenerationException(string message) : base(message) { }
    public ExerciseGenerationException(string message, Exception innerException) : base(message, innerException) { }
}
