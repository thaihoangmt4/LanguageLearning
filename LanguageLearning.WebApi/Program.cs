using LanguageLearning.WebApi.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureSerilog(builder.Configuration, builder.Environment);

builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);
builder.Services.AddOpenTelemetry(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseApplicationPipeline();

try
{
    Log.Information("Application starting up.");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
