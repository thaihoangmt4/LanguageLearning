using LanguageLearning.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LanguageLearning.WebApi.Features.System.DatabaseMigration;

public interface IDatabaseMigrationExecutor
{
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);
    Task MigrateAsync(CancellationToken cancellationToken);
}

public sealed class DatabaseMigrationExecutor(ApplicationDbContext dbContext) : IDatabaseMigrationExecutor
{
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) =>
        (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}
