using System.Reflection;
using System.Security.Claims;
using LanguageLearning.Common.Constants;
using LanguageLearning.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Admin;

public sealed class AdminLogsAuthorizationTests
{
    [Fact]
    public void Endpoint_UsesExistingAdminOnlyPolicy()
    {
        var attribute = Assert.Single(
            typeof(AdminLogsController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(AppConstants.Policies.AdminOnly, attribute.Policy);
    }

    [Fact]
    public async Task UnauthenticatedUser_IsRejectedByAdminPolicy()
    {
        var authorization = CreateAuthorizationService();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            AppConstants.Policies.AdminOnly);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthenticatedNonAdminUser_IsRejectedByAdminPolicy()
    {
        var authorization = CreateAuthorizationService();
        var principal = PrincipalWithRole(AppConstants.Roles.User);

        var result = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            AppConstants.Policies.AdminOnly);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthenticatedAdminUser_IsAllowedByAdminPolicy()
    {
        var authorization = CreateAuthorizationService();
        var principal = PrincipalWithRole(AppConstants.Roles.Admin);

        var result = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            AppConstants.Policies.AdminOnly);

        Assert.True(result.Succeeded);
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

    private static ClaimsPrincipal PrincipalWithRole(string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "Test"));
}
