namespace LanguageLearning.WebApi.Extensions;

using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Persistence;

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

        app.MapHealthChecks("/health", new()
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var result = new
                {
                    status = report.Status.ToString(),
                    totalDuration = report.TotalDuration.ToString(),
                    entries = report.Entries.Select(e => new
                    {
                        key = e.Key,
                        status = e.Value.Status.ToString(),
                        duration = e.Value.Duration.ToString(),
                        description = e.Value.Description
                    })
                };

                await context.Response.WriteAsJsonAsync(result);
            }
        });

        app.MapControllers();

        return app;
    }
}
