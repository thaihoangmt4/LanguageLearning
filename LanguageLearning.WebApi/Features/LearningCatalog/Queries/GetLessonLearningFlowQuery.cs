using FluentValidation;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.LearningCatalog.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LearningCatalog.Queries;

public sealed class GetLessonLearningFlowQuery : IRequest<Result<LessonLearningFlowResponse>>
{
    public Guid LessonId { get; init; }

    public sealed class Handler : IRequestHandler<GetLessonLearningFlowQuery, Result<LessonLearningFlowResponse>>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<Handler> _logger;

        public Handler(ApplicationDbContext dbContext, ILogger<Handler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<Result<LessonLearningFlowResponse>> Handle(
            GetLessonLearningFlowQuery request,
            CancellationToken cancellationToken)
        {
            var lesson = await _dbContext.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.Id == request.LessonId && lesson.Status == LessonStatus.Published)
                .Select(lesson => new LessonLearningFlowDto
                {
                    Id = lesson.Id,
                    Title = lesson.Title,
                    Description = lesson.Description,
                    DifficultyLevel = lesson.DifficultyLevel.ToString(),
                    EstimatedDurationMinutes = lesson.EstimatedDurationMinutes,
                    TotalSteps = lesson.LearningSteps.Count
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
            {
                return Result<LessonLearningFlowResponse>.Failure("lesson.not_found");
            }

            var instructionSteps = await _dbContext.LearningSteps
                .AsNoTracking()
                .Where(step => step.LessonId == request.LessonId && step.StepType == LearningStepType.Instruction)
                .Select(step => new LearningStepDto
                {
                    Id = step.Id,
                    Type = "instruction",
                    DisplayOrder = step.DisplayOrder,
                    IsRequired = step.IsRequired,
                    Instruction = new InstructionStepDto
                    {
                        Title = step.InstructionTitle,
                        Text = step.InstructionText,
                        Vocabulary = step.Vocabulary == null
                            ? null
                            : new VocabularyDto
                            {
                                Id = step.Vocabulary.Id,
                                Word = step.Vocabulary.Word,
                                Meaning = step.Vocabulary.Meaning,
                                Phonetic = step.Vocabulary.Phonetic,
                                PartOfSpeech = step.Vocabulary.PartOfSpeech.ToString(),
                                ExampleSentence = step.Vocabulary.ExampleSentence,
                                ExampleTranslation = step.Vocabulary.ExampleTranslation,
                                ImageUrl = step.Vocabulary.ImageUrl,
                                AudioUrl = step.Vocabulary.AudioUrl
                            }
                    }
                })
                .ToListAsync(cancellationToken);

            var questionSteps = await _dbContext.LearningSteps
                .AsNoTracking()
                .Where(step =>
                    step.LessonId == request.LessonId
                    && step.StepType == LearningStepType.Question
                    && step.Question != null)
                .Select(step => new LearningStepDto
                {
                    Id = step.Id,
                    Type = "question",
                    DisplayOrder = step.DisplayOrder,
                    IsRequired = step.IsRequired,
                    Question = new QuestionStepDto
                    {
                        Id = step.Question!.Id,
                        Type = step.Question.QuestionType == QuestionType.TextMultipleChoice
                            ? "textMultipleChoice"
                            : step.Question.QuestionType == QuestionType.ImageMultipleChoice
                                ? "imageMultipleChoice"
                                : step.Question.QuestionType == QuestionType.AudioMultipleChoice
                                    ? "audioMultipleChoice"
                                    : step.Question.QuestionType == QuestionType.TextInput
                                        ? "textInput"
                                        : "unsupported",
                        Prompt = step.Question.Prompt,
                        PromptImageUrl = step.Question.PromptImageUrl,
                        PromptAudioUrl = step.Question.PromptAudioUrl,
                        Options = step.Question.Options
                            .OrderBy(option => option.DisplayOrder)
                            .Select(option => new QuestionOptionDto
                            {
                                Id = option.Id,
                                Text = option.Text,
                                ImageUrl = option.ImageUrl,
                                AudioUrl = option.AudioUrl,
                                AccessibilityText = option.AccessibilityText,
                                DisplayOrder = option.DisplayOrder
                            })
                            .ToList()
                    }
                })
                .ToListAsync(cancellationToken);

            var response = new LessonLearningFlowResponse
            {
                Lesson = lesson,
                Steps = instructionSteps
                    .Concat(questionSteps)
                    .OrderBy(step => step.DisplayOrder)
                    .ToArray()
            };

            if (!IsValid(response))
            {
                _logger.LogWarning(
                    "Published lesson {LessonId} has an invalid learning flow.",
                    request.LessonId);
                return Result<LessonLearningFlowResponse>.Failure("lesson.invalid_learning_flow");
            }

            return Result<LessonLearningFlowResponse>.Success(response);
        }

        private static bool IsValid(LessonLearningFlowResponse response)
        {
            return response.Steps.Count > 0
                && response.Steps.Count == response.Lesson.TotalSteps
                && response.Steps.Any(step => step.IsRequired)
                && response.Steps.Select(step => step.DisplayOrder).Distinct().Count() == response.Steps.Count
                && response.Steps.All(step =>
                    step.Type == "instruction"
                        ? step.Instruction is not null
                            && step.Instruction.Vocabulary is not null
                            && step.Question is null
                        : step.Question is not null
                            && step.Instruction is null
                            && (step.Question.Type == "textInput"
                                ? step.Question.Options.Count == 0
                                : step.Question.Type is "textMultipleChoice" or "imageMultipleChoice" or "audioMultipleChoice"
                                    && step.Question.Options.Count >= 2))
                && response.Steps
                    .Where(step => step.Question is not null)
                    .All(step => step.Question!.Options.Select(option => option.DisplayOrder).Distinct().Count()
                        == step.Question.Options.Count);
        }
    }
}

public sealed class GetLessonLearningFlowQueryValidator : AbstractValidator<GetLessonLearningFlowQuery>
{
    public GetLessonLearningFlowQueryValidator()
    {
        RuleFor(query => query.LessonId).NotEmpty();
    }
}
