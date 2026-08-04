using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class LearningStepConfiguration : IEntityTypeConfiguration<LearningStep>
{
    public void Configure(EntityTypeBuilder<LearningStep> builder)
    {
        builder.ToTable("learning_steps", tableBuilder =>
            tableBuilder.HasCheckConstraint("CK_learning_steps_DisplayOrder", "\"DisplayOrder\" > 0"));
        builder.HasKey(step => step.Id);
        builder.Property(step => step.StepType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(step => step.DisplayOrder).IsRequired();
        builder.Property(step => step.IsRequired).IsRequired();
        builder.Property(step => step.InstructionTitle).IsRequired(false).HasMaxLength(200);
        builder.Property(step => step.InstructionText).IsRequired(false).HasMaxLength(2000);
        builder.Property(step => step.CreatedAt).IsRequired();
        builder.Property(step => step.UpdatedAt).IsRequired(false);
        builder.HasOne(step => step.Lesson).WithMany(lesson => lesson.LearningSteps)
            .HasForeignKey(step => step.LessonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(step => step.Vocabulary).WithMany()
            .HasForeignKey(step => step.VocabularyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(step => new { step.LessonId, step.DisplayOrder }).IsUnique();
        builder.HasIndex(step => step.VocabularyId);
    }
}
