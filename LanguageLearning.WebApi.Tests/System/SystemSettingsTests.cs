using System.Reflection;
using System.Security.Claims;
using FluentValidation.TestHelper;
using LanguageLearning.Common.Constants;
using LanguageLearning.Common.Entities.Settings;
using LanguageLearning.Common.Enums;
using LanguageLearning.Common.Persistence;
using LanguageLearning.WebApi.Controllers;
using LanguageLearning.WebApi.Extensions;
using LanguageLearning.WebApi.Features.System.Settings;
using LanguageLearning.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace LanguageLearning.WebApi.Tests.System;

public sealed class SystemSettingsTests
{
    [Fact]
    public async Task Get_ReturnsDefaultInformationSettings()
    {
        await using var db = CreateDb();
        db.SystemSettings.Add(new SystemSettings());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetSystemSettingsQueryHandler(db)
            .Handle(new(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SystemLogLevel.Information, result.Value.MinimumLogLevel);
        Assert.Null(result.Value.UpdatedByUserId);
    }

    [Fact]
    public async Task AdminUpdates_PersistAuditDataAndImmediatelyChangeRuntimeLevel()
    {
        await using var db = CreateDb();
        db.SystemSettings.Add(new SystemSettings());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var adminUserId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 26, 7, 0, 0, TimeSpan.Zero);
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var handler = Handler(db, adminUserId, now, levelSwitch);

        var debug = await handler.Handle(
            new(SystemLogLevel.Debug),
            TestContext.Current.CancellationToken);
        var warning = await handler.Handle(
            new(SystemLogLevel.Warning),
            TestContext.Current.CancellationToken);

        Assert.True(debug.IsSuccess);
        Assert.Equal(SystemLogLevel.Debug, debug.Value.MinimumLogLevel);
        Assert.True(warning.IsSuccess);
        Assert.Equal(SystemLogLevel.Warning, warning.Value.MinimumLogLevel);
        Assert.Equal(LogEventLevel.Warning, levelSwitch.MinimumLevel);
        Assert.Equal(now.UtcDateTime, warning.Value.UpdatedAtUtc);
        Assert.Equal(adminUserId, warning.Value.UpdatedByUserId);

        db.ChangeTracker.Clear();
        var persisted = await db.SystemSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SystemLogLevel.Warning, persisted.MinimumLogLevel);
        Assert.Equal(now.UtcDateTime, persisted.UpdatedAtUtc);
        Assert.Equal(adminUserId, persisted.UpdatedByUserId);
    }

    [Fact]
    public async Task InvalidLogLevel_IsRejectedByFluentValidation()
    {
        var validator = new UpdateSystemSettingsCommandValidator();

        var result = await validator.TestValidateAsync(
            new UpdateSystemSettingsCommand((SystemLogLevel)999),
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(command => command.MinimumLogLevel);
    }

    [Fact]
    public async Task PersistenceFailure_DoesNotChangeDatabaseOrRuntimeLevel()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using (var seed = CreateDb(databaseName, databaseRoot))
        {
            seed.SystemSettings.Add(new SystemSettings());
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var failingDb = CreateDb(
            databaseName,
            databaseRoot,
            new ThrowingSaveChangesInterceptor());
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var handler = Handler(
            failingDb,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            levelSwitch);

        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new(SystemLogLevel.Debug),
            TestContext.Current.CancellationToken));

        Assert.Equal(LogEventLevel.Information, levelSwitch.MinimumLevel);
        await using var verificationDb = CreateDb(databaseName, databaseRoot);
        Assert.Equal(
            SystemLogLevel.Information,
            (await verificationDb.SystemSettings.SingleAsync(
                TestContext.Current.CancellationToken)).MinimumLogLevel);
    }

    [Fact]
    public async Task MissingAuthenticatedUser_IsRejectedWithoutChangingRuntimeLevel()
    {
        await using var db = CreateDb();
        db.SystemSettings.Add(new SystemSettings());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var handler = new UpdateSystemSettingsCommandHandler(
            db,
            new MissingCurrentUser(),
            TimeProvider.System,
            levelSwitch,
            NullLogger<UpdateSystemSettingsCommandHandler>.Instance);

        var result = await handler.Handle(
            new(SystemLogLevel.Debug),
            TestContext.Current.CancellationToken);

        Assert.Equal(SystemSettingsErrors.CurrentUserUnavailable, result.Error);
        Assert.Equal(LogEventLevel.Information, levelSwitch.MinimumLevel);
        Assert.Equal(
            SystemLogLevel.Information,
            (await db.SystemSettings.SingleAsync(
                TestContext.Current.CancellationToken)).MinimumLogLevel);
    }

    [Fact]
    public async Task StartupRestore_AppliesPersistedMinimumLogLevel()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using (var seed = CreateDb(databaseName, databaseRoot))
        {
            var settings = new SystemSettings();
            settings.Update(
                SystemLogLevel.Error,
                DateTime.UtcNow,
                Guid.NewGuid());
            seed.SystemSettings.Add(settings);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(levelSwitch);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        await using var provider = services.BuildServiceProvider();

        await provider.RestoreSystemSettingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LogEventLevel.Error, levelSwitch.MinimumLevel);
    }

    [Fact]
    public async Task StartupRestore_RetainsBootstrapFallbackWhenSettingsAreUnavailable()
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Warning);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(levelSwitch);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        await using var provider = services.BuildServiceProvider();

        await provider.RestoreSystemSettingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LogEventLevel.Warning, levelSwitch.MinimumLevel);
    }

    [Fact]
    public void SettingsActions_UseAdminPolicyWithoutChangingMigrationAuthorization()
    {
        var settingsActions = new[]
        {
            typeof(SystemController).GetMethod(nameof(SystemController.GetSettings))!,
            typeof(SystemController).GetMethod(nameof(SystemController.UpdateSettings))!
        };

        Assert.All(settingsActions, action =>
        {
            var authorize = Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Equal(AppConstants.Policies.AdminOnly, authorize.Policy);
        });
        Assert.NotNull(typeof(SystemController)
            .GetMethod(nameof(SystemController.MigrateDatabase))!
            .GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Empty(typeof(SystemController).GetCustomAttributes<AuthorizeAttribute>());
    }

    [Fact]
    public async Task AdminPolicy_RejectsUnauthenticatedAndNonAdminUsers()
    {
        var authorization = CreateAuthorizationService();
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());
        var nonAdmin = PrincipalWithRole(AppConstants.Roles.User);
        var admin = PrincipalWithRole(AppConstants.Roles.Admin);

        Assert.False((await authorization.AuthorizeAsync(
            unauthenticated, null, AppConstants.Policies.AdminOnly)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            nonAdmin, null, AppConstants.Policies.AdminOnly)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            admin, null, AppConstants.Policies.AdminOnly)).Succeeded);
    }

    private static UpdateSystemSettingsCommandHandler Handler(
        ApplicationDbContext db,
        Guid adminUserId,
        DateTimeOffset now,
        LoggingLevelSwitch levelSwitch) => new(
            db,
            new CurrentUser(adminUserId),
            new FixedTimeProvider(now),
            levelSwitch,
            NullLogger<UpdateSystemSettingsCommandHandler>.Instance);

    private static ApplicationDbContext CreateDb(
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null,
        ISaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(
                databaseName ?? Guid.NewGuid().ToString(),
                databaseRoot ?? new InMemoryDatabaseRoot());
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return new(builder.Options);
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

    private static ClaimsPrincipal PrincipalWithRole(string role) => new(
        new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "Test"));

    private sealed class CurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid? UserId => userId;
    }

    private sealed class MissingCurrentUser : ICurrentUserContext
    {
        public Guid? UserId => null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulated persistence failure.");
    }
}
