namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public sealed record ExerciseGenerationSettingsSnapshot(
    int InitialDelayMinutes,
    int IntervalHours,
    int MinimumExerciseThreshold,
    int TargetExerciseCount,
    int MaxExercisesPerLessonPerRun,
    int GenerationBatchSize,
    DateTime UpdatedAtUtc,
    Guid? UpdatedByUserId,
    Guid Version);

public static class ExerciseGenerationSettingsErrors
{
    public const string NotFound = "exercise_generation.settings_not_found";
    public const string ConcurrencyConflict = "exercise_generation.settings_concurrency_conflict";
    public const string CurrentUserUnavailable = "exercise_generation.current_user_unavailable";
}
