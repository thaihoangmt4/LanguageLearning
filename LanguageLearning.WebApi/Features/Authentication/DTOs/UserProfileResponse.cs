using LanguageLearning.Common.Entities.Identity;

namespace LanguageLearning.WebApi.Features.Authentication.DTOs;

/// <summary>
/// The authenticated user's learning profile.
/// </summary>
public sealed record UserProfileResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Username { get; init; }

    public string? NativeLanguageCode { get; init; }

    public string? TimeZoneId { get; init; }

    public int DailyGoalMinutes { get; init; }

    public bool IsProfileCompleted { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static UserProfileResponse From(UserProfile profile, string email)
    {
        return new UserProfileResponse
        {
            Id = profile.Id,
            Email = email,
            DisplayName = profile.DisplayName,
            Username = profile.Username,
            NativeLanguageCode = profile.NativeLanguageCode,
            TimeZoneId = profile.TimeZoneId,
            DailyGoalMinutes = profile.DailyGoalMinutes,
            IsProfileCompleted = IsComplete(profile),
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }

    private static bool IsComplete(UserProfile profile)
    {
        return !string.IsNullOrWhiteSpace(profile.DisplayName)
            && !string.IsNullOrWhiteSpace(profile.Username)
            && !string.IsNullOrWhiteSpace(profile.NativeLanguageCode)
            && !string.IsNullOrWhiteSpace(profile.TimeZoneId)
            && profile.DailyGoalMinutes is >= 5 and <= 180;
    }
}
