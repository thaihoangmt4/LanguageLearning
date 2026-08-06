using LanguageLearning.Common.Entities.ExerciseEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class LessonAttemptExerciseConfiguration : IEntityTypeConfiguration<LessonAttemptExercise>
{
    public void Configure(EntityTypeBuilder<LessonAttemptExercise> builder)
    {
        builder.ToTable("lesson_attempt_exercises", table =>
        {
            table.HasCheckConstraint("CK_lesson_attempt_exercises_DisplayOrder", "\"DisplayOrder\" > 0");
            table.HasCheckConstraint("CK_lesson_attempt_exercises_ExerciseVersion", "\"ExerciseVersion\" >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActivityType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.LessonAttempt).WithMany(x => x.Activities)
            .HasForeignKey(x => x.LessonAttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Exercise).WithMany(x => x.LessonAttemptExercises)
            .HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceLesson).WithMany()
            .HasForeignKey(x => x.SourceLessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UserExerciseMistake).WithMany(x => x.ReviewActivities)
            .HasForeignKey(x => x.UserExerciseMistakeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LessonAttemptId, x.DisplayOrder }).IsUnique();
        builder.HasIndex(x => x.ExerciseId);
        builder.HasIndex(x => x.SourceLessonId);
        builder.HasIndex(x => x.UserExerciseMistakeId);
    }
}
