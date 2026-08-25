using System.Text.RegularExpressions;

namespace LanguageLearning.WebApi.Infrastructure.Logging;

public sealed partial class LogSanitizer
{
    private const string Redacted = "[REDACTED]";

    public string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sanitized = CookieHeaderPattern().Replace(value, match => $"{match.Groups[1].Value}: {Redacted}");
        sanitized = ConnectionStringPattern().Replace(sanitized, "[REDACTED_CONNECTION_STRING]");
        sanitized = JwtPattern().Replace(sanitized, Redacted);
        sanitized = BearerTokenPattern().Replace(sanitized, $"Bearer {Redacted}");
        return SecretValuePattern().Replace(
            sanitized,
            match => $"{match.Groups[1].Value}={Redacted}");
    }

    [GeneratedRegex(@"(?i)\b(cookie|set-cookie)\s*:\s*[^\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex CookieHeaderPattern();

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(
        @"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex(
        @"(?i)\b(?:host|server|data\s+source)\s*=\s*[^;\r\n]+(?:;[^;\r\n=]+\s*=\s*[^;\r\n]*)+",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(
        """(?i)(?:"|')?\b(authorization|password|passwd|refresh[_-]?token|access[_-]?token|id[_-]?token|jwt|api[_-]?key|client[_-]?secret|connection[_-]?string|connectionstrings?(?::[A-Za-z0-9_.-]+)?|user\s+id|username)\b(?:"|')?\s*[:=]\s*(?:"[^"]*"|'[^']*'|[^\s,;&]+)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretValuePattern();
}
