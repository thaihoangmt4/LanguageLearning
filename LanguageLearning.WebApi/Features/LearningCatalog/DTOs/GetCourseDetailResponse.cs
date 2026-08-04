using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.LearningCatalog.DTOs;

/// <summary>
/// A published course and its learner-visible curriculum structure.
/// </summary>
public sealed record GetCourseDetailResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public CefrLevel CefrLevel { get; init; }

    public IReadOnlyCollection<CourseUnitResponse> Units { get; init; } = [];
}

/// <summary>
/// A course unit containing its published lessons.
/// </summary>
public sealed record CourseUnitResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public IReadOnlyCollection<CourseLessonResponse> Lessons { get; init; } = [];
}

/// <summary>
/// A published lesson summary within a course unit.
/// </summary>
public sealed record CourseLessonResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? LearningObjectiveSummary { get; init; }

    public int EstimatedDurationMinutes { get; init; }

    public DifficultyLevel DifficultyLevel { get; init; }
}
