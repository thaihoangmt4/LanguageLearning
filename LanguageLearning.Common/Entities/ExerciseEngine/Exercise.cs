using LanguageLearning.Common.Entities.Base;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Enums;

namespace LanguageLearning.Common.Entities.ExerciseEngine;

public sealed class Exercise : BaseEntity, IAuditableEntity
{
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public ExerciseType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public DifficultyLevel Difficulty { get; set; }
    public int DisplayOrder { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string? ContentHash { get; set; }
    public int Version { get; set; } = 1;
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<LessonAttemptExercise> LessonAttemptExercises { get; set; } = [];
    public ICollection<UserExerciseMistake> UserExerciseMistakes { get; set; } = [];
}
