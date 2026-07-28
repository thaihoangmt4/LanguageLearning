namespace LanguageLearning.WebApi.Middlewares;

/// <summary>
/// Middleware that enriches every HTTP response with a unique trace identifier.
/// </summary>
public sealed class RequestTracingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestTracingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;

        await _next(context);
    }
}
