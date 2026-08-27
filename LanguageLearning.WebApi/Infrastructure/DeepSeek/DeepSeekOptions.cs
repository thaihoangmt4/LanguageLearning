namespace LanguageLearning.WebApi.Infrastructure.DeepSeek;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string BaseUrl { get; init; } = "https://api.deepseek.com";
    public string ApiKey { get; init; } = "sk-def467a541f24b0496eedc17f083ee14";
    public string Model { get; init; } = "deepseek-v4-flash";
    public int TimeoutSeconds { get; init; } = 60;
    public int MaxRetryAttempts { get; init; } = 3;
    public int MaxOutputTokens { get; init; } = 8192;

    public void Validate()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
            throw Invalid(nameof(BaseUrl));
        if (string.IsNullOrWhiteSpace(ApiKey)) throw Invalid(nameof(ApiKey));
        if (string.IsNullOrWhiteSpace(Model)) throw Invalid(nameof(Model));
        if (TimeoutSeconds <= 0) throw Invalid(nameof(TimeoutSeconds));
        if (MaxRetryAttempts is < 0 or > 10) throw Invalid(nameof(MaxRetryAttempts));
        if (MaxOutputTokens <= 0) throw Invalid(nameof(MaxOutputTokens));
    }

    private static InvalidOperationException Invalid(string name) =>
        new($"Invalid or missing '{SectionName}:{name}' configuration value.");
}