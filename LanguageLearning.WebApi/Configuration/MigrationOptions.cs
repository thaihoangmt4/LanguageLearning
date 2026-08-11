namespace LanguageLearning.WebApi.Configuration;

public sealed class MigrationOptions
{
    public const string SectionName = "Migration";

    public bool Enabled { get; init; }

    public string ApiKey { get; init; } = string.Empty;
}
