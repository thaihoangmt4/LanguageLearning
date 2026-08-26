using LanguageLearning.Common.Enums;
using Serilog.Events;

namespace LanguageLearning.WebApi.Features.System.Settings;

public sealed record SystemSettingsResponse(
    SystemLogLevel MinimumLogLevel,
    DateTime UpdatedAtUtc,
    Guid? UpdatedByUserId);

public sealed record UpdateSystemSettingsRequest(SystemLogLevel MinimumLogLevel);

public static class SystemSettingsErrors
{
    public const string NotFound = "system_settings.not_found";
    public const string CurrentUserUnavailable = "system_settings.current_user_unavailable";
}

public static class SystemLogLevelMappings
{
    public static LogEventLevel ToSerilogLevel(this SystemLogLevel level) => level switch
    {
        SystemLogLevel.Debug => LogEventLevel.Debug,
        SystemLogLevel.Information => LogEventLevel.Information,
        SystemLogLevel.Warning => LogEventLevel.Warning,
        SystemLogLevel.Error => LogEventLevel.Error,
        SystemLogLevel.Fatal => LogEventLevel.Fatal,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported system log level.")
    };
}
