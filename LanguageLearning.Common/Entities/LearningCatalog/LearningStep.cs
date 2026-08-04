using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.LearningCatalog;

public sealed class LearningStep : BaseEntity, IAuditableEntity
{
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public LearningStepType StepType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; }
    public Guid? VocabularyId { get; set; }
    public Vocabulary? Vocabulary { get; set; }
    public string? InstructionTitle { get; set; }
    public string? InstructionText { get; set; }
    public Question? Question { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
