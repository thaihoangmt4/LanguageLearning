using System.Reflection;
using System.Security.Claims;
using LanguageLearning.Common.Configuration;
using LanguageLearning.Common.Constants;
using LanguageLearning.Common.Entities.ExerciseGeneration;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Features.Admin.ExerciseGenerationSettings;
using LanguageLearning.WebApi.Features.ExerciseGeneration;
using LanguageLearning.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Admin;

public sealed class ExerciseGenerationSettingsAdminTests
{
    [Fact]
    public async Task Get_ReturnsCurrentDatabaseSettings()
    {
        await using var db = CreateDb();
        var entity = await SeedSettingsAsync(db);
        var handler = new GetExerciseGenerationSettingsQueryHandler(db);

        var result = await handler.Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(entity.Version, result.Value.Version);
        Assert.Equal(ExerciseGenerationOptions.DefaultIntervalHours, result.Value.IntervalHours);
        Assert.Equal(ExerciseGenerationOptions.DefaultGenerationBatchSize, result.Value.GenerationBatchSize);
    }

    [Fact]
    public async Task AdminPut_AtomicallyUpdatesSettingsAndAuditInformation()
    {
        await using var db = CreateDb();
        var entity = await SeedSettingsAsync(db);
        var originalVersion = entity.Version;
        var adminUserId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        var handler = new UpdateExerciseGenerationSettingsCommandHandler(
            db,
            new StubCurrentUserContext(adminUserId),
            new FixedTimeProvider(now),
            NullLogger<UpdateExerciseGenerationSettingsCommandHandler>.Instance);

        var result = await handler.Handle(
            new(5, 12, 10, 30, 40, 10, entity.Version),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.InitialDelayMinutes);
        Assert.Equal(12, result.Value.IntervalHours);
        Assert.Equal(10, result.Value.MinimumExerciseThreshold);
        Assert.Equal(30, result.Value.TargetExerciseCount);
        Assert.Equal(40, result.Value.MaxExercisesPerLessonPerRun);
        Assert.Equal(10, result.Value.GenerationBatchSize);
        Assert.Equal(now.UtcDateTime, result.Value.UpdatedAtUtc);
        Assert.Equal(adminUserId, result.Value.UpdatedByUserId);
        Assert.NotEqual(originalVersion, result.Value.Version);

        db.ChangeTracker.Clear();
        var persisted = await db.ExerciseGenerationSettings.SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(now.UtcDateTime, persisted.UpdatedAtUtc);
        Assert.Equal(adminUserId, persisted.UpdatedByUserId);
    }

    [Fact]
    public async Task StaleVersion_IsRejectedWithoutOverwritingNewerUpdate()
    {
        await using var db = CreateDb();
        var entity = await SeedSettingsAsync(db);
        var originalVersion = entity.Version;
        var handler = new UpdateExerciseGenerationSettingsCommandHandler(
            db,
            new StubCurrentUserContext(Guid.NewGuid()),
            TimeProvider.System,
            NullLogger<UpdateExerciseGenerationSettingsCommandHandler>.Instance);

        var first = await handler.Handle(
            new(0, 12, 10, 30, 40, 10, originalVersion),
            TestContext.Current.CancellationToken);
        var stale = await handler.Handle(
            new(0, 48, 10, 30, 40, 25, originalVersion),
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(stale.IsFailure);
        Assert.Equal(ExerciseGenerationSettingsErrors.ConcurrencyConflict, stale.Error);
        Assert.Equal(12, (await db.ExerciseGenerationSettings.SingleAsync(
            TestContext.Current.CancellationToken)).IntervalHours);
    }

    [Fact]
    public async Task MissingAuthenticatedUser_IsRejectedWithoutUpdating()
    {
        await using var db = CreateDb();
        var settings = await SeedSettingsAsync(db);
        var originalVersion = settings.Version;
        var handler = new UpdateExerciseGenerationSettingsCommandHandler(
            db,
            new StubCurrentUserContext(null),
            TimeProvider.System,
            NullLogger<UpdateExerciseGenerationSettingsCommandHandler>.Instance);

        var result = await handler.Handle(
            new(0, 24, 20, 40, 50, 20, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ExerciseGenerationSettingsErrors.CurrentUserUnavailable, result.Error);
        Assert.Equal(originalVersion, settings.Version);
    }

    [Fact]
    public void Endpoint_UsesExistingAdminOnlyPolicy()
    {
        var attribute = Assert.Single(
            typeof(AdminExerciseGenerationSettingsController)
                .GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(AppConstants.Policies.AdminOnly, attribute.Policy);
    }

    [Fact]
    public async Task AuthenticatedNonAdmin_IsRejectedByAdminPolicy()
    {
        var authorization = CreateAuthorizationService();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, AppConstants.Roles.User)],
            authenticationType: "Test"));

        var result = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            AppConstants.Policies.AdminOnly);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UnauthenticatedUser_IsRejectedByAdminPolicy()
    {
        var authorization = CreateAuthorizationService();

        var result = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource: null,
            AppConstants.Policies.AdminOnly);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(-1, 24, 20, 40, 50, 20)]
    [InlineData(0, 0, 20, 40, 50, 20)]
    [InlineData(0, 24, -1, 40, 50, 20)]
    [InlineData(0, 24, 20, 40, 0, 20)]
    [InlineData(0, 24, 20, 40, 50, 0)]
    [InlineData(0, 169, 20, 40, 50, 20)]
    [InlineData(0, 24, 20, 501, 50, 20)]
    [InlineData(0, 24, 20, 40, 50, 51)]
    public async Task InvalidOperationalValues_AreRejected(
        int initialDelay,
        int interval,
        int minimum,
        int target,
        int maximum,
        int batchSize)
    {
        var validator = new UpdateExerciseGenerationSettingsCommandValidator();

        var result = await validator.ValidateAsync(
            new UpdateExerciseGenerationSettingsCommand(
                initialDelay, interval, minimum, target, maximum, batchSize, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TargetBelowMinimum_IsRejected()
    {
        var validator = new UpdateExerciseGenerationSettingsCommandValidator();

        var result = await validator.ValidateAsync(
            new UpdateExerciseGenerationSettingsCommand(
                0, 24, 40, 20, 50, 20, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateExerciseGenerationSettingsCommand.TargetExerciseCount));
    }

    [Fact]
    public void SchedulingDecisions_UseStartupDelayAndLatestIntervalIndependently()
    {
        var startup = Snapshot(initialDelay: 10, interval: 24);
        var updated = Snapshot(initialDelay: 60, interval: 6);

        Assert.Equal(TimeSpan.FromMinutes(10), ExerciseGenerationSchedule.StartupDelay(startup));
        Assert.Equal(TimeSpan.FromHours(6), ExerciseGenerationSchedule.NextDelay(updated));
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new(options);
    }

    private static async Task<ExerciseGenerationSettings> SeedSettingsAsync(ApplicationDbContext db)
    {
        var settings = new ExerciseGenerationSettings();
        db.ExerciseGenerationSettings.Add(settings);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return settings;
    }

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationBuilder()
            .AddPolicy(AppConstants.Policies.AdminOnly, policy =>
                policy.RequireRole(AppConstants.Roles.Admin));
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ExerciseGenerationSettingsSnapshot Snapshot(int initialDelay, int interval) =>
        new(initialDelay, interval, 20, 40, 50, 20, DateTime.UtcNow, null, Guid.NewGuid());

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class StubCurrentUserContext(Guid? userId) : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
    }

}
