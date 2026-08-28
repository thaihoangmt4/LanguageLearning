using FluentValidation;
using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using LanguageLearning.WebApi.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ExerciseGenerationSettingsEntity = LanguageLearning.Common.Entities.ExerciseGeneration.ExerciseGenerationSettings;
using SystemSettingsEntity = LanguageLearning.Common.Entities.Settings.SystemSettings;

namespace LanguageLearning.WebApi.Features.Admin.ExerciseGenerationSettings;

public sealed record UpdateExerciseGenerationSettingsCommand(
    int InitialDelayMinutes,
    int IntervalHours,
    int MinimumExerciseThreshold,
    int TargetExerciseCount,
    int MaxExercisesPerLessonPerRun,
    Guid Version,
    bool Enabled = true) : IRequest<Result<ExerciseGenerationSettingsResponse>>;

public sealed class UpdateExerciseGenerationSettingsCommandValidator
    : AbstractValidator<UpdateExerciseGenerationSettingsCommand>
{
    public UpdateExerciseGenerationSettingsCommandValidator()
    {
        RuleFor(command => command.Version).NotEmpty();
        RuleFor(command => command).Custom((command, context) =>
        {
            var violations = ExerciseGenerationOptions.ValidateValues(
                command.InitialDelayMinutes,
                command.IntervalHours,
                command.MinimumExerciseThreshold,
                command.TargetExerciseCount,
                command.MaxExercisesPerLessonPerRun);
            foreach (var violation in violations)
                context.AddFailure(violation.PropertyName, violation.Message);
        });
    }
}

public sealed class UpdateExerciseGenerationSettingsCommandHandler(
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider,
    ILogger<UpdateExerciseGenerationSettingsCommandHandler> logger)
    : IRequestHandler<UpdateExerciseGenerationSettingsCommand, Result<ExerciseGenerationSettingsResponse>>
{
    public async Task<Result<ExerciseGenerationSettingsResponse>> Handle(
        UpdateExerciseGenerationSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserContext.UserId is not { } adminUserId)
        {
            return Result<ExerciseGenerationSettingsResponse>.Failure(
                ExerciseGenerationSettingsErrors.CurrentUserUnavailable);
        }

        var settings = await dbContext.ExerciseGenerationSettings.SingleOrDefaultAsync(
            value => value.Id == ExerciseGenerationSettingsEntity.SingletonId,
            cancellationToken);
        if (settings is null)
        {
            return Result<ExerciseGenerationSettingsResponse>.Failure(
                ExerciseGenerationSettingsErrors.NotFound);
        }
        if (settings.Version != request.Version)
        {
            return Result<ExerciseGenerationSettingsResponse>.Failure(
                ExerciseGenerationSettingsErrors.ConcurrencyConflict);
        }

        var oldIntervalHours = settings.IntervalHours;
        var systemSettings = await dbContext.SystemSettings.SingleOrDefaultAsync(
            value => value.Id == SystemSettingsEntity.SingletonId,
            cancellationToken);
        if (systemSettings is null)
        {
            systemSettings = new SystemSettingsEntity();
            dbContext.SystemSettings.Add(systemSettings);
        }

        var oldEnabled = systemSettings.ExerciseGenerationEnabled;
        var updatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        settings.Update(
            request.InitialDelayMinutes,
            request.IntervalHours,
            request.MinimumExerciseThreshold,
            request.TargetExerciseCount,
            request.MaxExercisesPerLessonPerRun,
            updatedAtUtc,
            adminUserId);
        systemSettings.SetExerciseGenerationEnabled(request.Enabled, updatedAtUtc, adminUserId);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ExerciseGenerationSettingsResponse>.Failure(
                ExerciseGenerationSettingsErrors.ConcurrencyConflict);
        }

        logger.LogInformation(
            "Exercise generation settings updated by AdminUserId {AdminUserId}, OldEnabled {OldEnabled}, NewEnabled {NewEnabled}, OldIntervalHours {OldIntervalHours}, NewIntervalHours {NewIntervalHours}",
            adminUserId,
            oldEnabled,
            systemSettings.ExerciseGenerationEnabled,
            oldIntervalHours,
            settings.IntervalHours);

        return Result<ExerciseGenerationSettingsResponse>.Success(
            GetExerciseGenerationSettingsQueryHandler.ToResponse(
                settings,
                systemSettings.ExerciseGenerationEnabled));
    }
}
