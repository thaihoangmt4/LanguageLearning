using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

/// <summary>
/// Entity type configuration for <see cref="Course"/>.
/// </summary>
public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_courses_DisplayOrder",
                "\"DisplayOrder\" >= 0"));

        builder.HasKey(course => course.Id);

        builder.Property(course => course.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(course => course.Code)
            .IsUnique();

        builder.Property(course => course.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(course => course.Description)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(course => course.CefrLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(2);

        builder.Property(course => course.DisplayOrder)
            .IsRequired();

        builder.Property(course => course.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(course => course.CreatedAt)
            .IsRequired();

        builder.Property(course => course.UpdatedAt)
            .IsRequired(false);

        builder.HasIndex(course => new { course.IsPublished, course.DisplayOrder });

        builder.Navigation(course => course.Units)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
