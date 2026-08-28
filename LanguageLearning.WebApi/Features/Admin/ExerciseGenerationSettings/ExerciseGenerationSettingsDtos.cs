namespace LanguageLearning.WebApi.Features.Admin.ExerciseGenerationSettings;

public sealed record ExerciseGenerationSettingsResponse(
    bool Enabled,
    int InitialDelayMinutes,
    int IntervalHours,
    int MinimumExerciseThreshold,
    int TargetExerciseCount,
    int MaxExercisesPerLessonPerRun,
    DateTime UpdatedAtUtc,
    Guid? UpdatedByUserId,
    Guid Version);

public sealed record UpdateExerciseGenerationSettingsRequest(
    int InitialDelayMinutes,
    int IntervalHours,
    int MinimumExerciseThreshold,
    int TargetExerciseCount,
    int MaxExercisesPerLessonPerRun,
    Guid Version,
    bool Enabled = true);
