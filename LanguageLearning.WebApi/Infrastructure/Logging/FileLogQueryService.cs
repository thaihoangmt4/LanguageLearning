using System.Globalization;
using System.Text.Json;
using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Features.Admin.Logs;

namespace LanguageLearning.WebApi.Infrastructure.Logging;

public sealed class FileLogQueryService(LogFileOptions options, LogSanitizer sanitizer) : ILogQueryService
{
    private const string FilePattern = "log-*.json";

    public async Task<AdminLogPageResponse> QueryAsync(
        LogQueryCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.Directory))
            return new([], false, null);

        var capacity = criteria.Limit + 1;
        var newestEvents = new PriorityQueue<AdminLogEntryResponse, DateTimeOffset>();
        var files = Directory.EnumerateFiles(options.Directory, FilePattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(options.MaxFilesToScan)
            .Select(file => file.FullName);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReadFileAsync(file, criteria, capacity, newestEvents, cancellationToken);
            if (newestEvents.Count >= capacity)
                break;
        }

        var ordered = newestEvents.UnorderedItems
            .Select(item => item.Element)
            .OrderByDescending(item => item.TimestampUtc)
            .ToArray();
        var hasMore = ordered.Length > criteria.Limit;
        var items = ordered.Take(criteria.Limit).ToArray();

        return new(
            items,
            hasMore,
            hasMore && items.Length > 0 ? items[^1].TimestampUtc : null);
    }

    private async Task ReadFileAsync(
        string path,
        LogQueryCriteria criteria,
        int capacity,
        PriorityQueue<AdminLogEntryResponse, DateTimeOffset> newestEvents,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 16_384,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream);

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!TryParse(line, out var item) || !Matches(item, criteria))
                    continue;

                newestEvents.Enqueue(item, item.TimestampUtc);
                if (newestEvents.Count > capacity)
                    newestEvents.Dequeue();
            }
        }
        catch (IOException)
        {
            // A rolling file may disappear between enumeration and opening.
        }
        catch (UnauthorizedAccessException)
        {
            // Skip an unreadable file without failing the complete query.
        }
    }

    private bool TryParse(string line, out AdminLogEntryResponse item)
    {
        item = null!;

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!TryGetTimestamp(root, out var timestamp) || !TryGetString(root, "Level", out var level))
                return false;

            var properties = root.TryGetProperty("Properties", out var value) && value.ValueKind == JsonValueKind.Object
                ? value
                : default;
            var message = GetString(root, "RenderedMessage") ?? GetString(root, "MessageTemplate") ?? string.Empty;

            item = new(
                timestamp.ToUniversalTime(),
                sanitizer.Sanitize(level) ?? string.Empty,
                sanitizer.Sanitize(message) ?? string.Empty,
                SanitizeProperty(properties, "SourceContext"),
                SanitizeProperty(properties, "TraceId"),
                SanitizeProperty(properties, "RequestId"),
                SanitizeProperty(properties, "RequestMethod"),
                SanitizeProperty(properties, "RequestPath"),
                GetInt32(properties, "StatusCode"),
                GetDouble(properties, "Elapsed"),
                sanitizer.Sanitize(GetString(root, "Exception")));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool Matches(AdminLogEntryResponse item, LogQueryCriteria criteria)
    {
        if (criteria.Level is not null &&
            !string.Equals(item.Level, criteria.Level, StringComparison.OrdinalIgnoreCase))
            return false;
        if (criteria.FromUtc is { } fromUtc && item.TimestampUtc < fromUtc)
            return false;
        if (criteria.ToUtc is { } toUtc && item.TimestampUtc > toUtc)
            return false;
        if (criteria.BeforeUtc is { } beforeUtc && item.TimestampUtc >= beforeUtc)
            return false;
        if (!string.IsNullOrWhiteSpace(criteria.Search) && !Contains(item, criteria.Search))
            return false;
        return true;
    }

    private static bool Contains(AdminLogEntryResponse item, string search) =>
        new[]
        {
            item.Message,
            item.SourceContext,
            item.Exception,
            item.TraceId,
            item.RequestId,
            item.RequestMethod,
            item.RequestPath
        }.Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);

    private string? SanitizeProperty(JsonElement properties, string name) =>
        sanitizer.Sanitize(GetString(properties, name));

    private static bool TryGetTimestamp(JsonElement element, out DateTimeOffset timestamp)
    {
        var value = GetString(element, "Timestamp");
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = GetString(element, name) ?? string.Empty;
        return value.Length > 0;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? GetInt32(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        return int.TryParse(value.ToString(), CultureInfo.InvariantCulture, out number) ? number : null;
    }

    private static double? GetDouble(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;
        return double.TryParse(value.ToString(), CultureInfo.InvariantCulture, out number) ? number : null;
    }
}
