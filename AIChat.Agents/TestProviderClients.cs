using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using AIChat.Agents.Providers;
using AIChat.Infrastructure.Configuration;

namespace AIChat.Agents.Testing;

public class ProviderTestHarness
{
    private readonly ProviderClientFactory _factory;
    private readonly IConfiguration _config;

    public ProviderTestHarness(ProviderClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public async Task TestAllProviders()
    {
        Console.WriteLine("Testing all AI providers...\n");

        var providers = new[] { "OpenRouter", "NanoGPT", "LMStudio" };

        foreach (var provider in providers)
        {
            await TestProvider(provider);
        }
    }

    public async Task TestProvider(string providerName)
    {
        Console.WriteLine($"=== Testing {providerName} ===");
        
        try
        {
            var client = _factory.CreateChatClient(providerName);
            
            var messages = new[] 
            { 
                new ChatMessage(ChatRole.User, "Hello! Please respond with a brief greeting.") 
            };

            Console.WriteLine("Sending message...");
            var response = await client.GetResponseAsync(messages);
            
            Console.WriteLine($"Response: {response.Text}");
            Console.WriteLine($"Input Tokens: {response.Usage?.InputTokenCount ?? 0}");
            Console.WriteLine($"Output Tokens: {response.Usage?.OutputTokenCount ?? 0}");
            Console.WriteLine($"Total Tokens: {response.Usage?.TotalTokenCount ?? 0}");
            Console.WriteLine($"{providerName} test: PASSED\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{providerName} test: FAILED");
            Console.WriteLine($"Error: {ex.Message}\n");
        }
    }

    public static async Task RunTests()
    {
        // Build configuration
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<Program>()
            .Build();

        // Create configuration object
        var providersConfig = new ProvidersConfiguration();
        config.GetSection("Providers").Bind(providersConfig);
        
        if (providersConfig == null || !providersConfig.Providers.Any())
        {
            Console.WriteLine("No provider configuration found. Please check appsettings.json");
            return;
        }

        // Create IOptions wrapper for providersConfig
        var options = Options.Create(providersConfig);

        // Create factory
        var factory = new ProviderClientFactory(options, null!);
        
        // Run tests
        var harness = new ProviderTestHarness(factory, config);
        await harness.TestAllProviders();
    }
}