using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using AIChat.Agents.Providers;
using AIChat.Infrastructure.Configuration;

namespace AIChat.Agents.Tests;

public class ProviderIntegrationTests : IDisposable
{
    private readonly ProviderClientFactory _factory;
    private readonly IConfiguration _config;

    public ProviderIntegrationTests()
    {
        // Build configuration - set base directory to ensure appsettings.json is found
        var basePath = Directory.GetCurrentDirectory();
        _config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<ProviderIntegrationTests>()
            .Build();

        // Debug: Check if configuration is loaded
        var providersSection = _config.GetSection("Providers");
        Console.WriteLine($"Providers section exists: {providersSection.Exists()}");
        
        // Create configuration object using Get<>() which should work better
        var providersConfig = _config.GetSection("Providers").Get<ProvidersConfiguration>();
        
        // If Get doesn't work, try manual construction
        if (providersConfig?.Providers == null || providersConfig.Providers.Count == 0)
        {
            providersConfig = new ProvidersConfiguration();
            
            // Manually bind each provider
            var openRouterSection = providersSection.GetSection("OpenRouter");
            if (openRouterSection.Exists())
            {
                providersConfig.Providers["OpenRouter"] = new ProviderSettings
                {
                    BaseUrl = openRouterSection["BaseUrl"] ?? "",
                    DefaultModel = openRouterSection["DefaultModel"] ?? "",
                    ApiKey = _config["Providers:OpenRouter:ApiKey"]
                };
            }
            
            var nanoGptSection = providersSection.GetSection("NanoGPT");
            if (nanoGptSection.Exists())
            {
                providersConfig.Providers["NanoGPT"] = new ProviderSettings
                {
                    BaseUrl = nanoGptSection["BaseUrl"] ?? "",
                    DefaultModel = nanoGptSection["DefaultModel"] ?? "",
                    ApiKey = _config["Providers:NanoGPT:ApiKey"]
                };
            }
            
            var lmStudioSection = providersSection.GetSection("LMStudio");
            if (lmStudioSection.Exists())
            {
                providersConfig.Providers["LMStudio"] = new ProviderSettings
                {
                    BaseUrl = lmStudioSection["BaseUrl"] ?? "",
                    DefaultModel = lmStudioSection["DefaultModel"] ?? "",
                    ApiKey = _config["Providers:LMStudio:ApiKey"]
                };
            }
        }
        
        Console.WriteLine($"Number of providers configured: {providersConfig.Providers.Count}");
        
        if (providersConfig == null || !providersConfig.Providers.Any())
        {
            throw new InvalidOperationException("Provider configuration is missing or empty. Check appsettings.json and user secrets.");
        }

        // Create factory
        _factory = new ProviderClientFactory(providersConfig);
    }

    [Fact]
    public async Task OpenRouter_ShouldReturnValidResponse()
    {
        // Arrange
        var client = _factory.CreateChatClient("OpenRouter");
        var messages = new[] 
        { 
            new ChatMessage(ChatRole.User, "Hello! Please respond with a brief greeting and tell me your name.") 
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        Assert.True(response.Usage?.TotalTokenCount > 0, "Token count should be greater than 0");
        
        // Log response for verification
        Console.WriteLine($"OpenRouter Response: {response.Text}");
        Console.WriteLine($"Tokens used: {response.Usage?.TotalTokenCount}");
    }

    [Fact]
    public async Task NanoGPT_ShouldReturnValidResponse()
    {
        // Arrange
        var client = _factory.CreateChatClient("NanoGPT");
        var messages = new[] 
        { 
            new ChatMessage(ChatRole.User, "Hello! Please respond with a brief greeting and tell me your name.") 
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        Assert.True(response.Usage?.TotalTokenCount > 0, "Token count should be greater than 0");
        
        // Log response for verification
        Console.WriteLine($"NanoGPT Response: {response.Text}");
        Console.WriteLine($"Tokens used: {response.Usage?.TotalTokenCount}");
    }

    [Fact]
    public async Task LMStudio_ShouldReturnValidResponse_WhenServerIsRunning()
    {
        // Arrange
        var client = _factory.CreateChatClient("LMStudio");
        var messages = new[] 
        { 
            new ChatMessage(ChatRole.User, "Hello! Please respond with a brief greeting.") 
        };

        // Act & Assert
        try
        {
            var response = await client.GetResponseAsync(messages);
            
            Assert.NotNull(response);
            Assert.NotNull(response.Text);
            Assert.NotEmpty(response.Text);
            Assert.True(response.Usage?.TotalTokenCount > 0, "Token count should be greater than 0");
            
            // Log response for verification
            Console.WriteLine($"LM Studio Response: {response.Text}");
            Console.WriteLine($"Tokens used: {response.Usage?.TotalTokenCount}");
        }
        catch (HttpRequestException ex)
        {
            // LM Studio might not be running locally
            Console.WriteLine($"LM Studio test skipped: {ex.Message}");
            Assert.True(true, "LM Studio test skipped - server not running");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LM Studio test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task OpenRouter_ShouldHandleMultipleMessages()
    {
        // Arrange
        var client = _factory.CreateChatClient("OpenRouter");
        var messages = new[] 
        { 
            new ChatMessage(ChatRole.User, "My name is Alice. What is my name?"),
            new ChatMessage(ChatRole.Assistant, "Your name is Alice."),
            new ChatMessage(ChatRole.User, "What did I just tell you?")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        Assert.Contains("Alice", response.Text, StringComparison.OrdinalIgnoreCase);
        
        Console.WriteLine($"OpenRouter Conversation Test Response: {response.Text}");
    }

    [Fact]
    public async Task NanoGPT_ShouldHandleMultipleMessages()
    {
        // Arrange
        var client = _factory.CreateChatClient("NanoGPT");
        var messages = new[] 
        { 
            new ChatMessage(ChatRole.User, "My name is Bob. What is my name?"),
            new ChatMessage(ChatRole.Assistant, "Your name is Bob."),
            new ChatMessage(ChatRole.User, "What did I just tell you?")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        Assert.Contains("Bob", response.Text, StringComparison.OrdinalIgnoreCase);
        
        Console.WriteLine($"NanoGPT Conversation Test Response: {response.Text}");
    }

    [Fact]
    public void ProviderClientFactory_ShouldThrowException_ForUnknownProvider()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateChatClient("UnknownProvider"));
    }

    [Fact]
    public void ProviderClientFactory_ShouldThrowException_ForUnconfiguredProvider()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateChatClient(""));
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}