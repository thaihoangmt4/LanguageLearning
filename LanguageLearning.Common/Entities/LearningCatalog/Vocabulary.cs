using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.LearningCatalog;

public sealed class Vocabulary : BaseEntity, IAuditableEntity
{
    public string Word { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public PartOfSpeech PartOfSpeech { get; set; }
    public string? ExampleSentence { get; set; }
    public string? ExampleTranslation { get; set; }
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
