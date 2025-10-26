namespace AIChat.Infrastructure.Configuration;

public class ProvidersConfiguration
{
    public Dictionary<string, ProviderSettings> Providers { get; set; } = new();
}

public class ProviderSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string DefaultModel { get; set; } = string.Empty;
}