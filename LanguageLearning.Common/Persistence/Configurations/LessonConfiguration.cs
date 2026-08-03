using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

/// <summary>
/// Entity type configuration for <see cref="Lesson"/>.
/// </summary>
public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("lessons", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_lessons_DisplayOrder",
                "\"DisplayOrder\" >= 0");
            tableBuilder.HasCheckConstraint(
                "CK_lessons_EstimatedDurationMinutes",
                "\"EstimatedDurationMinutes\" > 0");
        });

        builder.HasKey(lesson => lesson.Id);

        builder.Property(lesson => lesson.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(lesson => lesson.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(lesson => lesson.Description)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(lesson => lesson.LearningObjectiveSummary)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(lesson => lesson.EstimatedDurationMinutes)
            .IsRequired();

        builder.Property(lesson => lesson.DifficultyLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(lesson => lesson.DisplayOrder)
            .IsRequired();

        builder.Property(lesson => lesson.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(lesson => lesson.CreatedAt)
            .IsRequired();

        builder.Property(lesson => lesson.UpdatedAt)
            .IsRequired(false);

        builder.HasOne(lesson => lesson.Unit)
            .WithMany(unit => unit.Lessons)
            .HasForeignKey(lesson => lesson.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(lesson => new { lesson.UnitId, lesson.Code })
            .IsUnique();

        builder.HasIndex(lesson => new { lesson.UnitId, lesson.DisplayOrder })
            .IsUnique();

        builder.HasIndex(lesson => new
        {
            lesson.UnitId,
            lesson.IsPublished,
            lesson.DisplayOrder
        });

        builder.Navigation(lesson => lesson.LessonSections)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
