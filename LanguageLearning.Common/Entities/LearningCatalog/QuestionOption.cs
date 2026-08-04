using LanguageLearning.Common.Entities.Base;

namespace LanguageLearning.Common.Entities.LearningCatalog;

public sealed class QuestionOption : BaseEntity, IAuditableEntity
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string? Text { get; set; }
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public string? AccessibilityText { get; set; }
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
