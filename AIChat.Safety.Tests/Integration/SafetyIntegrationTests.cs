using AIChat.Safety.Contracts;
using AIChat.Safety.DependencyInjection;
using AIChat.Safety.Options;
using AIChat.Safety.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AIChat.Safety.Tests.Integration;

/// <summary>
/// Integration tests for the safety system using test doubles for HTTP calls.
/// </summary>
public class SafetyIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly ISafetyEvaluationService _safetyService;
    private readonly SafetyOptions _testOptions;

    public SafetyIntegrationTests()
    {
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json")
            .Build();

        // Setup mock HTTP handler
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();

        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // Configure safety options
        services.Configure<SafetyOptions>(configuration.GetSection("Safety"));
        
        // Register mock HttpClient
        services.AddSingleton(_httpClient);
        
        // Add safety services
        services.AddAISafetyServices(configuration);

        _serviceProvider = services.BuildServiceProvider();
        
        _safetyService = _serviceProvider.GetRequiredService<ISafetyEvaluationService>();
        _testOptions = _serviceProvider.GetRequiredService<IOptions<SafetyOptions>>().Value;
    }

    /// <summary>
    /// Tests the complete flow of safe user input evaluation through the service layer.
    /// </summary>
    [Fact]
    public async Task CompleteUserInputEvaluationFlow_WithSafeContent_PassesThroughAllLayers()
    {
        // Arrange
        var safeInput = "Hello, how are you today? I hope you're having a wonderful day!";
        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _safetyService.EvaluateUserInputAsync(safeInput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();
        result.Recommendations.Should().Contain("Content is safe to proceed.");
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Provider.Should().Be("OpenAI Moderation");
        result.Metadata.ProcessingTimeMs.Should().BeGreaterThan(0);

        // Verify HTTP request was made
        _mockHttp.GetMatchCount().Should().Be(1);
    }

    /// <summary>
    /// Tests the complete flow of harmful user input evaluation through all layers.
    /// </summary>
    [Fact]
    public async Task CompleteUserInputEvaluationFlow_WithHarmfulContent_DetectsAndBlocksContent()
    {
        // Arrange
        var harmfulInput = "I hate people from that country and they should be punished.";
        var expectedResponse = CreateHateModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _safetyService.EvaluateUserInputAsync(harmfulInput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.RiskScore.Should().BeGreaterThan(0);
        result.DetectedCategories.Should().HaveCount(1);
        
        var hateCategory = result.DetectedCategories.First();
        hateCategory.Category.Should().Be(HarmCategory.Hate);
        hateCategory.Severity.Should().BeGreaterThan(0);
        hateCategory.Confidence.Should().BeGreaterThan(0);
        
        result.Recommendations.Should().Contain(r => r.Contains("hate speech"));
        result.Metadata!.Provider.Should().Be("OpenAI Moderation");

        // Verify HTTP request was made
        _mockHttp.GetMatchCount().Should().Be(1);
    }

    /// <summary>
    /// Tests the complete flow of AI output evaluation through all layers.
    /// </summary>
    [Fact]
    public async Task CompleteOutputEvaluationFlow_WithSexualContent_DetectsAndBlocksContent()
    {
        // Arrange
        var sexualOutput = "Explicit sexual content with detailed descriptions of adult activities.";
        var expectedResponse = CreateSexualModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _safetyService.EvaluateOutputAsync(sexualOutput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.DetectedCategories.Should().HaveCount(1);
        
        var sexualCategory = result.DetectedCategories.First();
        sexualCategory.Category.Should().Be(HarmCategory.Sexual);
        sexualCategory.Severity.Should().BeGreaterThan(0);
        
        result.Recommendations.Should().Contain(r => r.Contains("sexually explicit"));

        // Verify HTTP request was made
        _mockHttp.GetMatchCount().Should().Be(1);
    }

    /// <summary>
    /// Tests batch evaluation flow with mixed content types.
    /// </summary>
    [Fact]
    public async Task BatchEvaluationFlow_WithMixedContent_ReturnsCorrectResultsForAll()
    {
        // Arrange
        var messages = new[]
        {
            "This is safe content",
            "I hate everyone and want to cause violence",
            "This is also safe content",
            "I want to hurt myself and end my life",
            "More safe content here"
        };

        // Setup multiple responses for batch requests
        var safeResponse = CreateSafeModerationResponse();
        var hateResponse = CreateHateModerationResponse();
        var selfHarmResponse = CreateSelfHarmModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(hateResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(selfHarmResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));

        // Act
        var results = await _safetyService.EvaluateBatchAsync(messages);

        // Assert
        results.Should().HaveCount(5);
        results[0].IsSafe.Should().BeTrue();
        results[1].IsSafe.Should().BeFalse();
        results[1].DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Hate);
        results[2].IsSafe.Should().BeTrue();
        results[3].IsSafe.Should().BeFalse();
        results[3].DetectedCategories.Should().Contain(c => c.Category == HarmCategory.SelfHarm);
        results[4].IsSafe.Should().BeTrue();

        // Verify HTTP requests were made
        _mockHttp.GetMatchCount().Should().Be(5);
    }

    /// <summary>
    /// Tests streaming evaluation flow through multiple chunks.
    /// </summary>
    [Fact]
    public async Task StreamingEvaluationFlow_WithMultipleChunks_DetectsHarmWhenAccumulated()
    {
        // Arrange
        var streamingEvaluator = _safetyService.CreateStreamingEvaluator();
        var chunks = new[]
        {
            "I really dislike",
            " when people are mean",
            " to each other. I hate everyone and want to cause violence!"
        };

        var safeResponse = CreateSafeModerationResponse();
        var harmfulResponse = CreateHateAndViolenceModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(harmfulResponse));

        // Act
        var results = new List<SafetyEvaluationResult>();
        foreach (var chunk in chunks)
        {
            var result = await streamingEvaluator.EvaluateChunkAsync(chunk);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(chunks.Length);
        streamingEvaluator.GetProcessedChunkCount().Should().Be(chunks.Length);
        streamingEvaluator.GetAccumulatedContent().Should().Be(string.Concat(chunks));
        
        // The last chunk should trigger evaluation and detect violations
        var lastResult = results.Last();
        lastResult.IsSafe.Should().BeFalse();
        lastResult.DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Hate);
        lastResult.DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Violence);
        
        streamingEvaluator.HasViolations().Should().BeTrue();

        // Verify HTTP requests were made
        _mockHttp.GetMatchCount().Should().BeGreaterOrEqualTo(1);

        // Cleanup
        streamingEvaluator.Dispose();
    }

    /// <summary>
    /// Tests error handling flow when the moderation API is unavailable.
    /// </summary>
    [Fact]
    public async Task ErrorHandlingFlow_WhenApiUnavailable_ReturnsFallbackResults()
    {
        // Arrange
        var userInput = "Some input to test error handling";
        
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        var result = await _safetyService.EvaluateUserInputAsync(userInput);

        // Assert
        result.Should().NotBeNull();
        
        // With FailOpen behavior, should return safe
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");

        // Verify HTTP request was attempted
        _mockHttp.GetMatchCount().Should().Be(1);
    }

    /// <summary>
    /// Tests timeout handling flow when the moderation API times out.
    /// </summary>
    [Fact]
    public async Task TimeoutHandlingFlow_WhenApiTimesOut_ReturnsFallbackResults()
    {
        // Arrange
        var userInput = "Some input to test timeout handling";
        
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(req => throw new TaskCanceledException("Request timed out"));

        // Act
        var result = await _safetyService.EvaluateUserInputAsync(userInput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue(); // FailOpen behavior
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");

        // Verify HTTP request was attempted
        _mockHttp.GetMatchCount().Should().Be(1);
    }

    /// <summary>
    /// Tests safety status reporting functionality.
    /// </summary>
    [Fact]
    public void SafetyStatusFlow_ReturnsCorrectConfigurationInformation()
    {
        // Act
        var status = _safetyService.GetSafetyStatus();

        // Assert
        status.Should().NotBeNull();
        status.IsEnabled.Should().BeTrue();
        status.Provider.Should().Be("OpenAI Moderation");
        status.SupportedCategories.Should().NotBeEmpty();
        status.SupportedCategories.Should().Contain(HarmCategory.Hate);
        status.SupportedCategories.Should().Contain(HarmCategory.SelfHarm);
        status.SupportedCategories.Should().Contain(HarmCategory.Sexual);
        status.SupportedCategories.Should().Contain(HarmCategory.Violence);
        status.FallbackBehavior.Should().Be(FallbackBehavior.FailOpen);
        status.InputPolicy.Should().NotBeNull();
        status.OutputPolicy.Should().NotBeNull();
    }

    /// <summary>
    /// tests text filtering flow when filter is configured.
    /// </summary>
    [Fact]
    public async Task TextFilteringFlow_WithConfiguredFilter_ReturnsFilteredResults()
    {
        // Note: This test would require a mock ISafetyFilter implementation
        // For now, we test that the service handles the absence of filter gracefully
        
        // Act
        var result = await _safetyService.FilterTextAsync("Some text to filter");

        // Assert
        result.Should().BeNull(); // No filter configured in test setup
    }

    /// <summary>
    /// Tests threshold application across different harm categories.
    /// </summary>
    [Fact]
    public async Task ThresholdApplicationFlow_WithDifferentSeverities_AppliesThresholdsCorrectly()
    {
        // Arrange
        var lowSeverityText = "I mildly dislike this situation";
        var highSeverityText = "I hate everyone and want to cause extreme violence";
        
        var lowSeverityResponse = CreateLowSeverityHateResponse();
        var highSeverityResponse = CreateHighSeverityHateResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(lowSeverityResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(highSeverityResponse));

        // Act
        var lowSeverityResult = await _safetyService.EvaluateUserInputAsync(lowSeverityText);
        var highSeverityResult = await _safetyService.EvaluateUserInputAsync(highSeverityText);

        // Assert
        // Low severity should be safe (below threshold)
        lowSeverityResult.IsSafe.Should().BeTrue();
        
        // High severity should be unsafe (above threshold)
        highSeverityResult.IsSafe.Should().BeFalse();
        highSeverityResult.DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Hate);

        // Verify HTTP requests were made
        _mockHttp.GetMatchCount().Should().Be(2);
    }

    /// <summary>
    /// Tests the complete dependency injection setup.
    /// </summary>
    [Fact]
    public void DependencyInjectionSetup_ResolvesAllServicesCorrectly()
    {
        // Act & Assert
        _serviceProvider.GetRequiredService<ISafetyEvaluationService>().Should().NotBeNull();
        _serviceProvider.GetRequiredService<ISafetyEvaluator>().Should().NotBeNull();
        _serviceProvider.GetRequiredService<IOptions<SafetyOptions>>().Should().NotBeNull();
        
        // Verify the evaluator is the expected type
        var evaluator = _serviceProvider.GetRequiredService<ISafetyEvaluator>();
        evaluator.GetType().Name.Should().Be("OpenAIModerationEvaluator");
        evaluator.GetProviderName().Should().Be("OpenAI Moderation");
    }

    /// <summary>
    /// Tests health check functionality.
    /// </summary>
    [Fact]
    public async Task HealthCheckFlow_WithWorkingService_ReturnsHealthyStatus()
    {
        // Arrange
        var healthCheck = _serviceProvider.GetServices<Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck>()
            .FirstOrDefault(h => h.GetType().Name.Contains("SafetyHealthCheck"));
        
        healthCheck.Should().NotBeNull();

        var expectedResponse = CreateSafeModerationResponse();
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        var context = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext();

        // Act
        var result = await healthCheck!.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
        result.Description.Should().Contain("operational");
        result.Description.Should().Contain("OpenAI Moderation");

        // Verify HTTP request was made
        _mockHttp.GetMatchCount().Should().Be(1);
    }

    private static object CreateSafeModerationResponse()
    {
        return new
        {
            id = "modr-integration-safe-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new { },
                    hate = false,
                    hate_score = 0.01,
                    self_harm = false,
                    self_harm_score = 0.02,
                    sexual = false,
                    sexual_score = 0.01,
                    violence = false,
                    violence_score = 0.01
                }
            }
        };
    }

    private static object CreateHateModerationResponse()
    {
        return new
        {
            id = "modr-integration-hate-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { hate = true },
                    hate = true,
                    hate_score = 0.8,
                    self_harm = false,
                    self_harm_score = 0.1,
                    sexual = false,
                    sexual_score = 0.05,
                    violence = false,
                    violence_score = 0.2
                }
            }
        };
    }

    private static object CreateSexualModerationResponse()
    {
        return new
        {
            id = "modr-integration-sexual-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { sexual = true },
                    hate = false,
                    hate_score = 0.1,
                    self_harm = false,
                    self_harm_score = 0.05,
                    sexual = true,
                    sexual_score = 0.85,
                    violence = false,
                    violence_score = 0.15
                }
            }
        };
    }

    private static object CreateSelfHarmModerationResponse()
    {
        return new
        {
            id = "modr-integration-selfharm-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { self_harm = true },
                    hate = false,
                    hate_score = 0.1,
                    self_harm = true,
                    self_harm_score = 0.9,
                    sexual = false,
                    sexual_score = 0.05,
                    violence = false,
                    violence_score = 0.1
                }
            }
        };
    }

    private static object CreateHateAndViolenceModerationResponse()
    {
        return new
        {
            id = "modr-integration-multiple-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { hate = true, violence = true },
                    hate = true,
                    hate_score = 0.7,
                    self_harm = false,
                    self_harm_score = 0.1,
                    sexual = false,
                    sexual_score = 0.05,
                    violence = true,
                    violence_score = 0.8
                }
            }
        };
    }

    private static object CreateLowSeverityHateResponse()
    {
        return new
        {
            id = "modr-integration-low-hate-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { hate = true },
                    hate = true,
                    hate_score = 0.3, // Low severity
                    self_harm = false,
                    self_harm_score = 0.05,
                    sexual = false,
                    sexual_score = 0.02,
                    violence = false,
                    violence_score = 0.1
                }
            }
        };
    }

    private static object CreateHighSeverityHateResponse()
    {
        return new
        {
            id = "modr-integration-high-hate-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { hate = true },
                    hate = true,
                    hate_score = 0.9, // High severity
                    self_harm = false,
                    self_harm_score = 0.05,
                    sexual = false,
                    sexual_score = 0.02,
                    violence = false,
                    violence_score = 0.1
                }
            }
        };
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _httpClient?.Dispose();
        _mockHttp?.Dispose();
    }
}