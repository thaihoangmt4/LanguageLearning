using LanguageLearning.Common.Entities.ExerciseEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class UserLessonProgressConfiguration : IEntityTypeConfiguration<UserLessonProgress>
{
    public void Configure(EntityTypeBuilder<UserLessonProgress> builder)
    {
        builder.ToTable("user_lesson_progress");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CompletedAt).IsRequired();
        builder.HasOne(value => value.User).WithMany(value => value.LessonProgress)
            .HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.Lesson).WithMany()
            .HasForeignKey(value => value.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.UserId, value.LessonId }).IsUnique();
        builder.HasIndex(value => value.LessonId);
    }
}
