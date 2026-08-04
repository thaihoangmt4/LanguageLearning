using FluentValidation;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LearningCatalog.Commands;

public sealed class EvaluateQuestionCommand : IRequest<Result<EvaluateQuestionResponse>>
{
    public Guid LessonId { get; init; }
    public Guid QuestionId { get; init; }
    public Guid? SelectedOptionId { get; init; }
    public string? TextAnswer { get; init; }

    public sealed class Handler : IRequestHandler<EvaluateQuestionCommand, Result<EvaluateQuestionResponse>>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<Handler> _logger;

        public Handler(ApplicationDbContext dbContext, ILogger<Handler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<Result<EvaluateQuestionResponse>> Handle(
            EvaluateQuestionCommand request,
            CancellationToken cancellationToken)
        {
            if (!HasExactlyOneAnswer(request) || request.TextAnswer?.Length > 500)
            {
                return Result<EvaluateQuestionResponse>.Failure("question.answer_invalid");
            }

            var question = await _dbContext.Questions
                .AsNoTracking()
                .Where(question =>
                    question.Id == request.QuestionId
                    && question.LearningStep.LessonId == request.LessonId
                    && question.LearningStep.Lesson.Status == LessonStatus.Published)
                .Select(question => new QuestionEvaluationData
                {
                    Id = question.Id,
                    Type = question.QuestionType,
                    TextAnswer = question.TextAnswer,
                    IsCaseSensitive = question.IsCaseSensitive,
                    Explanation = question.Explanation,
                    Options = question.Options
                        .Select(option => new OptionEvaluationData
                        {
                            Id = option.Id,
                            Text = option.Text,
                            AccessibilityText = option.AccessibilityText,
                            IsCorrect = option.IsCorrect
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (question is null)
            {
                return Result<EvaluateQuestionResponse>.Failure("question.not_found");
            }

            return question.Type switch
            {
                QuestionType.TextMultipleChoice or
                QuestionType.ImageMultipleChoice or
                QuestionType.AudioMultipleChoice => EvaluateMultipleChoice(question, request),
                QuestionType.TextInput => EvaluateTextInput(question, request),
                _ => Unsupported(question)
            };
        }

        private Result<EvaluateQuestionResponse> EvaluateMultipleChoice(
            QuestionEvaluationData question,
            EvaluateQuestionCommand request)
        {
            if (request.SelectedOptionId is null || request.TextAnswer is not null)
            {
                return Result<EvaluateQuestionResponse>.Failure("question.answer_invalid");
            }

            var selectedOption = question.Options.SingleOrDefault(option => option.Id == request.SelectedOptionId);
            if (selectedOption is null)
            {
                return Result<EvaluateQuestionResponse>.Failure("question.option_invalid");
            }

            var correctOptions = question.Options.Where(option => option.IsCorrect).ToArray();
            if (correctOptions.Length != 1)
            {
                _logger.LogWarning("Question {QuestionId} has no single correct option.", question.Id);
                return Result<EvaluateQuestionResponse>.Failure("question.answer_invalid");
            }

            var correctOption = correctOptions[0];

            return Success(
                question,
                selectedOption.IsCorrect,
                correctOption.Id,
                correctOption.Text ?? correctOption.AccessibilityText);
        }

        private static Result<EvaluateQuestionResponse> EvaluateTextInput(
            QuestionEvaluationData question,
            EvaluateQuestionCommand request)
        {
            if (request.SelectedOptionId is not null || string.IsNullOrWhiteSpace(request.TextAnswer)
                || string.IsNullOrWhiteSpace(question.TextAnswer))
            {
                return Result<EvaluateQuestionResponse>.Failure("question.answer_invalid");
            }

            var submitted = request.TextAnswer.Trim();
            var expected = question.TextAnswer.Trim();
            var comparison = question.IsCaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return Success(
                question,
                string.Equals(submitted, expected, comparison),
                null,
                expected);
        }

        private Result<EvaluateQuestionResponse> Unsupported(QuestionEvaluationData question)
        {
            _logger.LogWarning(
                "Question {QuestionId} has unsupported type {QuestionType}.",
                question.Id,
                question.Type);
            return Result<EvaluateQuestionResponse>.Failure("question.type_not_supported");
        }

        private static Result<EvaluateQuestionResponse> Success(
            QuestionEvaluationData question,
            bool isCorrect,
            Guid? correctOptionId,
            string? correctText) =>
            Result<EvaluateQuestionResponse>.Success(new EvaluateQuestionResponse
            {
                QuestionId = question.Id,
                IsCorrect = isCorrect,
                CorrectAnswer = new CorrectAnswerDto
                {
                    OptionId = correctOptionId,
                    Text = correctText
                },
                Explanation = question.Explanation
            });

        private static bool HasExactlyOneAnswer(EvaluateQuestionCommand request) =>
            request.SelectedOptionId.HasValue ^ request.TextAnswer is not null;

        private sealed class QuestionEvaluationData
        {
            public Guid Id { get; init; }
            public QuestionType Type { get; init; }
            public string? TextAnswer { get; init; }
            public bool IsCaseSensitive { get; init; }
            public string? Explanation { get; init; }
            public IReadOnlyCollection<OptionEvaluationData> Options { get; init; } = [];
        }

        private sealed class OptionEvaluationData
        {
            public Guid Id { get; init; }
            public string? Text { get; init; }
            public string? AccessibilityText { get; init; }
            public bool IsCorrect { get; init; }
        }
    }
}

public sealed class EvaluateQuestionCommandValidator : AbstractValidator<EvaluateQuestionCommand>
{
    public EvaluateQuestionCommandValidator()
    {
        RuleFor(command => command.LessonId).NotEmpty();
        RuleFor(command => command.QuestionId).NotEmpty();
        RuleFor(command => command.TextAnswer).MaximumLength(500);
        RuleFor(command => command.TextAnswer)
            .NotEmpty()
            .When(command => command.TextAnswer is not null);
        RuleFor(command => command)
            .Must(command => command.SelectedOptionId.HasValue ^ command.TextAnswer is not null)
            .WithMessage("Exactly one answer field must be supplied.");
    }
}
