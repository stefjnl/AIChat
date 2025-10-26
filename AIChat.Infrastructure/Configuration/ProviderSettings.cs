namespace AIChat.Infrastructure.Configuration;

public class ProvidersConfiguration
{
    public Dictionary<string, ProviderSettings> Providers { get; set; } = new();
}

public class ProviderSettings
{
    public required string BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public required string DefaultModel { get; set; }
}