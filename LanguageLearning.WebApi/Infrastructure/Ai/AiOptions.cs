namespace LanguageLearning.WebApi.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    public const string DefaultModel = "gemini-2.5-flash-lite";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public string Model { get; init; } = DefaultModel;
    public int TimeoutSeconds { get; init; } = 60;
    public int MaxRetryAttempts { get; init; } = 3;
    public int MaxOutputTokens { get; init; } = 8192;

    public string? ConfigurationError()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return $"{SectionName}:{nameof(ApiKey)}";
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return $"{SectionName}:{nameof(BaseUrl)}";
        if (string.IsNullOrWhiteSpace(Model)) return $"{SectionName}:{nameof(Model)}";
        if (TimeoutSeconds <= 0) return $"{SectionName}:{nameof(TimeoutSeconds)}";
        if (MaxRetryAttempts is < 0 or > 10) return $"{SectionName}:{nameof(MaxRetryAttempts)}";
        if (MaxOutputTokens <= 0) return $"{SectionName}:{nameof(MaxOutputTokens)}";
        return null;
    }
}
