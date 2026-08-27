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
    private static readonly ExerciseType[] TextGeneratedTypes =
    [
        ExerciseType.MultipleChoice,
        ExerciseType.AudioMatching,
        ExerciseType.Typing,
        ExerciseType.SentenceOrdering,
        ExerciseType.Categorization,
        ExerciseType.Speaking
    ];

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

        var publishedCourseIds = await dbContext.Courses
            .AsNoTracking()
            .Where(course => course.IsPublished)
            .Select(course => course.Id)
            .ToListAsync(cancellationToken);
        logger.LogDebug(
            "Published courses loaded with PublishedCourseCount {PublishedCourseCount}",
            publishedCourseIds.Count);

        var eligibleUnitIds = publishedCourseIds.Count == 0
            ? []
            : await dbContext.Units
                .AsNoTracking()
                .Where(unit => publishedCourseIds.Contains(unit.CourseId))
                .Select(unit => unit.Id)
                .ToListAsync(cancellationToken);
        logger.LogDebug(
            "Eligible units loaded with UnitCount {UnitCount}",
            eligibleUnitIds.Count);

        var publishedLessons = eligibleUnitIds.Count == 0
            ? []
            : await dbContext.Lessons
                .AsNoTracking()
                .Where(lesson =>
                    eligibleUnitIds.Contains(lesson.UnitId) &&
                    lesson.Status == LessonStatus.Published)
                .Select(lesson => new LessonMetadata(
                    lesson.Id,
                    lesson.Code,
                    lesson.Title,
                    lesson.Description,
                    lesson.LearningObjectiveSummary,
                    lesson.DifficultyLevel))
                .ToListAsync(cancellationToken);
        logger.LogDebug(
            "Published lessons loaded with LessonCount {LessonCount}",
            publishedLessons.Count);

        var lessonDifficulties = publishedLessons
            .Select(lesson => lesson.Difficulty)
            .Distinct()
            .ToArray();
        var imageAssetRows = lessonDifficulties.Length == 0
            ? []
            : await dbContext.Vocabularies
                .AsNoTracking()
                .Where(vocabulary =>
                    lessonDifficulties.Contains(vocabulary.DifficultyLevel) &&
                    vocabulary.ImageUrl != null &&
                    vocabulary.ImageUrl != "")
                .OrderBy(vocabulary => vocabulary.Word)
                .Take(200)
                .Select(vocabulary => new ImageAssetRow(
                    vocabulary.DifficultyLevel,
                    vocabulary.Id,
                    vocabulary.Meaning,
                    vocabulary.Word,
                    vocabulary.Meaning))
                .ToListAsync(cancellationToken);
        var imageAssetsByDifficulty = imageAssetRows
            .GroupBy(row => row.Difficulty)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ExerciseGenerationImageAsset>)group
                    .Select(row => new ExerciseGenerationImageAsset(
                        row.ImageMediaId, row.AltText, row.Word, row.Meaning))
                    .ToArray());
        logger.LogDebug(
            "Image-backed vocabulary loaded with ImageAssetCount {ImageAssetCount}",
            imageAssetRows.Count);

        var publishedLessonIds = publishedLessons.Select(lesson => lesson.LessonId).ToList();
        var exerciseRows = publishedLessonIds.Count == 0
            ? []
            : await dbContext.Exercises
                .AsNoTracking()
                .Where(exercise => publishedLessonIds.Contains(exercise.LessonId))
                .Select(exercise => new ExerciseRow(
                    exercise.LessonId,
                    exercise.IsActive,
                    exercise.DisplayOrder,
                    exercise.ContentHash))
                .ToListAsync(cancellationToken);
        logger.LogDebug(
            "Exercises loaded with ExerciseCount {ExerciseCount}",
            exerciseRows.Count);

        var exerciseRowsByLesson = exerciseRows
            .GroupBy(exercise => exercise.LessonId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var candidates = new List<Candidate>();
        var lessonContexts = new Dictionary<Guid, LessonMetadata>();
        var hashesByLesson = new Dictionary<Guid, HashSet<string>>();

        foreach (var lesson in publishedLessons)
        {
            var lessonExercises = exerciseRowsByLesson.GetValueOrDefault(lesson.LessonId) ?? [];
            var currentExerciseCount = lessonExercises.Count(exercise => exercise.IsActive);
            if (currentExerciseCount >= settings.MinimumExerciseThreshold)
                continue;

            var maximumDisplayOrder = lessonExercises.Count == 0
                ? 0
                : lessonExercises.Max(exercise => exercise.DisplayOrder);
            candidates.Add(new Candidate(lesson.LessonId, currentExerciseCount, maximumDisplayOrder));
            lessonContexts.Add(lesson.LessonId, lesson);
            hashesByLesson.Add(
                lesson.LessonId,
                lessonExercises
                    .Where(exercise => exercise.ContentHash is not null)
                    .Select(exercise => exercise.ContentHash!)
                    .ToHashSet());
        }

        logger.LogDebug(
            "Generation candidates calculated with CandidateCount {CandidateCount}",
            candidates.Count);

        var totalLessons = publishedLessons.Count;

        var processed = 0;
        var failed = 0;
        var requested = 0;
        var accepted = 0;
        var rejected = 0;
        var generatedByType = Enum.GetValues<ExerciseType>().ToDictionary(type => type, _ => 0);
        var acceptedByType = Enum.GetValues<ExerciseType>().ToDictionary(type => type, _ => 0);

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
            var lessonGenerated = 0;
            var lessonGeneratedByType = Enum.GetValues<ExerciseType>().ToDictionary(type => type, _ => 0);
            var lessonAcceptedByType = Enum.GetValues<ExerciseType>().ToDictionary(type => type, _ => 0);
            var nextDisplayOrder = candidate.MaximumDisplayOrder;
            var providerFailed = false;
            var availableImages = imageAssetsByDifficulty.GetValueOrDefault(lesson.Difficulty) ?? [];
            var generatedTypes = availableImages.Count >= 2
                ? Enum.GetValues<ExerciseType>()
                : TextGeneratedTypes;
            var availableImagesById = availableImages.ToDictionary(image => image.ImageMediaId);

            for (var offset = 0; offset < requiredCount; offset += settings.GenerationBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batchSize = Math.Min(settings.GenerationBatchSize, requiredCount - offset);
                requested += batchSize;
                GeneratedExerciseBatch generatedBatch;
                var batchTypes = RotateTypes(generatedTypes, offset)
                    .Take(Math.Min(batchSize, generatedTypes.Length))
                    .ToArray();
                var requestedByType = BuildDistribution(batchTypes, batchSize);

                try
                {
                    generatedBatch = await generator.GenerateAsync(new ExerciseGenerationContext(
                        lesson.LessonId,
                        lesson.Code,
                        lesson.Title,
                        lesson.Description,
                        lesson.LearningObjective,
                        lesson.Difficulty,
                        batchTypes,
                        batchSize,
                        availableImages), cancellationToken);
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

                lessonGenerated += generatedBatch.Exercises.Count;
                foreach (var generated in generatedBatch.Exercises)
                {
                    if (!lessonGeneratedByType.TryGetValue(generated.Type, out var currentCount))
                        continue;
                    lessonGeneratedByType[generated.Type] = currentCount + 1;
                    generatedByType[generated.Type]++;
                }
                var exercisesToPersist = new List<Exercise>();
                var acceptedInBatchByType = batchTypes.ToDictionary(type => type, _ => 0);
                foreach (var generated in generatedBatch.Exercises)
                {
                    if (exercisesToPersist.Count >= batchSize)
                    {
                        lessonRejected++;
                        continue;
                    }

                    var validation = await generatedExerciseValidator.ValidateAsync(generated, cancellationToken);
                    if (!batchTypes.Contains(generated.Type) ||
                        acceptedInBatchByType[generated.Type] >= requestedByType[generated.Type] ||
                        !validation.IsValid ||
                        !TryMapContent(generated, availableImagesById, out var content))
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
                        Instruction = InstructionFor(generated.Type),
                        Difficulty = lesson.Difficulty,
                        DisplayOrder = ++nextDisplayOrder,
                        ContentJson = serialized.Value,
                        ContentHash = hash,
                        Version = 1,
                        IsRequired = true,
                        IsActive = true
                    });
                    acceptedInBatchByType[generated.Type]++;
                }

                if (exercisesToPersist.Count > 0)
                {
                    dbContext.Exercises.AddRange(exercisesToPersist);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    lessonAccepted += exercisesToPersist.Count;
                    foreach (var exercise in exercisesToPersist)
                    {
                        lessonAcceptedByType[exercise.Type]++;
                        acceptedByType[exercise.Type]++;
                    }
                }
            }

            if (!providerFailed) processed++;
            accepted += lessonAccepted;
            rejected += lessonRejected;

            logger.LogInformation(
                "Exercise generation lesson completed for LessonId {LessonId}, CurrentInventory {CurrentInventory}, RequestedCount {RequestedCount}, GeneratedCount {GeneratedCount}, ValidCount {ValidCount}, RejectedCount {RejectedCount}, GeneratedCountByExerciseType {@GeneratedCountByExerciseType}, ValidCountByExerciseType {@ValidCountByExerciseType}, DurationMs {DurationMs}",
                candidate.LessonId, candidate.CurrentExerciseCount, requiredCount, lessonGenerated,
                lessonAccepted, lessonRejected, NonZeroCounts(lessonGeneratedByType),
                NonZeroCounts(lessonAcceptedByType),
                lessonStopwatch.ElapsedMilliseconds);
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
            "Exercise generation job completed with TotalLessons {TotalLessons}, EligibleLessons {EligibleLessons}, ProcessedLessons {ProcessedLessons}, SkippedLessons {SkippedLessons}, FailedLessons {FailedLessons}, RequestedExercises {RequestedExercises}, AcceptedExercises {AcceptedExercises}, RejectedExercises {RejectedExercises}, GeneratedCountByExerciseType {@GeneratedCountByExerciseType}, ValidCountByExerciseType {@ValidCountByExerciseType}, DurationMs {DurationMs}",
            totalLessons, result.EligibleLessons, result.ProcessedLessons, result.SkippedLessons,
            result.FailedLessons, result.RequestedExercises, result.AcceptedExercises,
            result.RejectedExercises, NonZeroCounts(generatedByType), NonZeroCounts(acceptedByType),
            stopwatch.ElapsedMilliseconds);

        return Result<GenerateExercisesResult>.Success(result);
    }

    private static bool TryMapContent(
        GeneratedExercise exercise,
        IReadOnlyDictionary<Guid, ExerciseGenerationImageAsset> availableImages,
        out object content)
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
            case ExerciseType.AudioMatching:
            {
                var options = exercise.Options.Select(x => new ExerciseOption(Guid.NewGuid(), x.Trim())).ToArray();
                var correct = options.FirstOrDefault(x => string.Equals(
                    x.Text, exercise.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (correct is null)
                {
                    content = null!;
                    return false;
                }

                content = new AudioMatchingContent(
                    exercise.PronunciationText!.Trim(), options, correct.Id, exercise.Explanation?.Trim());
                return true;
            }
            case ExerciseType.ImageMatching:
            {
                if (exercise.ImageMatches is null || exercise.ImageMatches.Any(match =>
                        !availableImages.ContainsKey(match.ImageMediaId)))
                {
                    content = null!;
                    return false;
                }

                var sources = exercise.ImageMatches.Select(match => new ImageMatchingSource(
                    Guid.NewGuid(), match.ImageMediaId, availableImages[match.ImageMediaId].AltText)).ToArray();
                var targets = exercise.ImageMatches.Select(match => new MatchingTarget(
                    Guid.NewGuid(), match.Target.Trim())).ToArray();
                var pairs = sources.Zip(targets, (source, target) => new MatchPair(source.Id, target.Id)).ToArray();
                content = new ImageMatchingContent(sources, targets, pairs, exercise.Explanation?.Trim());
                return true;
            }
            case ExerciseType.SentenceOrdering:
            {
                var orderedTokens = exercise.OrderedSegments!
                    .Select(segment => new SentenceToken(Guid.NewGuid(), segment.Trim()))
                    .ToArray();
                var displayTokens = orderedTokens.Length == 2
                    ? orderedTokens.Reverse().ToArray()
                    : orderedTokens.Skip(1).Append(orderedTokens[0]).ToArray();
                content = new SentenceOrderingContent(
                    exercise.Question.Trim(), displayTokens, orderedTokens.Select(token => token.Id).ToArray(),
                    exercise.Explanation?.Trim());
                return true;
            }
            case ExerciseType.Categorization:
            {
                var generatedCategories = exercise.Categories!;
                var categories = generatedCategories
                    .Select(category => new ExerciseCategory(Guid.NewGuid(), category.Name.Trim()))
                    .ToArray();
                var items = new List<CategorizationItem>();
                var assignments = new List<CategoryAssignment>();
                for (var index = 0; index < categories.Length; index++)
                {
                    foreach (var itemText in generatedCategories[index].Items)
                    {
                        var item = new CategorizationItem(Guid.NewGuid(), itemText.Trim());
                        items.Add(item);
                        assignments.Add(new CategoryAssignment(item.Id, categories[index].Id));
                    }
                }

                content = new CategorizationContent(items, categories, assignments, exercise.Explanation?.Trim());
                return true;
            }
            case ExerciseType.Speaking:
                content = new SpeakingContent(
                    exercise.Question.Trim(), exercise.ReferenceText!.Trim(), null);
                return true;
            default:
                content = null!;
                return false;
        }
    }

    private static ExerciseType[] RotateTypes(IReadOnlyList<ExerciseType> types, int offset)
    {
        var start = offset % types.Count;
        return types.Skip(start).Concat(types.Take(start)).ToArray();
    }

    private static IReadOnlyDictionary<ExerciseType, int> BuildDistribution(
        IReadOnlyList<ExerciseType> types,
        int requestedCount)
    {
        var baseCount = requestedCount / types.Count;
        var remainder = requestedCount % types.Count;
        return types.Select((type, index) => new
            {
                Type = type,
                Count = baseCount + (index < remainder ? 1 : 0)
            })
            .ToDictionary(item => item.Type, item => item.Count);
    }

    private static string InstructionFor(ExerciseType type) => type switch
    {
        ExerciseType.MultipleChoice => "Choose the correct answer.",
        ExerciseType.ImageMatching => "Match each image to the correct word or meaning.",
        ExerciseType.AudioMatching => "Listen and choose the correct answer.",
        ExerciseType.Typing => "Type the correct answer.",
        ExerciseType.SentenceOrdering => "Arrange the segments into the correct order.",
        ExerciseType.Categorization => "Place each item in the correct category.",
        ExerciseType.Speaking => "Read the text aloud, then confirm completion.",
        _ => string.Empty
    };

    private static IReadOnlyDictionary<string, int> NonZeroCounts(
        IReadOnlyDictionary<ExerciseType, int> counts) => counts
        .Where(pair => pair.Value > 0)
        .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value);

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private sealed record Candidate(Guid LessonId, int CurrentExerciseCount, int MaximumDisplayOrder);
    private sealed record ExerciseRow(
        Guid LessonId,
        bool IsActive,
        int DisplayOrder,
        string? ContentHash);
    private sealed record LessonMetadata(
        Guid LessonId,
        string Code,
        string Title,
        string? Description,
        string? LearningObjective,
        DifficultyLevel Difficulty);
    private sealed record ImageAssetRow(
        DifficultyLevel Difficulty,
        Guid ImageMediaId,
        string AltText,
        string Word,
        string Meaning);
}
