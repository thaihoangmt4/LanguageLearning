using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Entities.ExerciseGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class ExerciseGenerationSettingsConfiguration
    : IEntityTypeConfiguration<ExerciseGenerationSettings>
{
    private static readonly DateTime InitialUpdatedAtUtc =
        new(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ExerciseGenerationSettings> builder)
    {
        builder.ToTable("exercise_generation_settings", table =>
        {
            table.HasCheckConstraint(
                "CK_exercise_generation_settings_Singleton",
                $"\"Id\" = '{ExerciseGenerationSettings.SingletonId}'::uuid");
            table.HasCheckConstraint(
                "CK_exercise_generation_settings_InitialDelayMinutes",
                $"\"InitialDelayMinutes\" BETWEEN 0 AND {ExerciseGenerationOptions.MaximumInitialDelayMinutes}");
            table.HasCheckConstraint(
                "CK_exercise_generation_settings_IntervalHours",
                $"\"IntervalHours\" BETWEEN 1 AND {ExerciseGenerationOptions.MaximumIntervalHours}");
            table.HasCheckConstraint(
                "CK_exercise_generation_settings_ExerciseCounts",
                $"\"MinimumExerciseThreshold\" BETWEEN 0 AND {ExerciseGenerationOptions.MaximumExerciseCount} AND " +
                $"\"TargetExerciseCount\" BETWEEN \"MinimumExerciseThreshold\" AND {ExerciseGenerationOptions.MaximumExerciseCount}");
            table.HasCheckConstraint(
                "CK_exercise_generation_settings_MaxPerRun",
                $"\"MaxExercisesPerLessonPerRun\" BETWEEN 1 AND {ExerciseGenerationOptions.MaximumExercisesPerLessonPerRun}");
        });

        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Version).IsRequired().IsConcurrencyToken();
        builder.Property(settings => settings.UpdatedAtUtc).IsRequired();
        builder.HasOne(settings => settings.UpdatedByUser)
            .WithMany()
            .HasForeignKey(settings => settings.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new
        {
            Id = ExerciseGenerationSettings.SingletonId,
            InitialDelayMinutes = ExerciseGenerationOptions.DefaultInitialDelayMinutes,
            IntervalHours = ExerciseGenerationOptions.DefaultIntervalHours,
            MinimumExerciseThreshold = ExerciseGenerationOptions.DefaultMinimumExerciseThreshold,
            TargetExerciseCount = ExerciseGenerationOptions.DefaultTargetExerciseCount,
            MaxExercisesPerLessonPerRun = ExerciseGenerationOptions.DefaultMaxExercisesPerLessonPerRun,
            UpdatedAtUtc = InitialUpdatedAtUtc,
            UpdatedByUserId = (Guid?)null,
            Version = ExerciseGenerationSettings.InitialVersion
        });
    }
}