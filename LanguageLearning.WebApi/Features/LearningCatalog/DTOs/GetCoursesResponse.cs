using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.LearningCatalog.DTOs;

/// <summary>
/// The published courses available to the authenticated learner.
/// </summary>
public sealed record GetCoursesResponse
{
    public IReadOnlyCollection<CourseListItemResponse> Items { get; init; } = [];
}

/// <summary>
/// A summary of a published course in the learning catalog.
/// </summary>
public sealed record CourseListItemResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public CefrLevel CefrLevel { get; init; }

    public int LessonCount { get; init; }
}
