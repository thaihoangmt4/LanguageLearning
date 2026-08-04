using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable("question_options", tableBuilder =>
            tableBuilder.HasCheckConstraint("CK_question_options_DisplayOrder", "\"DisplayOrder\" > 0"));
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Text).IsRequired(false).HasMaxLength(1000);
        builder.Property(option => option.ImageUrl).IsRequired(false).HasMaxLength(2048);
        builder.Property(option => option.AudioUrl).IsRequired(false).HasMaxLength(2048);
        builder.Property(option => option.AccessibilityText).IsRequired(false).HasMaxLength(500);
        builder.Property(option => option.IsCorrect).IsRequired();
        builder.Property(option => option.DisplayOrder).IsRequired();
        builder.Property(option => option.CreatedAt).IsRequired();
        builder.Property(option => option.UpdatedAt).IsRequired(false);
        builder.HasOne(option => option.Question).WithMany(question => question.Options)
            .HasForeignKey(option => option.QuestionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(option => new { option.QuestionId, option.DisplayOrder }).IsUnique();
    }
}
