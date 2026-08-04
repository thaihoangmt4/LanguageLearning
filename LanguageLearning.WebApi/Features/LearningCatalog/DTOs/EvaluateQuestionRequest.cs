namespace LanguageLearning.WebApi.Features.LearningCatalog.DTOs;

public sealed record EvaluateQuestionRequest
{
    public Guid? SelectedOptionId { get; init; }
    public string? TextAnswer { get; init; }
}

public sealed record EvaluateQuestionResponse
{
    public Guid QuestionId { get; init; }
    public bool IsCorrect { get; init; }
    public CorrectAnswerDto CorrectAnswer { get; init; } = new();
    public string? Explanation { get; init; }
}

public sealed record CorrectAnswerDto
{
    public Guid? OptionId { get; init; }
    public string? Text { get; init; }
}
