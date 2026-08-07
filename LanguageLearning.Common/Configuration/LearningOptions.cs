namespace LanguageLearning.Common.Configuration;

public sealed class LearningOptions
{
    public const string SectionName = "Learning";
    public string DefaultCourseCode { get; init; } = string.Empty;
}
