using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using AIChat.Infrastructure.Configuration;
using System.ClientModel;

namespace AIChat.Agents.Providers;

public class ProviderClientFactory
{
    private readonly ProvidersConfiguration _config;

    public ProviderClientFactory(IOptions<ProvidersConfiguration> config)
    {
        _config = config.Value;
    }

    public IChatClient CreateChatClient(string providerName)
    {
        if (!_config.Providers.TryGetValue(providerName, out var settings))
            throw new ArgumentException($"Provider '{providerName}' not configured");

        return providerName switch
        {
            "OpenRouter" => CreateOpenRouterClient(settings),
            "NanoGPT" => CreateNanoGPTClient(settings),
            "LMStudio" => CreateLMStudioClient(settings),
            _ => throw new ArgumentException($"Unknown provider: {providerName}")
        };
    }

    private IChatClient CreateOpenRouterClient(ProviderSettings settings)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(settings.ApiKey ?? ""),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(settings.BaseUrl)
            });

        return client
            .GetChatClient(settings.DefaultModel)
            .AsIChatClient();
    }

    private IChatClient CreateNanoGPTClient(ProviderSettings settings)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(settings.ApiKey ?? ""),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(settings.BaseUrl)
            });

        return client
            .GetChatClient(settings.DefaultModel)
            .AsIChatClient();
    }

    private IChatClient CreateLMStudioClient(ProviderSettings settings)
    {
        // LM Studio doesn't require an API key for local usage
        var client = new OpenAIClient(
            new ApiKeyCredential("not-needed"),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(settings.BaseUrl)
            });

        return client
            .GetChatClient(settings.DefaultModel)
            .AsIChatClient();
    }
}