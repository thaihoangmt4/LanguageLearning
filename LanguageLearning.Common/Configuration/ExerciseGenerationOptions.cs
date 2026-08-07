namespace LanguageLearning.Common.Configuration;

public sealed class ExerciseGenerationOptions
{
    public const string SectionName = "ExerciseGeneration";

    public int InitialDelayMinutes { get; init; } = 10;
    public int IntervalHours { get; init; } = 24;
    public int MinimumExerciseThreshold { get; init; } = 20;
    public int TargetExerciseCount { get; init; } = 40;
    public int MaxExercisesPerLessonPerRun { get; init; } = 50;
    public int GenerationBatchSize { get; init; } = 20;

    public void Validate()
    {
        if (InitialDelayMinutes < 0) throw Invalid(nameof(InitialDelayMinutes));
        if (IntervalHours <= 0) throw Invalid(nameof(IntervalHours));
        if (MinimumExerciseThreshold < 0) throw Invalid(nameof(MinimumExerciseThreshold));
        if (TargetExerciseCount <= MinimumExerciseThreshold)
            throw new InvalidOperationException($"{SectionName}:{nameof(TargetExerciseCount)} must be greater than {nameof(MinimumExerciseThreshold)}.");
        if (MaxExercisesPerLessonPerRun <= 0) throw Invalid(nameof(MaxExercisesPerLessonPerRun));
        if (GenerationBatchSize <= 0) throw Invalid(nameof(GenerationBatchSize));
    }

    private static InvalidOperationException Invalid(string name) =>
        new($"{SectionName}:{name} must be greater than zero (except InitialDelayMinutes and MinimumExerciseThreshold, which may be zero).");
}
