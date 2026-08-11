using System.Security.Cryptography;
using System.Text;
using LanguageLearning.WebApi.Configuration;
using MediatR;

namespace LanguageLearning.WebApi.Features.System.DatabaseMigration;

public sealed record MigrateDatabaseCommand(string? ApiKey) : IRequest<DatabaseMigrationResult>;

public sealed class MigrateDatabaseCommandHandler(
    MigrationOptions options,
    DatabaseMigrationGuard guard,
    IDatabaseMigrationExecutor executor,
    ILogger<MigrateDatabaseCommandHandler> logger)
    : IRequestHandler<MigrateDatabaseCommand, DatabaseMigrationResult>
{
    public async Task<DatabaseMigrationResult> Handle(
        MigrateDatabaseCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Manual database migration requested.");

        if (!options.Enabled)
            return new(DatabaseMigrationStatus.Disabled);

        if (!IsAuthorized(request.ApiKey, options.ApiKey))
            return new(DatabaseMigrationStatus.Unauthorized);

        if (!guard.TryAcquire())
            return new(DatabaseMigrationStatus.Conflict);

        try
        {
            logger.LogInformation("Manual database migration started.");
            var pending = await executor.GetPendingMigrationsAsync(cancellationToken);
            logger.LogInformation(
                "Found {PendingMigrationCount} pending database migrations: {PendingMigrations}.",
                pending.Count,
                pending);

            if (pending.Count > 0)
                await executor.MigrateAsync(cancellationToken);

            logger.LogInformation(
                "Manual database migration completed. Applied {AppliedMigrationCount} migrations.",
                pending.Count);

            var message = pending.Count == 0
                ? "Database is already up to date."
                : "Database migration completed successfully.";

            return new(
                DatabaseMigrationStatus.Completed,
                new DatabaseMigrationResponse(true, pending, message));
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Manual database migration failed with exception type {ExceptionType}.",
                exception.GetType().Name);
            return new(DatabaseMigrationStatus.Failed);
        }
        finally
        {
            guard.Release();
        }
    }

    private static bool IsAuthorized(string? suppliedKey, string expectedKey)
    {
        if (string.IsNullOrEmpty(suppliedKey) || string.IsNullOrEmpty(expectedKey))
            return false;

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
