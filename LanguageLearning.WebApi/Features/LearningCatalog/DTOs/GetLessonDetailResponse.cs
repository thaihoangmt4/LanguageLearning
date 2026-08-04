using LanguageLearning.Common.Enums;

namespace LanguageLearning.WebApi.Features.LearningCatalog.DTOs;

/// <summary>
/// A learner-visible lesson with its catalog context and ordered sections.
/// </summary>
public sealed record GetLessonDetailResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? LearningObjectiveSummary { get; init; }

    public int EstimatedDurationMinutes { get; init; }

    public DifficultyLevel DifficultyLevel { get; init; }

    public LessonCourseResponse Course { get; init; } = new();

    public LessonUnitResponse Unit { get; init; } = new();

    public IReadOnlyCollection<LessonSectionResponse> Sections { get; init; } = [];
}

/// <summary>
/// The published parent course of a lesson.
/// </summary>
public sealed record LessonCourseResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public CefrLevel CefrLevel { get; init; }
}

/// <summary>
/// The parent unit of a lesson.
/// </summary>
public sealed record LessonUnitResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;
}

/// <summary>
/// An ordered section belonging to a lesson.
/// </summary>
public sealed record LessonSectionResponse
{
    public Guid Id { get; init; }

    public LessonSectionType SectionType { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool IsRequired { get; init; }
}
