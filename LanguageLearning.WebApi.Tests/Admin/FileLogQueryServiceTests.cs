using System.Globalization;
using System.Text.Json;
using LanguageLearning.WebApi.Configuration;
using LanguageLearning.WebApi.Features.Admin.Logs;
using LanguageLearning.WebApi.Infrastructure.Logging;
using Xunit;

namespace LanguageLearning.WebApi.Tests.Admin;

public sealed class FileLogQueryServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "language-learning-log-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidStructuredRecord_IsParsedIntoStableResponse()
    {
        var timestamp = new DateTimeOffset(2026, 8, 25, 8, 20, 0, TimeSpan.Zero);
        await WriteLinesAsync(LogLine(
            timestamp,
            "Error",
            "HTTP POST /api/exercises/submit responded 500",
            new()
            {
                ["SourceContext"] = "LanguageLearning.Submissions",
                ["TraceId"] = "abc123",
                ["RequestId"] = "request-1",
                ["RequestMethod"] = "POST",
                ["RequestPath"] = "/api/exercises/submit",
                ["StatusCode"] = 500,
                ["Elapsed"] = 412.5
            },
            "System.Exception: failed"));

        var item = Assert.Single((await QueryAsync()).Items);

        Assert.Equal(timestamp, item.TimestampUtc);
        Assert.Equal("Error", item.Level);
        Assert.Equal("HTTP POST /api/exercises/submit responded 500", item.Message);
        Assert.Equal("LanguageLearning.Submissions", item.SourceContext);
        Assert.Equal("abc123", item.TraceId);
        Assert.Equal("request-1", item.RequestId);
        Assert.Equal("POST", item.RequestMethod);
        Assert.Equal("/api/exercises/submit", item.RequestPath);
        Assert.Equal(500, item.StatusCode);
        Assert.Equal(412.5, item.ElapsedMs);
        Assert.Equal("System.Exception: failed", item.Exception);
    }

    [Fact]
    public async Task MalformedLines_AreIgnoredWithoutFailingQuery()
    {
        await WriteLinesAsync(
            "{\"Timestamp\":\"partially written",
            LogLine(Utc(8), "Information", "valid"),
            "not json");

        var item = Assert.Single((await QueryAsync()).Items);

        Assert.Equal("valid", item.Message);
    }

    [Fact]
    public async Task LevelFilter_IsCaseInsensitive()
    {
        await WriteLinesAsync([
            LogLine(Utc(8), "Information", "info"),
            LogLine(Utc(9), "Error", "error")]);

        var item = Assert.Single((await QueryAsync(level: "error")).Items);

        Assert.Equal("Error", item.Level);
    }

    [Fact]
    public async Task TextSearch_FiltersAcrossNormalizedFields()
    {
        await WriteLinesAsync([
            LogLine(Utc(8), "Warning", "ordinary message"),
            LogLine(Utc(9), "Error", "exercise submission failed")]);

        var item = Assert.Single((await QueryAsync(search: "SUBMISSION")).Items);

        Assert.Equal("exercise submission failed", item.Message);
    }

    [Fact]
    public async Task DateRange_IsInclusive()
    {
        await WriteLinesAsync(
            LogLine(Utc(7), "Information", "before"),
            LogLine(Utc(8), "Information", "first"),
            LogLine(Utc(9), "Information", "second"),
            LogLine(Utc(10), "Information", "after"));

        var result = await QueryAsync(fromUtc: Utc(8), toUtc: Utc(9));

        Assert.Equal(["second", "first"], result.Items.Select(item => item.Message));
    }

    [Fact]
    public async Task Limit_IsEnforcedAndReturnsCursorWhenMoreItemsExist()
    {
        await WriteLinesAsync(
            LogLine(Utc(8), "Information", "oldest"),
            LogLine(Utc(9), "Information", "middle"),
            LogLine(Utc(10), "Information", "newest"));

        var result = await QueryAsync(limit: 2);

        Assert.Equal(2, result.Items.Count);
        Assert.True(result.HasMore);
        Assert.Equal(Utc(9), result.NextBeforeUtc);
    }

    [Fact]
    public async Task BeforeUtc_ReturnsOnlyOlderItems()
    {
        await WriteLinesAsync(
            LogLine(Utc(8), "Information", "older"),
            LogLine(Utc(9), "Information", "cursor"),
            LogLine(Utc(10), "Information", "newer"));

        var result = await QueryAsync(beforeUtc: Utc(9));

        Assert.Equal(["older"], result.Items.Select(item => item.Message));
    }

    [Fact]
    public async Task Results_AreOrderedNewestFirstAcrossFiles()
    {
        await WriteLinesAsync(LogLine(Utc(8), "Information", "older"), "log-20260824.json");
        await WriteLinesAsync(LogLine(Utc(10), "Information", "newer"), "log-20260825.json");

        var result = await QueryAsync();

        Assert.Equal(["newer", "older"], result.Items.Select(item => item.Message));
    }

    [Fact]
    public async Task MissingLogDirectory_ReturnsEmptyPage()
    {
        var result = await QueryAsync();

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
        Assert.Null(result.NextBeforeUtc);
    }

    [Fact]
    public async Task CommonSecrets_AreRedactedBeforeResponse()
    {
        await WriteLinesAsync(LogLine(
            Utc(8),
            "Error",
            "Authorization: Bearer abc.def password=hunter2 refreshToken=refresh-secret apiKey=api-secret Cookie: session=credential",
            exception: "{\"idToken\":\"google-secret\"} raw=eyJheader.eyJpayload.signature ConnectionString=Host=db;Password=db-secret"));

        var item = Assert.Single((await QueryAsync()).Items);
        var returnedText = $"{item.Message} {item.Exception}";

        Assert.Contains("[REDACTED]", returnedText);
        Assert.DoesNotContain("abc.def", returnedText);
        Assert.DoesNotContain("hunter2", returnedText);
        Assert.DoesNotContain("refresh-secret", returnedText);
        Assert.DoesNotContain("api-secret", returnedText);
        Assert.DoesNotContain("credential", returnedText);
        Assert.DoesNotContain("google-secret", returnedText);
        Assert.DoesNotContain("eyJheader", returnedText);
        Assert.DoesNotContain("Host=db", returnedText);
        Assert.DoesNotContain("db-secret", returnedText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private async Task<AdminLogPageResponse> QueryAsync(
        string? level = null,
        string? search = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int limit = 100,
        DateTimeOffset? beforeUtc = null)
    {
        var options = new LogFileOptions { Directory = _directory };
        var service = new FileLogQueryService(options, new LogSanitizer());
        return await service.QueryAsync(
            new(level, search, fromUtc, toUtc, beforeUtc, limit),
            TestContext.Current.CancellationToken);
    }

    private async Task WriteLinesAsync(params string[] lines) =>
        await WriteLinesAsync(lines, "log-20260825.json");

    private async Task WriteLinesAsync(string line, string fileName) =>
        await WriteLinesAsync([line], fileName);

    private async Task WriteLinesAsync(IEnumerable<string> lines, string fileName)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllLinesAsync(
            Path.Combine(_directory, fileName),
            lines,
            TestContext.Current.CancellationToken);
    }

    private static DateTimeOffset Utc(int hour) =>
        new(2026, 8, 25, hour, 0, 0, TimeSpan.Zero);

    private static string LogLine(
        DateTimeOffset timestamp,
        string level,
        string message,
        Dictionary<string, object?>? properties = null,
        string? exception = null) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["Timestamp"] = timestamp.ToString("O", CultureInfo.InvariantCulture),
            ["Level"] = level,
            ["MessageTemplate"] = message,
            ["RenderedMessage"] = message,
            ["Exception"] = exception,
            ["Properties"] = properties ?? []
        });
}
