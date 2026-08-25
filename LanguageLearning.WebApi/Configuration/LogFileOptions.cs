namespace LanguageLearning.WebApi.Configuration;

public sealed class LogFileOptions
{
    public const string SectionName = "LogFiles";

    public string Directory { get; init; } = "logs";

    public long FileSizeLimitBytes { get; init; } = 52_428_800;

    public int RetentionDays { get; init; } = 14;

    public int RetainedFileCountLimit { get; init; } = 50;

    public int MaxFilesToScan { get; init; } = 50;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Directory))
            throw new InvalidOperationException($"'{SectionName}:Directory' must be configured.");
        if (FileSizeLimitBytes <= 0)
            throw new InvalidOperationException($"'{SectionName}:FileSizeLimitBytes' must be greater than zero.");
        if (RetentionDays is < 1 or > 90)
            throw new InvalidOperationException($"'{SectionName}:RetentionDays' must be between 1 and 90.");
        if (RetainedFileCountLimit is < 1 or > 100)
            throw new InvalidOperationException($"'{SectionName}:RetainedFileCountLimit' must be between 1 and 100.");
        if (MaxFilesToScan is < 1 or > 200)
            throw new InvalidOperationException($"'{SectionName}:MaxFilesToScan' must be between 1 and 200.");
    }
}
