using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class ExerciseAttemptConfiguration : IEntityTypeConfiguration<ExerciseAttempt>
{
    public void Configure(EntityTypeBuilder<ExerciseAttempt> builder)
    {
        builder.ToTable("exercise_attempts", table =>
        {
            table.HasCheckConstraint("CK_exercise_attempts_ExerciseVersion", "\"ExerciseVersion\" >= 1");
            table.HasCheckConstraint("CK_exercise_attempts_AttemptNumber", "\"AttemptNumber\" > 0");
            table.HasCheckConstraint("CK_exercise_attempts_Score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 100)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AnswerJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.ResultJson).IsRequired(false).HasColumnType("jsonb");
        builder.Property(x => x.EvaluationStatus).IsRequired().HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(EvaluationStatus.NotEvaluated).HasSentinel((EvaluationStatus)0);
        builder.Property(x => x.Score).HasPrecision(5, 2);
        builder.Property(x => x.Feedback).HasMaxLength(4000);
        builder.Property(x => x.SubmittedAt).IsRequired();
        builder.HasOne(x => x.LessonAttemptExercise).WithMany(x => x.ExerciseAttempts)
            .HasForeignKey(x => x.LessonAttemptExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LessonAttemptExerciseId, x.AttemptNumber }).IsUnique();
        builder.HasIndex(x => x.SubmissionId).IsUnique();
    }
}
