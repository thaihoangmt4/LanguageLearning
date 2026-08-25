using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.Identity;

namespace LanguageLearning.Common.Entities.ExerciseGeneration;

public sealed class ExerciseGenerationSettings : BaseEntity
{
    public static readonly Guid SingletonId = Guid.Parse("e76d6ef3-df4c-4f42-88df-41114da06401");
    public static readonly Guid InitialVersion = Guid.Parse("6d332c99-0a93-4cc0-a400-24931e424240");

    public ExerciseGenerationSettings()
    {
        Id = SingletonId;
    }

    public int InitialDelayMinutes { get; private set; } = ExerciseGenerationOptions.DefaultInitialDelayMinutes;
    public int IntervalHours { get; private set; } = ExerciseGenerationOptions.DefaultIntervalHours;
    public int MinimumExerciseThreshold { get; private set; } = ExerciseGenerationOptions.DefaultMinimumExerciseThreshold;
    public int TargetExerciseCount { get; private set; } = ExerciseGenerationOptions.DefaultTargetExerciseCount;
    public int MaxExercisesPerLessonPerRun { get; private set; } = ExerciseGenerationOptions.DefaultMaxExercisesPerLessonPerRun;
    public int GenerationBatchSize { get; private set; } = ExerciseGenerationOptions.DefaultGenerationBatchSize;
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public User? UpdatedByUser { get; private set; }
    public Guid Version { get; private set; } = InitialVersion;

    public void Update(
        int initialDelayMinutes,
        int intervalHours,
        int minimumExerciseThreshold,
        int targetExerciseCount,
        int maxExercisesPerLessonPerRun,
        int generationBatchSize,
        DateTime updatedAtUtc,
        Guid updatedByUserId)
    {
        InitialDelayMinutes = initialDelayMinutes;
        IntervalHours = intervalHours;
        MinimumExerciseThreshold = minimumExerciseThreshold;
        TargetExerciseCount = targetExerciseCount;
        MaxExercisesPerLessonPerRun = maxExercisesPerLessonPerRun;
        GenerationBatchSize = generationBatchSize;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
        Version = Guid.NewGuid();
    }
}
