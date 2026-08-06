using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class UserExerciseMistakeConfiguration : IEntityTypeConfiguration<UserExerciseMistake>
{
    public void Configure(EntityTypeBuilder<UserExerciseMistake> builder)
    {
        builder.ToTable("user_exercise_mistakes", table =>
        {
            table.HasCheckConstraint("CK_user_exercise_mistakes_ExerciseVersion", "\"ExerciseVersion\" >= 1");
            table.HasCheckConstraint("CK_user_exercise_mistakes_Counts", "\"FailureCount\" > 0 AND \"SuccessfulReviewCount\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(UserExerciseMistakeStatus.Pending).HasSentinel((UserExerciseMistakeStatus)0);
        builder.HasOne(x => x.User).WithMany(x => x.ExerciseMistakes)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Exercise).WithMany(x => x.UserExerciseMistakes)
            .HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.ExerciseId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.LastFailedAt });
        builder.HasIndex(x => x.ExerciseId);
    }
}
