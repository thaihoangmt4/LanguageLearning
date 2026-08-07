using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public sealed record ExerciseGenerationContext(
    Guid LessonId,
    string LessonCode,
    string LessonTitle,
    string? LessonDescription,
    string? LearningObjective,
    DifficultyLevel Difficulty,
    IReadOnlyList<ExerciseType> SupportedExerciseTypes,
    int RequestedCount);

public sealed record GeneratedExerciseBatch(IReadOnlyList<GeneratedExercise> Exercises)
{
    public static GeneratedExerciseBatch Empty { get; } = new([]);
}

public sealed record GeneratedExercise(
    ExerciseType Type,
    string Question,
    IReadOnlyList<string> Options,
    string? CorrectAnswer,
    string? Explanation);

public interface IExerciseGenerator
{
    Task<GeneratedExerciseBatch> GenerateAsync(
        ExerciseGenerationContext context,
        CancellationToken cancellationToken);
}

public class ExerciseGenerationException : Exception
{
    public ExerciseGenerationException(string message) : base(message) { }
    public ExerciseGenerationException(string message, Exception innerException) : base(message, innerException) { }
}
