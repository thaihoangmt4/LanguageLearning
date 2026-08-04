using LanguageLearning.Common.Entities.LearningCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LanguageLearning.Common.Persistence.Configurations;

public sealed class VocabularyConfiguration : IEntityTypeConfiguration<Vocabulary>
{
    public void Configure(EntityTypeBuilder<Vocabulary> builder)
    {
        builder.ToTable("vocabularies");
        builder.HasKey(vocabulary => vocabulary.Id);
        builder.Property(vocabulary => vocabulary.Word).IsRequired().HasMaxLength(200);
        builder.Property(vocabulary => vocabulary.Meaning).IsRequired().HasMaxLength(500);
        builder.Property(vocabulary => vocabulary.Phonetic).IsRequired(false).HasMaxLength(200);
        builder.Property(vocabulary => vocabulary.PartOfSpeech).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(vocabulary => vocabulary.ExampleSentence).IsRequired(false).HasMaxLength(1000);
        builder.Property(vocabulary => vocabulary.ExampleTranslation).IsRequired(false).HasMaxLength(1000);
        builder.Property(vocabulary => vocabulary.ImageUrl).IsRequired(false).HasMaxLength(2048);
        builder.Property(vocabulary => vocabulary.AudioUrl).IsRequired(false).HasMaxLength(2048);
        builder.Property(vocabulary => vocabulary.DifficultyLevel).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(vocabulary => vocabulary.CreatedAt).IsRequired();
        builder.Property(vocabulary => vocabulary.UpdatedAt).IsRequired(false);
        builder.HasIndex(vocabulary => vocabulary.Word);
        builder.HasIndex(vocabulary => vocabulary.DifficultyLevel);
    }
}
