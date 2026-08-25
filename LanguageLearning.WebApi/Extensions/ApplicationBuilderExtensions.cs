namespace LanguageLearning.WebApi.Extensions;

using System.Diagnostics;
using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

/// <summary>
/// Extension methods for configuring the HTTP request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    public static async Task SeedDataAsync(this WebApplication app, CancellationToken cancellationToken)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ExerciseEngineSeeder>().SeedAsync(cancellationToken);
    }

    /// <summary>
    /// Configures the middleware pipeline in the correct order.
    /// </summary>
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var traceId = Activity.Current?.TraceId.ToString();
                diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
                if (!string.IsNullOrEmpty(traceId))
                {
                    diagnosticContext.Set("TraceId", traceId);
                    diagnosticContext.Set("CorrelationId", traceId);
                }
            };
        });

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
