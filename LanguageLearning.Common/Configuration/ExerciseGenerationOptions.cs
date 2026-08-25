namespace LanguageLearning.Common.Configuration;

/// <summary>
/// Defines validated bootstrap defaults. The singleton PostgreSQL settings row is the runtime
/// source of truth for generation and scheduling once the database is available.
/// </summary>
public sealed class ExerciseGenerationOptions
{
    public const string SectionName = "ExerciseGeneration";

    public const int DefaultInitialDelayMinutes = 10;
    public const int DefaultIntervalHours = 24;
    public const int DefaultMinimumExerciseThreshold = 20;
    public const int DefaultTargetExerciseCount = 40;
    public const int DefaultMaxExercisesPerLessonPerRun = 50;
    public const int DefaultGenerationBatchSize = 20;

    public const int MaximumInitialDelayMinutes = 1_440;
    public const int MaximumIntervalHours = 168;
    public const int MaximumExerciseCount = 500;
    public const int MaximumExercisesPerLessonPerRun = 200;
    public const int MaximumGenerationBatchSize = 50;

    public int InitialDelayMinutes { get; init; } = DefaultInitialDelayMinutes;
    public int IntervalHours { get; init; } = DefaultIntervalHours;
    public int MinimumExerciseThreshold { get; init; } = DefaultMinimumExerciseThreshold;
    public int TargetExerciseCount { get; init; } = DefaultTargetExerciseCount;
    public int MaxExercisesPerLessonPerRun { get; init; } = DefaultMaxExercisesPerLessonPerRun;
    public int GenerationBatchSize { get; init; } = DefaultGenerationBatchSize;

    public void Validate()
    {
        var violations = ValidateValues(
            InitialDelayMinutes,
            IntervalHours,
            MinimumExerciseThreshold,
            TargetExerciseCount,
            MaxExercisesPerLessonPerRun,
            GenerationBatchSize);
        if (violations.Count > 0)
            throw new InvalidOperationException(
                $"{SectionName}:{violations[0].PropertyName} {violations[0].Message}");
    }

    public static IReadOnlyList<ExerciseGenerationSettingViolation> ValidateValues(
        int initialDelayMinutes,
        int intervalHours,
        int minimumExerciseThreshold,
        int targetExerciseCount,
        int maxExercisesPerLessonPerRun,
        int generationBatchSize)
    {
        var violations = new List<ExerciseGenerationSettingViolation>();
        AddRangeViolation(violations, nameof(InitialDelayMinutes), initialDelayMinutes, 0, MaximumInitialDelayMinutes);
        AddRangeViolation(violations, nameof(IntervalHours), intervalHours, 1, MaximumIntervalHours);
        AddRangeViolation(violations, nameof(MinimumExerciseThreshold), minimumExerciseThreshold, 0, MaximumExerciseCount);
        AddRangeViolation(violations, nameof(TargetExerciseCount), targetExerciseCount, 0, MaximumExerciseCount);
        AddRangeViolation(violations, nameof(MaxExercisesPerLessonPerRun), maxExercisesPerLessonPerRun, 1, MaximumExercisesPerLessonPerRun);
        AddRangeViolation(violations, nameof(GenerationBatchSize), generationBatchSize, 1, MaximumGenerationBatchSize);

        if (targetExerciseCount < minimumExerciseThreshold)
        {
            violations.Add(new(
                nameof(TargetExerciseCount),
                $"must be greater than or equal to {nameof(MinimumExerciseThreshold)}."));
        }

        return violations;
    }

    private static void AddRangeViolation(
        ICollection<ExerciseGenerationSettingViolation> violations,
        string propertyName,
        int value,
        int minimum,
        int maximum)
    {
        if (value < minimum || value > maximum)
            violations.Add(new(propertyName, $"must be between {minimum} and {maximum}."));
    }
}

public sealed record ExerciseGenerationSettingViolation(string PropertyName, string Message);
