using System.Diagnostics;
using FluentValidation;
using LanguageLearning.Common.Entities.ExerciseEngine;
using LanguageLearning.Common.Entities.ExerciseGeneration;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.ExerciseEngine;
using LanguageLearning.Common.ExerciseEngine.Models;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;

public sealed record GenerateExercisesCommand : IRequest<Result<GenerateExercisesResult>>;

public sealed record GenerateExercisesResult(
    int EligibleLessons,
    int ProcessedLessons,
    int SkippedLessons,
    int FailedLessons,
    int RequestedExercises,
    int AcceptedExercises,
    int RejectedExercises);

public sealed class GenerateExercisesCommandHandler(
    ApplicationDbContext dbContext,
    IExerciseGenerator generator,
    IValidator<GeneratedExercise> generatedExerciseValidator,
    IExerciseContentSerializer contentSerializer,
    IExerciseDefinitionValidatorResolver definitionValidator,
    ILogger<GenerateExercisesCommandHandler> logger)
    : IRequestHandler<GenerateExercisesCommand, Result<GenerateExercisesResult>>
{
    private static readonly ExerciseType[] GeneratedTypes =
        [ExerciseType.MultipleChoice, ExerciseType.Typing];

    public async Task<Result<GenerateExercisesResult>> Handle(
        GenerateExercisesCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = await dbContext.ExerciseGenerationSettings
            .AsNoTracking()
            .Where(value => value.Id == ExerciseGenerationSettings.SingletonId)
            .Select(value => new ExerciseGenerationSettingsSnapshot(
                value.InitialDelayMinutes,
                value.IntervalHours,
                value.MinimumExerciseThreshold,
                value.TargetExerciseCount,
                value.MaxExercisesPerLessonPerRun,
                value.GenerationBatchSize,
                value.UpdatedAtUtc,
                value.UpdatedByUserId,
                value.Version))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(ExerciseGenerationSettingsErrors.NotFound);
        logger.LogInformation("Exercise generation job started");

        var totalLessons = await dbContext.Lessons.AsNoTracking()
            .CountAsync(x => x.Status == LessonStatus.Published && x.Unit.Course.IsPublished, cancellationToken);

        var candidates = await dbContext.Lessons.AsNoTracking()
            .Where(x => x.Status == LessonStatus.Published && x.Unit.Course.IsPublished)
            .Select(x => new Candidate(
                x.Id,
                dbContext.Exercises.Count(exercise => exercise.LessonId == x.Id && exercise.IsActive),
                dbContext.Exercises.Where(exercise => exercise.LessonId == x.Id)
                    .Select(exercise => (int?)exercise.DisplayOrder).Max() ?? 0))
            .Where(x => x.CurrentExerciseCount < settings.MinimumExerciseThreshold)
            .ToListAsync(cancellationToken);

        var candidateIds = candidates.Select(x => x.LessonId).ToArray();
        var lessonContexts = await dbContext.Lessons.AsNoTracking()
            .Where(x => candidateIds.Contains(x.Id))
            .Select(x => new LessonMetadata(
                x.Id,
                x.Code,
                x.Title,
                x.Description,
                x.LearningObjectiveSummary,
                x.DifficultyLevel))
            .ToDictionaryAsync(x => x.LessonId, cancellationToken);

        var persistedHashes = await dbContext.Exercises.AsNoTracking()
            .Where(x => candidateIds.Contains(x.LessonId) && x.ContentHash != null)
            .Select(x => new { x.LessonId, ContentHash = x.ContentHash! })
            .ToListAsync(cancellationToken);
        var hashesByLesson = persistedHashes
            .GroupBy(x => x.LessonId)
            .ToDictionary(x => x.Key, x => x.Select(value => value.ContentHash).ToHashSet());

        var processed = 0;
        var failed = 0;
        var requested = 0;
        var accepted = 0;
        var rejected = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lessonStopwatch = Stopwatch.StartNew();
            var requiredCount = ExerciseGenerationPolicy.RequiredCount(
                candidate.CurrentExerciseCount,
                settings.MinimumExerciseThreshold,
                settings.TargetExerciseCount,
                settings.MaxExercisesPerLessonPerRun);

            if (requiredCount == 0 || !lessonContexts.TryGetValue(candidate.LessonId, out var lesson))
                continue;

            var existingHashes = hashesByLesson.TryGetValue(candidate.LessonId, out var hashes)
                ? hashes
                : [];

            var lessonAccepted = 0;
            var lessonRejected = 0;
            var nextDisplayOrder = candidate.MaximumDisplayOrder;
            var providerFailed = false;

            for (var offset = 0; offset < requiredCount; offset += settings.GenerationBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batchSize = Math.Min(settings.GenerationBatchSize, requiredCount - offset);
                requested += batchSize;
                GeneratedExerciseBatch generatedBatch;

                try
                {
                    generatedBatch = await generator.GenerateAsync(new ExerciseGenerationContext(
                        lesson.LessonId,
                        lesson.Code,
                        lesson.Title,
                        lesson.Description,
                        lesson.LearningObjective,
                        lesson.Difficulty,
                        GeneratedTypes,
                        batchSize), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ExerciseGenerationException exception)
                {
                    logger.LogError(exception,
                        "Exercise generation failed for LessonId {LessonId}", candidate.LessonId);
                    failed++;
                    providerFailed = true;
                    break;
                }

                var exercisesToPersist = new List<Exercise>();
                foreach (var generated in generatedBatch.Exercises)
                {
                    if (exercisesToPersist.Count >= batchSize)
                    {
                        lessonRejected++;
                        continue;
                    }

                    var validation = await generatedExerciseValidator.ValidateAsync(generated, cancellationToken);
                    if (!validation.IsValid || !TryMapContent(generated, out var content))
                    {
                        lessonRejected++;
                        continue;
                    }

                    var definitionResult = definitionValidator.Validate(generated.Type, content);
                    var serialized = contentSerializer.Serialize(generated.Type, content);
                    if (definitionResult.IsFailure || serialized.IsFailure)
                    {
                        lessonRejected++;
                        continue;
                    }

                    var hash = ExerciseContentHasher.Compute(generated.Type, generated.Question);
                    if (!existingHashes.Add(hash))
                    {
                        lessonRejected++;
                        continue;
                    }

                    exercisesToPersist.Add(new Exercise
                    {
                        LessonId = candidate.LessonId,
                        Type = generated.Type,
                        Title = Truncate(generated.Question.Trim(), 200),
                        Instruction = generated.Type == ExerciseType.MultipleChoice
                            ? "Choose the correct answer."
                            : "Type the correct answer.",
                        Difficulty = lesson.Difficulty,
                        DisplayOrder = ++nextDisplayOrder,
                        ContentJson = serialized.Value,
                        ContentHash = hash,
                        Version = 1,
                        IsRequired = true,
                        IsActive = true
                    });
                }

                if (exercisesToPersist.Count > 0)
                {
                    dbContext.Exercises.AddRange(exercisesToPersist);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    lessonAccepted += exercisesToPersist.Count;
                }
            }

            if (!providerFailed) processed++;
            accepted += lessonAccepted;
            rejected += lessonRejected;

            logger.LogInformation(
                "Exercise generation lesson completed for LessonId {LessonId}, CurrentInventory {CurrentInventory}, RequestedCount {RequestedCount}, AcceptedCount {AcceptedCount}, RejectedCount {RejectedCount}, DurationMs {DurationMs}",
                candidate.LessonId, candidate.CurrentExerciseCount, requiredCount, lessonAccepted,
                lessonRejected, lessonStopwatch.ElapsedMilliseconds);
        }

        var result = new GenerateExercisesResult(
            candidates.Count,
            processed,
            Math.Max(0, totalLessons - candidates.Count),
            failed,
            requested,
            accepted,
            rejected);

        logger.LogInformation(
            "Exercise generation job completed with TotalLessons {TotalLessons}, EligibleLessons {EligibleLessons}, ProcessedLessons {ProcessedLessons}, SkippedLessons {SkippedLessons}, FailedLessons {FailedLessons}, RequestedExercises {RequestedExercises}, AcceptedExercises {AcceptedExercises}, RejectedExercises {RejectedExercises}, DurationMs {DurationMs}",
            totalLessons, result.EligibleLessons, result.ProcessedLessons, result.SkippedLessons,
            result.FailedLessons, result.RequestedExercises, result.AcceptedExercises,
            result.RejectedExercises, stopwatch.ElapsedMilliseconds);

        return Result<GenerateExercisesResult>.Success(result);
    }

    private static bool TryMapContent(GeneratedExercise exercise, out object content)
    {
        switch (exercise.Type)
        {
            case ExerciseType.MultipleChoice:
            {
                var options = exercise.Options.Select(x => new ExerciseOption(Guid.NewGuid(), x.Trim())).ToArray();
                var correct = options.FirstOrDefault(x => string.Equals(
                    x.Text, exercise.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (correct is null)
                {
                    content = null!;
                    return false;
                }

                content = new MultipleChoiceContent(exercise.Question.Trim(), options, correct.Id, exercise.Explanation?.Trim());
                return true;
            }
            case ExerciseType.Typing:
                content = new TypingContent(
                    exercise.Question.Trim(), [exercise.CorrectAnswer!.Trim()], false, true,
                    exercise.Explanation?.Trim(), ExerciseLimits.MaximumTypingLength);
                return true;
            default:
                content = null!;
                return false;
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed record Candidate(Guid LessonId, int CurrentExerciseCount, int MaximumDisplayOrder);
    private sealed record LessonMetadata(
        Guid LessonId,
        string Code,
        string Title,
        string? Description,
        string? LearningObjective,
        DifficultyLevel Difficulty);
}
