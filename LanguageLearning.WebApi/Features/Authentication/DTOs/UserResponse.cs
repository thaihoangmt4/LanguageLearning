namespace LanguageLearning.WebApi.Features.Authentication.DTOs;

/// <summary>
/// The current authenticated user's profile information.
/// </summary>
public sealed record UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}
