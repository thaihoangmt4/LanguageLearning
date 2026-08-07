using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class LessonAttemptConfiguration : IEntityTypeConfiguration<LessonAttempt>
{
    public void Configure(EntityTypeBuilder<LessonAttempt> builder)
    {
        builder.ToTable("lesson_attempts", table =>
        {
            table.HasCheckConstraint("CK_lesson_attempts_TotalScore", "\"TotalScore\" >= 0 AND \"TotalScore\" <= 100");
            table.HasCheckConstraint("CK_lesson_attempts_Counts", "\"CorrectCount\" >= 0 AND \"IncorrectCount\" >= 0 AND \"CompletedActivityCount\" >= 0 AND \"TotalActivityCount\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(LessonAttemptStatus.InProgress).HasSentinel((LessonAttemptStatus)0);
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.TotalScore).HasPrecision(5, 2).IsRequired();
        builder.HasOne(x => x.User).WithMany(x => x.LessonAttempts)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Lesson).WithMany()
            .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.Status });
        builder.HasIndex(x => new { x.UserId, x.LessonId })
            .IsUnique()
            .HasFilter("\"Status\" = 'InProgress'")
            .HasDatabaseName("IX_lesson_attempts_UserId_LessonId_InProgress");
        builder.HasIndex(x => x.LessonId);
    }
}
