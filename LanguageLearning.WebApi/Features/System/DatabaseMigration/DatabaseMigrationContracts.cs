namespace LanguageLearning.WebApi.Features.System.DatabaseMigration;

public sealed record DatabaseMigrationResponse(
    bool Success,
    IReadOnlyList<string> AppliedMigrations,
    string Message);

public enum DatabaseMigrationStatus
{
    Completed,
    Disabled,
    Unauthorized,
    Conflict,
    Failed
}

public sealed record DatabaseMigrationResult(
    DatabaseMigrationStatus Status,
    DatabaseMigrationResponse? Response = null);
