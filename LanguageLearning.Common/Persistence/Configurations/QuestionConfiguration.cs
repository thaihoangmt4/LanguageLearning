using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.QuestionType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(question => question.Prompt).IsRequired().HasMaxLength(1000);
        builder.Property(question => question.PromptImageUrl).IsRequired(false).HasMaxLength(2048);
        builder.Property(question => question.PromptAudioUrl).IsRequired(false).HasMaxLength(2048);
        builder.Property(question => question.Explanation).IsRequired(false).HasMaxLength(2000);
        builder.Property(question => question.TextAnswer).IsRequired(false).HasMaxLength(500);
        builder.Property(question => question.IsCaseSensitive).IsRequired();
        builder.Property(question => question.CreatedAt).IsRequired();
        builder.Property(question => question.UpdatedAt).IsRequired(false);
        builder.HasOne(question => question.LearningStep).WithOne(step => step.Question)
            .HasForeignKey<Question>(question => question.LearningStepId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(question => question.TargetVocabulary).WithMany()
            .HasForeignKey(question => question.TargetVocabularyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(question => question.LearningStepId).IsUnique();
        builder.HasIndex(question => question.TargetVocabularyId);
        builder.Navigation(question => question.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
