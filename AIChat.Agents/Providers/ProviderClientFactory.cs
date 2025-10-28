using AIChat.Infrastructure.Configuration;
using AIChat.Safety.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using System.ClientModel;

namespace AIChat.Agents.Providers;

public class ProviderClientFactory
{
    private readonly ProvidersConfiguration _config;
    private readonly IServiceProvider _serviceProvider;

    public ProviderClientFactory(
        ProvidersConfiguration config,
        IServiceProvider serviceProvider)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IChatClient CreateChatClient(string providerName)
    {
        if (!_config.Providers.TryGetValue(providerName, out var settings))
       throw new ArgumentException($"Provider '{providerName}' not configured");

        // Create the base client
      var baseClient = providerName switch
        {
            "OpenRouter" => CreateOpenRouterClient(settings),
            "NanoGPT" => CreateNanoGPTClient(settings),
        "LMStudio" => CreateLMStudioClient(settings),
            _ => throw new ArgumentException($"Unknown provider: {providerName}")
        };

        // Wrap with safety middleware
        return baseClient.UseSafetyMiddleware(_serviceProvider);
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