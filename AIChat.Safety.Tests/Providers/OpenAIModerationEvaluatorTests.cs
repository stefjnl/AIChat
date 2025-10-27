using AIChat.Safety.Contracts;
using AIChat.Safety.Options;
using AIChat.Safety.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AIChat.Safety.Tests.Providers;

/// <summary>
/// Unit tests for the OpenAIModerationEvaluator class.
/// </summary>
public class OpenAIModerationEvaluatorTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OpenAIModerationEvaluator>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly SafetyOptions _testOptions;
    private readonly OpenAIModerationEvaluator _evaluator;

    public OpenAIModerationEvaluatorTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _mockLogger = new Mock<ILogger<OpenAIModerationEvaluator>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _testOptions = CreateTestOptions();
        var optionsWrapper = Options.Create(_testOptions);

        _evaluator = new OpenAIModerationEvaluator(_httpClient, optionsWrapper, _mockLogger.Object, _mockLoggerFactory.Object);
    }

    /// <summary>
    /// Tests that safe content is evaluated correctly and returns a safe result.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithSafeContent_ReturnsSafeResult()
    {
        // Arrange
        var safeText = "Hello, how are you today? I hope you're having a wonderful day!";
        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateTextAsync(safeText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();
        result.Recommendations.Should().Contain("Content is safe to proceed.");
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Provider.Should().Be("OpenAI Moderation");
        result.Metadata.ProcessingTimeMs.Should().BeGreaterThan(0);
        result.Metadata.RequestId.Should().Be(expectedResponse.Id);
        result.Metadata.AdditionalData.Should().ContainKey("Model");
        result.Metadata.AdditionalData["Model"].Should().Be(expectedResponse.Model);

        VerifyLoggedMessages();
    }

    /// <summary>
    /// Tests that empty or null text returns a safe result.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public async Task EvaluateTextAsync_WithEmptyText_ReturnsSafeResult(string? text)
    {
        // Act
        var result = await _evaluator.EvaluateTextAsync(text!);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that when safety is disabled, content is always considered safe.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WhenDisabled_ReturnsSafeResult()
    {
        // Arrange
        var disabledOptions = CreateTestOptions();
        disabledOptions.Enabled = false;
        var optionsWrapper = Options.Create(disabledOptions);
        var disabledEvaluator = new OpenAIModerationEvaluator(_httpClient, optionsWrapper, _mockLogger.Object, _mockLoggerFactory.Object);

        var harmfulText = "This would normally be flagged as harmful content";

        // Act
        var result = await disabledEvaluator.EvaluateTextAsync(harmfulText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that hate content is detected and flagged appropriately.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithHateContent_DetectsHateViolation()
    {
        // Arrange
        var hateText = "I hate people from that country and they should be punished.";
        var expectedResponse = CreateHateModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateTextAsync(hateText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.RiskScore.Should().BeGreaterThan(0);
        result.DetectedCategories.Should().HaveCount(1);
        
        var hateCategory = result.DetectedCategories.First();
        hateCategory.Category.Should().Be(HarmCategory.Hate);
        hateCategory.Severity.Should().BeGreaterThan(0);
        hateCategory.Confidence.Should().BeGreaterThan(0);
        hateCategory.Description.Should().Contain("hate");
        
        result.Recommendations.Should().Contain(r => r.Contains("hate speech"));
        result.Metadata!.Provider.Should().Be("OpenAI Moderation");

        VerifyLoggedMessages();
    }

    /// <summary>
    /// Tests that self-harm content is detected and flagged appropriately.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithSelfHarmContent_DetectsSelfHarmViolation()
    {
        // Arrange
        var selfHarmText = "I want to hurt myself and end my life.";
        var expectedResponse = CreateSelfHarmModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateTextAsync(selfHarmText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.DetectedCategories.Should().HaveCount(1);
        
        var selfHarmCategory = result.DetectedCategories.First();
        selfHarmCategory.Category.Should().Be(HarmCategory.SelfHarm);
        selfHarmCategory.Severity.Should().BeGreaterThan(0);
        
        result.Recommendations.Should().Contain(r => r.Contains("self-harm"));
    }

    /// <summary>
    /// Tests that sexual content is detected and flagged appropriately.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithSexualContent_DetectsSexualViolation()
    {
        // Arrange
        var sexualText = "Explicit sexual content with detailed descriptions.";
        var expectedResponse = CreateSexualModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateTextAsync(sexualText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.DetectedCategories.Should().HaveCount(1);
        
        var sexualCategory = result.DetectedCategories.First();
        sexualCategory.Category.Should().Be(HarmCategory.Sexual);
        sexualCategory.Severity.Should().BeGreaterThan(0);
        
        result.Recommendations.Should().Contain(r => r.Contains("sexually explicit"));
    }

    /// <summary>
    /// Tests that violence content is detected and flagged appropriately.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithViolenceContent_DetectsViolenceViolation()
    {
        // Arrange
        var violenceText = "I want to hurt people and cause physical harm to others.";
        var expectedResponse = CreateViolenceModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateTextAsync(violenceText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.DetectedCategories.Should().HaveCount(1);
        
        var violenceCategory = result.DetectedCategories.First();
        violenceCategory.Category.Should().Be(HarmCategory.Violence);
        violenceCategory.Severity.Should().BeGreaterThan(0);
        
        result.Recommendations.Should().Contain(r => r.Contains("violent material"));
    }

    /// <summary>
    /// Tests that multiple categories of harmful content are all detected.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithMultipleViolations_DetectsAllCategories()
    {
        // Arrange
        var multiHarmText = "I hate everyone and want to hurt them with violence and sexual content.";
        var expectedResponse = CreateMultipleViolationModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateTextAsync(multiHarmText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.DetectedCategories.Should().HaveCountGreaterThan(1);
        
        var detectedCategories = result.DetectedCategories.Select(c => c.Category).ToList();
        detectedCategories.Should().Contain(HarmCategory.Hate);
        detectedCategories.Should().Contain(HarmCategory.Violence);
        detectedCategories.Should().Contain(HarmCategory.Sexual);
        
        // Risk score should be higher for multiple violations
        result.RiskScore.Should().BeGreaterThan(50);
    }

    /// <summary>
    /// Tests that HTTP errors are handled gracefully with fallback behavior.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithHttpError_ReturnsFallbackResult()
    {
        // Arrange
        var errorText = "Some text to test error handling";
        
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        var result = await _evaluator.EvaluateTextAsync(errorText);

        // Assert
        result.Should().NotBeNull();
        
        // With FailOpen behavior, should return safe
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");
    }

    /// <summary>
    /// Tests that HTTP timeout is handled gracefully with fallback behavior.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithTimeout_ReturnsFallbackResult()
    {
        // Arrange
        var timeoutText = "Text that will cause timeout";
        
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(req => throw new TaskCanceledException("Request timed out"));

        // Act
        var result = await _evaluator.EvaluateTextAsync(timeoutText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue(); // FailOpen behavior
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");
    }

    /// <summary>
    /// Tests that batch evaluation works correctly for multiple texts.
    /// </summary>
    [Fact]
    public async Task EvaluateBatchAsync_WithMultipleTexts_ReturnsResultsForAll()
    {
        // Arrange
        var texts = new[]
        {
            "This is safe content",
            "This contains hate speech and should be flagged",
            "This is also safe content",
            "This contains violence and should be flagged"
        };

        var safeResponse = CreateSafeModerationResponse();
        var hateResponse = CreateHateModerationResponse();
        var violenceResponse = CreateViolenceModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(hateResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(violenceResponse));

        // Act
        var results = await _evaluator.EvaluateBatchAsync(texts);

        // Assert
        results.Should().HaveCount(4);
        results[0].IsSafe.Should().BeTrue();
        results[1].IsSafe.Should().BeFalse();
        results[1].DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Hate);
        results[2].IsSafe.Should().BeTrue();
        results[3].IsSafe.Should().BeFalse();
        results[3].DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Violence);
    }

    /// <summary>
    /// Tests that GetSupportedCategories returns the correct set of categories.
    /// </summary>
    [Fact]
    public void GetSupportedCategories_ReturnsCorrectCategories()
    {
        // Act
        var categories = _evaluator.GetSupportedCategories();

        // Assert
        categories.Should().NotBeNull();
        categories.Should().HaveCount(4);
        categories.Should().Contain(HarmCategory.Hate);
        categories.Should().Contain(HarmCategory.SelfHarm);
        categories.Should().Contain(HarmCategory.Sexual);
        categories.Should().Contain(HarmCategory.Violence);
    }

    /// <summary>
    /// Tests that GetProviderName returns the correct provider name.
    /// </summary>
    [Fact]
    public void GetProviderName_ReturnsCorrectName()
    {
        // Act
        var providerName = _evaluator.GetProviderName();

        // Assert
        providerName.Should().Be("OpenAI Moderation");
    }

    /// <summary>
    /// Tests that CreateStreamingEvaluator returns a valid streaming evaluator.
    /// </summary>
    [Fact]
    public void CreateStreamingEvaluator_ReturnsValidEvaluator()
    {
        // Act
        var streamingEvaluator = _evaluator.CreateStreamingEvaluator();

        // Assert
        streamingEvaluator.Should().NotBeNull();
        streamingEvaluator.Should().BeAssignableTo<IStreamingSafetyEvaluator>();
    }

    /// <summary>
    /// Tests that threshold configuration affects violation detection.
    /// </summary>
    [Fact]
    public async Task EvaluateTextAsync_WithHighThreshold_DoesNotFlagLowSeverityContent()
    {
        // Arrange
        var highThresholdOptions = CreateTestOptions();
        highThresholdOptions.InputPolicy.Thresholds[HarmCategory.Hate] = 7; // Very high threshold
        var optionsWrapper = Options.Create(highThresholdOptions);
        var highThresholdEvaluator = new OpenAIModerationEvaluator(_httpClient, optionsWrapper, _mockLogger.Object, _mockLoggerFactory.Object);

        var lowSeverityHateText = "I mildly dislike this situation";
        var response = CreateLowSeverityHateResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(response));

        // Act
        var result = await highThresholdEvaluator.EvaluateTextAsync(lowSeverityHateText);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue(); // Should be safe due to high threshold
        result.DetectedCategories.Should().BeEmpty();
    }

    private void VerifyLoggedMessages()
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting text evaluation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Text evaluation completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static SafetyOptions CreateTestOptions()
    {
        return new SafetyOptions
        {
            Enabled = true,
            Endpoint = "https://api.openai.com/v1/moderations",
            ApiKey = "test-api-key",
            Model = "text-moderation-latest",
            FallbackBehavior = FallbackBehavior.FailOpen,
            InputPolicy = new PolicySettings
            {
                Thresholds = new Dictionary<HarmCategory, int>
                {
                    [HarmCategory.Hate] = 2,
                    [HarmCategory.SelfHarm] = 2,
                    [HarmCategory.Sexual] = 2,
                    [HarmCategory.Violence] = 2
                }
            },
            Resilience = new ResilienceSettings
            {
                TimeoutInMilliseconds = 5000
            }
        };
    }

    private static object CreateSafeModerationResponse()
    {
        return new
        {
            id = "modr-123456789",
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
            id = "modr-hate-123",
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

    private static object CreateSelfHarmModerationResponse()
    {
        return new
        {
            id = "modr-selfharm-123",
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

    private static object CreateSexualModerationResponse()
    {
        return new
        {
            id = "modr-sexual-123",
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

    private static object CreateViolenceModerationResponse()
    {
        return new
        {
            id = "modr-violence-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { violence = true },
                    hate = false,
                    hate_score = 0.2,
                    self_harm = false,
                    self_harm_score = 0.1,
                    sexual = false,
                    sexual_score = 0.15,
                    violence = true,
                    violence_score = 0.75
                }
            }
        };
    }

    private static object CreateMultipleViolationModerationResponse()
    {
        return new
        {
            id = "modr-multiple-123",
            model = "text-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new { hate = true, sexual = true, violence = true },
                    hate = true,
                    hate_score = 0.7,
                    self_harm = false,
                    self_harm_score = 0.1,
                    sexual = true,
                    sexual_score = 0.6,
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
            id = "modr-low-hate-123",
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

    public void Dispose()
    {
        _httpClient?.Dispose();
        _mockHttp?.Dispose();
    }
}