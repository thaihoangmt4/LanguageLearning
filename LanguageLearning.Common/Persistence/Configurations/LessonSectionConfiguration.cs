using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

/// <summary>
/// Entity type configuration for <see cref="LessonSection"/>.
/// </summary>
public sealed class LessonSectionConfiguration : IEntityTypeConfiguration<LessonSection>
{
    public void Configure(EntityTypeBuilder<LessonSection> builder)
    {
        builder.ToTable("lesson_sections", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_lesson_sections_DisplayOrder",
                "\"DisplayOrder\" >= 0"));

        builder.HasKey(section => section.Id);

        builder.Property(section => section.SectionType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(section => section.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(section => section.DisplayOrder)
            .IsRequired();

        builder.Property(section => section.IsRequired)
            .IsRequired();

        builder.Property(section => section.CreatedAt)
            .IsRequired();

        builder.Property(section => section.UpdatedAt)
            .IsRequired(false);

        builder.HasOne(section => section.Lesson)
            .WithMany(lesson => lesson.LessonSections)
            .HasForeignKey(section => section.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(section => new { section.LessonId, section.DisplayOrder })
            .IsUnique();
    }
}
