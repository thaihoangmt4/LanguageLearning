using LanguageLearning.Common.Enums;
using LanguageLearning.WebApi.Features.ExerciseGeneration;

namespace LanguageLearning.WebApi.Features.LessonGeneration;

public sealed record LessonGenerationContext(
    Guid UnitId,
    string CourseTitle,
    string UnitTitle,
    string? UnitObjective,
    DifficultyLevel Difficulty,
    IReadOnlyList<LessonGenerationVocabulary> Vocabulary,
    IReadOnlyList<ExistingLessonSummary> ExistingLessons,
    IReadOnlyList<ExerciseType> SupportedExerciseTypes,
    int RequiredExerciseCount,
    IReadOnlyList<ExerciseGenerationImageAsset> AvailableImages);

public sealed record LessonGenerationVocabulary(
    string Word,
    string Meaning,
    string? ExampleSentence);

public sealed record ExistingLessonSummary(
    string Title,
    string? Topic,
    string? LearningObjective);

public sealed record GeneratedLesson(
    string Title,
    string Topic,
    string LearningObjective,
    IReadOnlyList<GeneratedLessonExercise> Exercises);

public sealed record GeneratedLessonExercise(int Order, GeneratedExercise Exercise);

public interface ILessonGenerator
{
    Task<GeneratedLesson> GenerateAsync(
        LessonGenerationContext context,
        CancellationToken cancellationToken);
}

public sealed record GenerateLessonResponse(Guid LessonId, string Title, int Order);


public static class LessonGenerationErrors
{
    public const string Disabled = "lesson_generation.disabled";
    public const string UnitNotFound = "lesson_generation.unit_not_found";
    public const string InvalidAiContent = "lesson_generation.invalid_ai_content";
    public const string ProviderFailure = "lesson_generation.provider_failure";
    public const string PersistenceFailure = "lesson_generation.persistence_failure";
}
