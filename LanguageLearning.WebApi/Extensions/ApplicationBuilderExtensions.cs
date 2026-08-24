namespace LanguageLearning.WebApi.Extensions;

using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extension methods for configuring the HTTP request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    public static async Task SeedDevelopmentDataAsync(this WebApplication app, CancellationToken cancellationToken)
    {
        if (!app.Environment.IsDevelopment()) return;
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ExerciseEngineSeeder>().SeedAsync(cancellationToken);
    }

    /// <summary>
    /// Configures the middleware pipeline in the correct order.
    /// </summary>
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        app.UseExceptionHandler();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.HeadContent = "";
            c.InjectStylesheet("https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.18.2/swagger-ui.css");
            c.InjectJavascript("https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.18.2/swagger-ui-bundle.js");
            c.InjectJavascript("https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.18.2/swagger-ui-standalone-preset.js");
        });

        app.UseHttpsRedirection();

        app.UseCors(CorsSettings.FrontendPolicyName);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();

        app.MapControllers();

        return app;
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(
            report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
            context.RequestAborted);
    }
}
