namespace LanguageLearning.WebApi.Features.Admin.Logs;

public sealed record LogQueryCriteria(
    string? Level,
    string? Search,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    DateTimeOffset? BeforeUtc,
    int Limit);

public sealed record AdminLogEntryResponse(
    DateTimeOffset TimestampUtc,
    string Level,
    string Message,
    string? SourceContext,
    string? TraceId,
    string? RequestId,
    string? RequestMethod,
    string? RequestPath,
    int? StatusCode,
    double? ElapsedMs,
    string? Exception);

public sealed record AdminLogPageResponse(
    IReadOnlyList<AdminLogEntryResponse> Items,
    bool HasMore,
    DateTimeOffset? NextBeforeUtc);

public interface ILogQueryService
{
    Task<AdminLogPageResponse> QueryAsync(
        LogQueryCriteria criteria,
        CancellationToken cancellationToken);
}
