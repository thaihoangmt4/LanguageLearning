using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Features.System.DatabaseMigration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.System;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task DisabledMigration_IsRejected()
    {
        var executor = new FakeExecutor();
        var result = await CreateHandler(false, "secret", executor)
            .Handle(new("secret"), TestContext.Current.CancellationToken);

        Assert.Equal(DatabaseMigrationStatus.Disabled, result.Status);
        Assert.Equal(0, executor.PendingCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingMigrationKey_IsRejected(string? key)
    {
        var result = await CreateHandler(true, "secret", new FakeExecutor())
            .Handle(new(key), TestContext.Current.CancellationToken);

        Assert.Equal(DatabaseMigrationStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task InvalidMigrationKey_IsRejected()
    {
        var result = await CreateHandler(true, "secret", new FakeExecutor())
            .Handle(new("wrong"), TestContext.Current.CancellationToken);

        Assert.Equal(DatabaseMigrationStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task NoPendingMigrations_ReturnsUpToDateWithoutMigrating()
    {
        var executor = new FakeExecutor();
        var result = await CreateHandler(true, "secret", executor)
            .Handle(new("secret"), TestContext.Current.CancellationToken);

        Assert.Equal(DatabaseMigrationStatus.Completed, result.Status);
        Assert.Empty(result.Response!.AppliedMigrations);
        Assert.Equal("Database is already up to date.", result.Response.Message);
        Assert.Equal(0, executor.MigrateCalls);
    }

    [Fact]
    public async Task ConcurrentMigration_IsRejected()
    {
        var executor = new FakeExecutor { BlockPendingCheck = true };
        var guard = new DatabaseMigrationGuard();
        var firstHandler = CreateHandler(true, "secret", executor, guard);
        var secondHandler = CreateHandler(true, "secret", executor, guard);

        var firstRequest = firstHandler.Handle(new("secret"), TestContext.Current.CancellationToken);
        await executor.PendingCheckStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var secondResult = await secondHandler.Handle(
            new("secret"), TestContext.Current.CancellationToken);

        Assert.Equal(DatabaseMigrationStatus.Conflict, secondResult.Status);

        executor.ReleasePendingCheck.TrySetResult();
        await firstRequest;
    }

    private static MigrateDatabaseCommandHandler CreateHandler(
        bool enabled,
        string apiKey,
        FakeExecutor executor,
        DatabaseMigrationGuard? guard = null) =>
        new(
            new MigrationOptions { Enabled = enabled, ApiKey = apiKey },
            guard ?? new DatabaseMigrationGuard(),
            executor,
            NullLogger<MigrateDatabaseCommandHandler>.Instance);

    private sealed class FakeExecutor : IDatabaseMigrationExecutor
    {
        public bool BlockPendingCheck { get; init; }
        public int PendingCalls { get; private set; }
        public int MigrateCalls { get; private set; }
        public TaskCompletionSource PendingCheckStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleasePendingCheck { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
            CancellationToken cancellationToken)
        {
            PendingCalls++;
            PendingCheckStarted.TrySetResult();
            if (BlockPendingCheck)
                await ReleasePendingCheck.Task.WaitAsync(cancellationToken);
            return [];
        }

        public Task MigrateAsync(CancellationToken cancellationToken)
        {
            MigrateCalls++;
            return Task.CompletedTask;
        }
    }
}
