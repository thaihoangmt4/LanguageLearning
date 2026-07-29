using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LanguageLearning.Common.Persistence;

/// <summary>
/// Design-time factory for creating <see cref="ApplicationDbContext"/> during EF Core migrations.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // The connection string is read from the WebApi project's appsettings at design time.
        // For local development, set the connection string via the DOTNET_CONNECTION_STRING
        // environment variable or update this to match your local PostgreSQL instance.
        var connectionString = Environment.GetEnvironmentVariable("DOTNET_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=language_learning_dev;Username=postgres;Password=aod@123";

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}