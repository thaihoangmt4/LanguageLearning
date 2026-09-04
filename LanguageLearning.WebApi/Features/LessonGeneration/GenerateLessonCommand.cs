using System.Data;
using System.Diagnostics;
using FluentValidation;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.LearningCatalog;
using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.LearningCatalog;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.LessonGeneration;

public sealed record GenerateLessonCommand(Guid UnitId) : IRequest<Result<GenerateLessonResponse>>;
public sealed class GenerateLessonCommandValidator : AbstractValidator<GenerateLessonCommand>
{
    public GenerateLessonCommandValidator() => RuleFor(value => value.UnitId).NotEmpty();
}

public sealed class GenerateLessonCommandHandler(
    ApplicationDbContext dbContext,
    ILessonGenerator generator,
    IValidator<GeneratedExercise> exerciseValidator,
    IExerciseContentSerializer serializer,
    IExerciseDefinitionValidatorResolver definitionValidator,
    ILogger<GenerateLessonCommandHandler> logger)
    : IRequestHandler<GenerateLessonCommand, Result<GenerateLessonResponse>>
{
    private static readonly ExerciseType[] PlayableTypes =
    [ExerciseType.MultipleChoice, ExerciseType.ImageMatching, ExerciseType.AudioMatching,
        ExerciseType.Typing, ExerciseType.SentenceOrdering, ExerciseType.Categorization];

    public async Task<Result<GenerateLessonResponse>> Handle(GenerateLessonCommand request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        logger.LogInformation("Lesson generation requested. UnitId: {UnitId}", request.UnitId);
        var enabled = await dbContext.SystemSettings.AsNoTracking()
            .Where(x => x.Id == SystemSettings.SingletonId)
            .Select(x => (bool?)x.LessonGenerationEnabled).SingleOrDefaultAsync(cancellationToken) ?? true;
        if (!enabled)
        {
            logger.LogInformation("Lesson generation disabled. UnitId: {UnitId}", request.UnitId);
            return Result<GenerateLessonResponse>.Failure(LessonGenerationErrors.Disabled);
        }

        var unit = await dbContext.Units.AsNoTracking().Where(x => x.Id == request.UnitId)
            .Select(x => new { x.Id, x.Title, x.Description, CourseTitle = x.Course.Title, x.Course.CefrLevel })
            .SingleOrDefaultAsync(cancellationToken);
        if (unit is null) return Result<GenerateLessonResponse>.Failure(LessonGenerationErrors.UnitNotFound);
        var difficulty = (DifficultyLevel)((int)unit.CefrLevel + 1);
        var existing = await dbContext.Lessons.AsNoTracking().Where(x => x.UnitId == unit.Id)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new ExistingLessonSummary(x.Title, x.Description, x.LearningObjectiveSummary))
            .ToListAsync(cancellationToken);
        var vocabulary = await dbContext.Vocabularies.AsNoTracking().Where(x => x.DifficultyLevel == difficulty)
            .OrderBy(x => x.Word).Take(100)
            .Select(x => new { x.Id, x.Word, x.Meaning, x.ExampleSentence, x.ImageUrl })
            .ToListAsync(cancellationToken);
        var images = vocabulary.Where(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
            .Select(x => new ExerciseGenerationImageAsset(x.Id, x.Meaning, x.Word, x.Meaning)).ToArray();
        var supported = images.Length >= 2 ? PlayableTypes : PlayableTypes.Where(x => x != ExerciseType.ImageMatching).ToArray();
        GeneratedLesson generated;
        try
        {
            generated = await generator.GenerateAsync(new(unit.Id, unit.CourseTitle, unit.Title, unit.Description,
                difficulty, vocabulary.Select(x => new LessonGenerationVocabulary(x.Word, x.Meaning, x.ExampleSentence)).ToArray(),
                existing, supported, LessonRules.RequiredExerciseCount, images), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ExerciseGenerationException exception)
        {
            logger.LogError(exception, "Lesson generation failed. UnitId: {UnitId}", request.UnitId);
            return Result<GenerateLessonResponse>.Failure(LessonGenerationErrors.ProviderFailure);
        }

        var mapped = await ValidateAndMapAsync(generated, supported, images, cancellationToken);
        if (mapped is null)
        {
            logger.LogWarning("Invalid AI lesson generated. UnitId: {UnitId}", request.UnitId);
            return Result<GenerateLessonResponse>.Failure(LessonGenerationErrors.InvalidAiContent);
        }

        await using var transaction = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"
            ? null : await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var order = (await dbContext.Lessons.Where(x => x.UnitId == unit.Id)
                .Select(x => (int?)x.DisplayOrder).MaxAsync(cancellationToken) ?? 0) + 1;
            var lesson = new Lesson
            {
                UnitId = unit.Id, Code = $"AI-{Guid.NewGuid():N}"[..35], Title = generated.Title.Trim(),
                Description = generated.Topic.Trim(), LearningObjectiveSummary = generated.LearningObjective.Trim(),
                EstimatedDurationMinutes = 10, DifficultyLevel = difficulty, DisplayOrder = order,
                Status = LessonStatus.Published
            };
            dbContext.Lessons.Add(lesson);
            dbContext.Exercises.AddRange(mapped.Select(x => new Exercise
            {
                Lesson = lesson, Type = x.Generated.Type, Title = x.Generated.Question.Trim()[..Math.Min(200, x.Generated.Question.Trim().Length)],
                Instruction = InstructionFor(x.Generated.Type), Difficulty = difficulty, DisplayOrder = x.Order,
                ContentJson = x.Json, ContentHash = ExerciseContentHasher.Compute(x.Generated.Type, x.Generated.Question),
                Version = 1, IsRequired = true, IsActive = true
            }));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Lesson generation completed. UnitId: {UnitId}, LessonId: {LessonId}, ExerciseCount: {ExerciseCount}, DurationMs: {DurationMs}",
                unit.Id, lesson.Id, mapped.Count, started.ElapsedMilliseconds);
            return Result<GenerateLessonResponse>.Success(new(lesson.Id, lesson.Title, order));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Lesson generation failed during persistence. UnitId: {UnitId}", unit.Id);
            return Result<GenerateLessonResponse>.Failure(LessonGenerationErrors.PersistenceFailure);
        }
    }

    private async Task<List<Mapped>?> ValidateAndMapAsync(GeneratedLesson lesson, IReadOnlyList<ExerciseType> supported,
        IReadOnlyList<ExerciseGenerationImageAsset> images, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(lesson.Title) || lesson.Title.Length > 200 ||
            string.IsNullOrWhiteSpace(lesson.Topic) || lesson.Topic.Length > 1000 ||
            string.IsNullOrWhiteSpace(lesson.LearningObjective) || lesson.LearningObjective.Length > 1000 ||
            lesson.Exercises.Count != LessonRules.RequiredExerciseCount ||
            !lesson.Exercises.Select(x => x.Order).SequenceEqual(Enumerable.Range(1, LessonRules.RequiredExerciseCount)) ||
            lesson.Exercises.Select(x => x.Exercise.Question.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != LessonRules.RequiredExerciseCount ||
            lesson.Exercises.Select(x => x.Exercise.Type).Distinct().Count() < 2) return null;
        var imageMap = images.ToDictionary(x => x.ImageMediaId);
        var result = new List<Mapped>();
        foreach (var item in lesson.Exercises)
        {
            if (!supported.Contains(item.Exercise.Type) || !(await exerciseValidator.ValidateAsync(item.Exercise, token)).IsValid ||
                !GeneratedExerciseContentMapper.TryMap(item.Exercise, imageMap, out var content) ||
                definitionValidator.Validate(item.Exercise.Type, content).IsFailure) return null;
            var serialized = serializer.Serialize(item.Exercise.Type, content);
            if (serialized.IsFailure) return null;
            result.Add(new(item.Order, item.Exercise, serialized.Value));
        }
        return result;
    }

    private static string InstructionFor(ExerciseType type) => type switch
    {
        ExerciseType.MultipleChoice => "Choose the correct answer.", ExerciseType.ImageMatching => "Match each image.",
        ExerciseType.AudioMatching => "Listen and choose.", ExerciseType.Typing => "Type the answer.",
        ExerciseType.SentenceOrdering => "Arrange the sentence.", ExerciseType.Categorization => "Categorize each item.", _ => string.Empty
    };
    private sealed record Mapped(int Order, GeneratedExercise Generated, string Json);
}
