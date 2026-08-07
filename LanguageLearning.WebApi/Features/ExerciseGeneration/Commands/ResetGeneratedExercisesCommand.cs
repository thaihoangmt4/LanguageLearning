using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.ExerciseGeneration.Commands;

public sealed record ResetGeneratedExercisesCommand : IRequest<Result<ResetGeneratedExercisesResult>>;

public sealed record ResetGeneratedExercisesResult(
    int DetectedGeneratedExercises,
    int DeletedExercises,
    int PreservedReferencedExercises);

public sealed class ResetGeneratedExercisesCommandHandler(
    ApplicationDbContext dbContext,
    IHostEnvironment environment,
    ILogger<ResetGeneratedExercisesCommandHandler> logger)
    : IRequestHandler<ResetGeneratedExercisesCommand, Result<ResetGeneratedExercisesResult>>
{
    public const string NotAvailable = "test.generated_exercise_reset_not_available";

    public async Task<Result<ResetGeneratedExercisesResult>> Handle(
        ResetGeneratedExercisesCommand request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return Result<ResetGeneratedExercisesResult>.Failure(NotAvailable);

        // Sprint 8 generated exercises are the only records assigned ContentHash.
        // Referenced exercises are preserved so development resets cannot corrupt attempt snapshots.
        var generated = await dbContext.Exercises
            .Where(x => x.ContentHash != null)
            .ToListAsync(cancellationToken);
        var generatedIds = generated.Select(x => x.Id).ToArray();
        var referencedIds = await dbContext.LessonAttemptExercises.AsNoTracking()
            .Where(x => generatedIds.Contains(x.ExerciseId))
            .Select(x => x.ExerciseId)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
        var deletable = generated.Where(x => !referencedIds.Contains(x.Id)).ToArray();

        dbContext.Exercises.RemoveRange(deletable);
        if (deletable.Length > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Development generated-exercise reset completed with DetectedGeneratedExercises {DetectedGeneratedExercises}, DeletedExercises {DeletedExercises}, PreservedReferencedExercises {PreservedReferencedExercises}",
            generated.Count, deletable.Length, referencedIds.Count);

        return Result<ResetGeneratedExercisesResult>.Success(new(
            generated.Count, deletable.Length, referencedIds.Count));
    }
}
