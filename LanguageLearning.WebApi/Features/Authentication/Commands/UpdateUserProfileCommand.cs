using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentValidation;
using LanguageLearning.Common.Persistence;
using LanguageLearning.Common.Results;
using LanguageLearning.WebApi.Features.Authentication.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LanguageLearning.WebApi.Features.Authentication.Commands;

/// <summary>
/// Updates the authenticated user's learning profile.
/// </summary>
public sealed class UpdateUserProfileCommand : IRequest<Result<UserProfileResponse>>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string NativeLanguageCode { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;

    public int DailyGoalMinutes { get; set; }

    private void Normalize()
    {
        DisplayName = DisplayName?.Trim() ?? string.Empty;
        Username = Username?.Trim().ToLowerInvariant() ?? string.Empty;
        NativeLanguageCode = NativeLanguageCode?.Trim().ToLowerInvariant() ?? string.Empty;
        TimeZoneId = TimeZoneId?.Trim() ?? string.Empty;
    }

    public sealed class Handler : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileResponse>>
    {
        private const string UsernameIndexName = "IX_user_profiles_Username";

        private readonly ApplicationDbContext _dbContext;
        private readonly IValidator<UpdateUserProfileCommand> _validator;

        public Handler(
            ApplicationDbContext dbContext,
            IValidator<UpdateUserProfileCommand> validator)
        {
            _dbContext = dbContext;
            _validator = validator;
        }

        public async Task<Result<UserProfileResponse>> Handle(
            UpdateUserProfileCommand request,
            CancellationToken cancellationToken)
        {
            request.Normalize();
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var profile = await _dbContext.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == request.UserId, cancellationToken);

            if (profile is null)
                return Result<UserProfileResponse>.Failure("user_profile.not_found");

            var usernameExists = await _dbContext.UserProfiles
                .AnyAsync(
                    up => up.Username == request.Username && up.Id != profile.Id,
                    cancellationToken);

            if (usernameExists)
                return Result<UserProfileResponse>.Failure("user_profile.username_already_exists");

            profile.DisplayName = request.DisplayName;
            profile.Username = request.Username;
            profile.NativeLanguageCode = request.NativeLanguageCode;
            profile.TimeZoneId = request.TimeZoneId;
            profile.DailyGoalMinutes = request.DailyGoalMinutes;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: UsernameIndexName
                })
            {
                return Result<UserProfileResponse>.Failure("user_profile.username_already_exists");
            }

            var email = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == request.UserId)
                .Select(user => user.Email)
                .SingleAsync(cancellationToken);

            return Result<UserProfileResponse>.Success(UserProfileResponse.From(profile, email));
        }
    }
}

/// <summary>
/// Validates normalized profile update input.
/// </summary>
public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    private static readonly int[] AllowedDailyGoals = [5, 10, 15, 20, 30, 45, 60];
    private static readonly Regex UsernamePattern = new(
        "^[a-z][a-z0-9_-]{2,29}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public UpdateUserProfileCommandValidator()
    {
        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(command => command.Username)
            .NotEmpty()
            .Length(3, 30)
            .Matches(UsernamePattern)
            .WithMessage("Username must start with a letter and contain only lowercase letters, numbers, underscores, or hyphens.");

        RuleFor(command => command.NativeLanguageCode)
            .Equal("vi")
            .WithMessage("Native language code must be 'vi'.");

        RuleFor(command => command.TimeZoneId)
            .NotEmpty()
            .MaximumLength(100)
            .Must(BeValidTimeZone)
            .WithMessage("Time zone ID must be a valid IANA time zone identifier.");

        RuleFor(command => command.DailyGoalMinutes)
            .Must(value => AllowedDailyGoals.Contains(value))
            .WithMessage("Daily goal must be one of: 5, 10, 15, 20, 30, 45, or 60 minutes.");
    }

    private static bool BeValidTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || !timeZoneId.Contains('/'))
            return false;

        if (CanFindTimeZone(timeZoneId))
            return true;

        return TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsTimeZoneId)
            && CanFindTimeZone(windowsTimeZoneId);
    }

    private static bool CanFindTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
