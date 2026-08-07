namespace LanguageLearning.WebApi.Features.ExerciseGeneration;

public static class ExerciseGenerationPolicy
{
    public static int RequiredCount(
        int currentExerciseCount,
        int minimumExerciseThreshold,
        int targetExerciseCount,
        int maxExercisesPerLessonPerRun)
    {
        if (currentExerciseCount >= minimumExerciseThreshold)
            return 0;

        return Math.Min(
            Math.Max(0, targetExerciseCount - currentExerciseCount),
            maxExercisesPerLessonPerRun);
    }
}
