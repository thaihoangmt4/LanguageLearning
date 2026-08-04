using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.LearningCatalog;

public sealed class Question : BaseEntity, IAuditableEntity
{
    private readonly List<QuestionOption> _options = [];

    public Guid LearningStepId { get; set; }
    public LearningStep LearningStep { get; set; } = null!;
    public QuestionType QuestionType { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? PromptImageUrl { get; set; }
    public string? PromptAudioUrl { get; set; }
    public string? Explanation { get; set; }
    public Guid? TargetVocabularyId { get; set; }
    public Vocabulary? TargetVocabulary { get; set; }
    public string? TextAnswer { get; set; }
    public bool IsCaseSensitive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyCollection<QuestionOption> Options => _options.AsReadOnly();
}
