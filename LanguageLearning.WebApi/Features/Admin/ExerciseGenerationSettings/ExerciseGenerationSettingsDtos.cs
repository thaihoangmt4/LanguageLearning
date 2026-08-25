namespace LanguageLearning.WebApi.Features.Admin.ExerciseGenerationSettings;

public sealed record ExerciseGenerationSettingsResponse(
    int InitialDelayMinutes,
    int IntervalHours,
    int MinimumExerciseThreshold,
    int TargetExerciseCount,
    int MaxExercisesPerLessonPerRun,
    int GenerationBatchSize,
    DateTime UpdatedAtUtc,
    Guid? UpdatedByUserId,
    Guid Version);

public sealed record UpdateExerciseGenerationSettingsRequest(
    int InitialDelayMinutes,
    int IntervalHours,
    int MinimumExerciseThreshold,
    int TargetExerciseCount,
    int MaxExercisesPerLessonPerRun,
    int GenerationBatchSize,
    Guid Version);
