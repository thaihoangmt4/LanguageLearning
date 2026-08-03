using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

/// <summary>
/// Entity type configuration for <see cref="Unit"/>.
/// </summary>
public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("units", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_units_DisplayOrder",
                "\"DisplayOrder\" >= 0"));

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(unit => unit.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(unit => unit.Description)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(unit => unit.DisplayOrder)
            .IsRequired();

        builder.Property(unit => unit.CreatedAt)
            .IsRequired();

        builder.Property(unit => unit.UpdatedAt)
            .IsRequired(false);

        builder.HasOne(unit => unit.Course)
            .WithMany(course => course.Units)
            .HasForeignKey(unit => unit.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(unit => new { unit.CourseId, unit.Code })
            .IsUnique();

        builder.HasIndex(unit => new { unit.CourseId, unit.DisplayOrder })
            .IsUnique();

        builder.Navigation(unit => unit.Lessons)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
