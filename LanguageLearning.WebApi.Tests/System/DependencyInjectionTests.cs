using LanguageLearning.WebApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LanguageLearning.WebApi.Tests.System;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void ApplicationContainer_ValidatesAllRegistrations()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.FullName,
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=localhost;Database=language_learning_tests;Username=test;Password=test",
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            ["Jwt:Secret"] = "test-only-secret-that-is-at-least-thirty-two-characters",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:ExpirationInMinutes"] = "15",
            ["Jwt:RefreshTokenExpirationInDays"] = "30",
            ["Google:ClientId"] = "test-client-id",
            ["Learning:DefaultCourseCode"] = "FOUNDATIONS"
        });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        var loggingLevelSwitch = builder.Host.ConfigureSerilog(
            builder.Configuration,
            builder.Environment);
        builder.Services.AddSingleton(loggingLevelSwitch);
        builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);
        builder.Services.AddOpenTelemetry(builder.Configuration, builder.Environment);

        using var app = builder.Build();
        app.UseApplicationPipeline();
        using var scope = app.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider);
    }
}
