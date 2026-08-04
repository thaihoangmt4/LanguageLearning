namespace LanguageLearning.WebApi.Features.LearningCatalog.DTOs;

public sealed record LessonLearningFlowResponse
{
    public LessonLearningFlowDto Lesson { get; init; } = new();
    public IReadOnlyCollection<LearningStepDto> Steps { get; init; } = [];
}

public sealed record LessonLearningFlowDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string DifficultyLevel { get; init; } = string.Empty;
    public int EstimatedDurationMinutes { get; init; }
    public int TotalSteps { get; init; }
}

public sealed record LearningStepDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsRequired { get; init; }
    public InstructionStepDto? Instruction { get; init; }
    public QuestionStepDto? Question { get; init; }
}

public sealed record InstructionStepDto
{
    public string? Title { get; init; }
    public string? Text { get; init; }
    public VocabularyDto? Vocabulary { get; init; }
}

public sealed record VocabularyDto
{
    public Guid Id { get; init; }
    public string Word { get; init; } = string.Empty;
    public string Meaning { get; init; } = string.Empty;
    public string? Phonetic { get; init; }
    public string PartOfSpeech { get; init; } = string.Empty;
    public string? ExampleSentence { get; init; }
    public string? ExampleTranslation { get; init; }
    public string? ImageUrl { get; init; }
    public string? AudioUrl { get; init; }
}

public sealed record QuestionStepDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string? PromptImageUrl { get; init; }
    public string? PromptAudioUrl { get; init; }
    public IReadOnlyCollection<QuestionOptionDto> Options { get; init; } = [];
}

public sealed record QuestionOptionDto
{
    public Guid Id { get; init; }
    public string? Text { get; init; }
    public string? ImageUrl { get; init; }
    public string? AudioUrl { get; init; }
    public string? AccessibilityText { get; init; }
    public int DisplayOrder { get; init; }
}
