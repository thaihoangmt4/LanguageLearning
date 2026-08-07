using LanguageLearning.Common.Entities.ExerciseEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises", table =>
        {
            table.HasCheckConstraint("CK_exercises_DisplayOrder", "\"DisplayOrder\" > 0");
            table.HasCheckConstraint("CK_exercises_Version", "\"Version\" >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Instruction).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Difficulty).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ContentJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.ContentHash).IsRequired(false).HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.Version).IsRequired().HasDefaultValue(1);
        builder.Property(x => x.IsRequired).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);
        builder.HasOne(x => x.Lesson).WithMany(x => x.Exercises)
            .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LessonId, x.DisplayOrder }).IsUnique();
        builder.HasIndex(x => new { x.LessonId, x.ContentHash }).IsUnique();
    }
}
