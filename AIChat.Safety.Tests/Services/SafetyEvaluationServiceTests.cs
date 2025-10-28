using AIChat.Safety.Contracts;
using AIChat.Safety.Options;
using AIChat.Safety.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AIChat.Safety.Tests.Services;

/// <summary>
/// Unit tests for the SafetyEvaluationService class.
/// </summary>
public class SafetyEvaluationServiceTests : IDisposable
{
    private readonly Mock<ISafetyEvaluator> _mockEvaluator;
    private readonly Mock<ISafetyFilter> _mockFilter;
    private readonly Mock<ILogger<SafetyEvaluationService>> _mockLogger;
    private readonly SafetyOptions _testOptions;
    private readonly SafetyEvaluationService _service;

    public SafetyEvaluationServiceTests()
    {
        _mockEvaluator = new Mock<ISafetyEvaluator>();
        _mockFilter = new Mock<ISafetyFilter>();
        _mockLogger = new Mock<ILogger<SafetyEvaluationService>>();

        _testOptions = CreateTestOptions();
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_testOptions);

        _service = new SafetyEvaluationService(_mockEvaluator.Object, optionsWrapper, _mockLogger.Object, _mockFilter.Object);
    }

    /// <summary>
    /// Tests that safe user input is evaluated correctly.
    /// </summary>
    [Fact]
    public async Task EvaluateUserInputAsync_WithSafeInput_ReturnsSafeResult()
    {
        // Arrange
        var safeInput = "Hello, how are you today?";
        var expectedResult = SafetyEvaluationResult.Safe;

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(safeInput, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.EvaluateUserInputAsync(safeInput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();

        _mockEvaluator.Verify(x => x.EvaluateTextAsync(safeInput, It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyDebugLogging("Evaluating user input for safety");
        VerifyDebugLogging("User input evaluated as safe");
    }

    /// <summary>
    /// Tests that harmful user input is detected and logged appropriately.
    /// </summary>
    [Fact]
    public async Task EvaluateUserInputAsync_WithHarmfulInput_ReturnsUnsafeResultAndLogsViolation()
    {
        // Arrange
        var harmfulInput = "I hate everyone and want to cause violence";
        var expectedResult = SafetyEvaluationResult.Unsafe(HarmCategory.Hate, 3);
        expectedResult.RiskScore = 75;

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(harmfulInput, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.EvaluateUserInputAsync(harmfulInput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.RiskScore.Should().Be(75);
        result.DetectedCategories.Should().HaveCount(1);
        result.DetectedCategories.First().Category.Should().Be(HarmCategory.Hate);

        _mockEvaluator.Verify(x => x.EvaluateTextAsync(harmfulInput, It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyWarningLogging("User input blocked due to");
        VerifyDebugLogging("Evaluating user input for safety");
    }

    /// <summary>
    /// Tests that empty or null user input returns a safe result.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public async Task EvaluateUserInputAsync_WithEmptyInput_ReturnsSafeResult(string? input)
    {
        // Act
        var result = await _service.EvaluateUserInputAsync(input!);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();

        // Should not call the evaluator for empty input
        _mockEvaluator.Verify(x => x.EvaluateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that when safety is disabled, user input is always considered safe.
    /// </summary>
    [Fact]
    public async Task EvaluateUserInputAsync_WhenDisabled_ReturnsSafeResult()
    {
        // Arrange
        var disabledOptions = CreateTestOptions();
        disabledOptions.Enabled = false;
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(disabledOptions);
        var disabledService = new SafetyEvaluationService(_mockEvaluator.Object, optionsWrapper, _mockLogger.Object, _mockFilter.Object);

        var harmfulInput = "This would normally be flagged as harmful";

        // Act
        var result = await disabledService.EvaluateUserInputAsync(harmfulInput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();

        // Should not call the evaluator when disabled
        _mockEvaluator.Verify(x => x.EvaluateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that safe AI output is evaluated correctly.
    /// </summary>
    [Fact]
    public async Task EvaluateOutputAsync_WithSafeOutput_ReturnsSafeResult()
    {
        // Arrange
        var safeOutput = "Here is some helpful information for you.";
        var expectedResult = SafetyEvaluationResult.Safe;

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(safeOutput, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.EvaluateOutputAsync(safeOutput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();

        _mockEvaluator.Verify(x => x.EvaluateTextAsync(safeOutput, It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyDebugLogging("Evaluating AI output for safety");
        VerifyDebugLogging("AI output evaluated as safe");
    }

    /// <summary>
    /// Tests that harmful AI output is detected and logged appropriately.
    /// </summary>
    [Fact]
    public async Task EvaluateOutputAsync_WithHarmfulOutput_ReturnsUnsafeResultAndLogsViolation()
    {
        // Arrange
        var harmfulOutput = "I want to hurt myself and end my life";
        var expectedResult = SafetyEvaluationResult.Unsafe(HarmCategory.SelfHarm, 4);
        expectedResult.RiskScore = 80;

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(harmfulOutput, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.EvaluateOutputAsync(harmfulOutput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeFalse();
        result.RiskScore.Should().Be(80);
        result.DetectedCategories.Should().HaveCount(1);
        result.DetectedCategories.First().Category.Should().Be(HarmCategory.SelfHarm);

        _mockEvaluator.Verify(x => x.EvaluateTextAsync(harmfulOutput, It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyWarningLogging("AI output blocked due to");
        VerifyDebugLogging("Evaluating AI output for safety");
    }

    /// <summary>
    /// Tests that empty or null AI output returns a safe result.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public async Task EvaluateOutputAsync_WithEmptyOutput_ReturnsSafeResult(string? output)
    {
        // Act
        var result = await _service.EvaluateOutputAsync(output!);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();

        // Should not call the evaluator for empty output
        _mockEvaluator.Verify(x => x.EvaluateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that batch evaluation works correctly for multiple messages.
    /// </summary>
    [Fact]
    public async Task EvaluateBatchAsync_WithMultipleMessages_ReturnsResultsForAll()
    {
        // Arrange
        var messages = new[]
        {
            "This is safe content",
            "This contains hate speech",
            "This is also safe content",
            "This contains violence"
        };

        var expectedResults = new[]
        {
            SafetyEvaluationResult.Safe,
            SafetyEvaluationResult.Unsafe(HarmCategory.Hate, 3),
            SafetyEvaluationResult.Safe,
            SafetyEvaluationResult.Unsafe(HarmCategory.Violence, 4)
        };

        _mockEvaluator
            .Setup(x => x.EvaluateBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await _service.EvaluateBatchAsync(messages);

        // Assert
        results.Should().HaveCount(4);
        results[0].IsSafe.Should().BeTrue();
        results[1].IsSafe.Should().BeFalse();
        results[1].DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Hate);
        results[2].IsSafe.Should().BeTrue();
        results[3].IsSafe.Should().BeFalse();
        results[3].DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Violence);

        _mockEvaluator.Verify(x => x.EvaluateBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyDebugLogging("Starting batch safety evaluation");
        VerifyWarningLogging("Batch evaluation completed with");
    }

    /// <summary>
    /// Tests that batch evaluation with empty or null messages returns empty results.
    /// </summary>
    [Fact]
    public async Task EvaluateBatchAsync_WithEmptyMessages_ReturnsEmptyResults()
    {
        // Arrange
        var emptyMessages = Array.Empty<string>();

        // Act
        var results = await _service.EvaluateBatchAsync(emptyMessages);

        // Assert
        results.Should().BeEmpty();

        // Should not call the evaluator for empty batch
        _mockEvaluator.Verify(x => x.EvaluateBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that batch evaluation filters out null or whitespace messages.
    /// </summary>
    [Fact]
    public async Task EvaluateBatchAsync_WithMixedMessages_FiltersEmptyOnes()
    {
        // Arrange
        var mixedMessages = new[]
        {
            "Valid message",
            "",
            "   ",
            null!,
            "Another valid message"
        };

        var expectedResults = new[]
        {
            SafetyEvaluationResult.Safe,
            SafetyEvaluationResult.Safe
        };

        _mockEvaluator
            .Setup(x => x.EvaluateBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await _service.EvaluateBatchAsync(mixedMessages);

        // Assert
        results.Should().HaveCount(2); // Only valid messages should be evaluated

        _mockEvaluator.Verify(x => x.EvaluateBatchAsync(It.Is<IEnumerable<string>>(msgs => 
            msgs.Count() == 2 && 
            msgs.Contains("Valid message") && 
            msgs.Contains("Another valid message")), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that CreateStreamingEvaluator returns a valid streaming evaluator.
    /// </summary>
    [Fact]
    public void CreateStreamingEvaluator_ReturnsValidEvaluator()
    {
        // Arrange
        var expectedStreamingEvaluator = new Mock<IStreamingSafetyEvaluator>().Object;
        _mockEvaluator.Setup(x => x.CreateStreamingEvaluator()).Returns(expectedStreamingEvaluator);

        // Act
        var result = _service.CreateStreamingEvaluator();

        // Assert
        result.Should().Be(expectedStreamingEvaluator);
        _mockEvaluator.Verify(x => x.CreateStreamingEvaluator(), Times.Once);
        
        VerifyDebugLogging("Creating a new streaming safety evaluator");
    }

    /// <summary>
    /// Tests that CreateStreamingEvaluator returns no-op evaluator when safety is disabled.
    /// </summary>
    [Fact]
    public void CreateStreamingEvaluator_WhenDisabled_ReturnsNoOpEvaluator()
    {
        // Arrange
        var disabledOptions = CreateTestOptions();
        disabledOptions.Enabled = false;
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(disabledOptions);
        var disabledService = new SafetyEvaluationService(_mockEvaluator.Object, optionsWrapper, _mockLogger.Object, _mockFilter.Object);

        // Act
        var result = disabledService.CreateStreamingEvaluator();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IStreamingSafetyEvaluator>();
        
        // Should not call the underlying evaluator when disabled
        _mockEvaluator.Verify(x => x.CreateStreamingEvaluator(), Times.Never);
        
        VerifyDebugLogging("Safety is disabled, creating no-op streaming evaluator");
    }

    /// <summary>
    /// Tests that FilterTextAsync returns filtered text when filter is available.
    /// </summary>
    [Fact]
    public async Task FilterTextAsync_WithFilter_ReturnsFilteredResult()
    {
        // Arrange
        var textToFilter = "This contains some harmful content";
        var expectedFilteredResult = new FilteredTextResult
        {
            OriginalText = textToFilter,
            FilteredText = "This contains some [REDACTED] content",
            WasFiltered = true,
            AppliedActions = { new FilteringAction { Action = FilterActionType.Redact, Category = HarmCategory.Hate } }
        };

        _mockFilter
            .Setup(x => x.FilterTextAsync(textToFilter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFilteredResult);

        // Act
        var result = await _service.FilterTextAsync(textToFilter);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedFilteredResult);
        result.WasFiltered.Should().BeTrue();
        result.FilteredText.Should().Contain("[REDACTED]");

        _mockFilter.Verify(x => x.FilterTextAsync(textToFilter, It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyDebugLogging("Filtering text content");
        VerifyInformationLogging("Text content was filtered");
    }

    /// <summary>
    /// Tests that FilterTextAsync returns null when no filter is available.
    /// </summary>
    [Fact]
    public async Task FilterTextAsync_WithoutFilter_ReturnsNull()
    {
        // Arrange
        var serviceWithoutFilter = new SafetyEvaluationService(_mockEvaluator.Object, Microsoft.Extensions.Options.Options.Create(_testOptions), _mockLogger.Object, null);
        var textToFilter = "Some text to filter";

        // Act
        var result = await serviceWithoutFilter.FilterTextAsync(textToFilter);

        // Assert
        result.Should().BeNull();
        
        VerifyDebugLogging("No safety filter configured, skipping content filtering");
    }

    /// <summary>
    /// Tests that FilterTextAsync returns unfiltered result when safety is disabled.
    /// </summary>
    [Fact]
    public async Task FilterTextAsync_WhenDisabled_ReturnsUnfilteredResult()
    {
        // Arrange
        var disabledOptions = CreateTestOptions();
        disabledOptions.Enabled = false;
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(disabledOptions);
        var disabledService = new SafetyEvaluationService(_mockEvaluator.Object, optionsWrapper, _mockLogger.Object, _mockFilter.Object);

        var textToFilter = "Some text that would normally be filtered";

        // Act
        var result = await disabledService.FilterTextAsync(textToFilter);

        // Assert
        result.Should().NotBeNull();
        result.OriginalText.Should().Be(textToFilter);
        result.FilteredText.Should().Be(textToFilter);
        result.WasFiltered.Should().BeFalse();

        // Should not call the filter when disabled
        _mockFilter.Verify(x => x.FilterTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that GetSafetyStatus returns correct status information.
    /// </summary>
    [Fact]
    public void GetSafetyStatus_ReturnsCorrectStatus()
    {
        // Arrange
        var supportedCategories = new HashSet<HarmCategory> { HarmCategory.Hate, HarmCategory.Violence };
        _mockEvaluator.Setup(x => x.GetProviderName()).Returns("Test Provider");
        _mockEvaluator.Setup(x => x.GetSupportedCategories()).Returns(supportedCategories);
        _mockFilter.Setup(x => x.GetProviderName()).Returns("Test Filter");

        // Act
        var status = _service.GetSafetyStatus();

        // Assert
        status.Should().NotBeNull();
        status.IsEnabled.Should().BeTrue();
        status.Provider.Should().Be("Test Provider");
        status.SupportedCategories.Should().BeEquivalentTo(supportedCategories);
        status.HasFilter.Should().BeTrue();
        status.FilterProvider.Should().Be("Test Filter");
        status.FallbackBehavior.Should().Be(FallbackBehavior.FailOpen);
        status.InputPolicy.Should().NotBeNull();
        status.OutputPolicy.Should().NotBeNull();

        _mockEvaluator.Verify(x => x.GetProviderName(), Times.Once);
        _mockEvaluator.Verify(x => x.GetSupportedCategories(), Times.Once);
        _mockFilter.Verify(x => x.GetProviderName(), Times.Once);
    }

    /// <summary>
    /// Tests that fallback behavior works correctly for user input evaluation.
    /// </summary>
    [Fact]
    public async Task EvaluateUserInputAsync_WithEvaluatorError_ReturnsFallbackResult()
    {
        // Arrange
        var userInput = "Some input that causes error";
        var exception = new HttpRequestException("Service unavailable");

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(userInput, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _service.EvaluateUserInputAsync(userInput);

        // Assert
        result.Should().NotBeNull();
        
        // With FailOpen behavior, should return safe
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");
        result.Metadata.AdditionalData["Context"].Should().Be("UserInput");

        _mockEvaluator.Verify(x => x.EvaluateTextAsync(userInput, It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyErrorLogging("Error occurred during user input safety evaluation");
    }

    /// <summary>
    /// Tests that fallback behavior works correctly for AI output evaluation.
    /// </summary>
    [Fact]
    public async Task EvaluateOutputAsync_WithEvaluatorError_ReturnsFallbackResult()
    {
        // Arrange
        var aiOutput = "Some output that causes error";
        var exception = new TaskCanceledException("Request timed out");

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(aiOutput, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _service.EvaluateOutputAsync(aiOutput);

        // Assert
        result.Should().NotBeNull();
        
        // With FailOpen behavior, should return safe
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");
        result.Metadata.AdditionalData["Context"].Should().Be("AIOutput");

        _mockEvaluator.Verify(x => x.EvaluateTextAsync(aiOutput, It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyErrorLogging("Error occurred during AI output safety evaluation");
    }

    /// <summary>
    /// Tests that fallback behavior works correctly for batch evaluation.
    /// </summary>
    [Fact]
    public async Task EvaluateBatchAsync_WithEvaluatorError_ReturnsFallbackResults()
    {
        // Arrange
        var messages = new[] { "Message 1", "Message 2", "Message 3" };
        var exception = new InvalidOperationException("Service configuration error");

        _mockEvaluator
            .Setup(x => x.EvaluateBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var results = await _service.EvaluateBatchAsync(messages);

        // Assert
        results.Should().HaveCount(3);
        
        // All results should be fallback results (safe with FailOpen)
        foreach (var result in results)
        {
            result.IsSafe.Should().BeTrue();
            result.RiskScore.Should().Be(0);
            result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
            result.Metadata!.AdditionalData["Context"].Should().Be("Batch");
        }

        _mockEvaluator.Verify(x => x.EvaluateBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyErrorLogging("Error occurred during batch safety evaluation");
    }

    /// <summary>
    /// Tests that FailClosed fallback behavior returns unsafe results.
    /// </summary>
    [Fact]
    public async Task EvaluateUserInputAsync_WithFailClosedBehavior_ReturnsUnsafeFallbackResult()
    {
        // Arrange
        var failClosedOptions = CreateTestOptions();
        failClosedOptions.FallbackBehavior = FallbackBehavior.FailClosed;
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(failClosedOptions);
        var failClosedService = new SafetyEvaluationService(_mockEvaluator.Object, optionsWrapper, _mockLogger.Object, _mockFilter.Object);

        var userInput = "Some input that causes error";
        var exception = new HttpRequestException("Service unavailable");

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(userInput, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await failClosedService.EvaluateUserInputAsync(userInput);

        // Assert
        result.Should().NotBeNull();
        
        // With FailClosed behavior, should return unsafe
        result.IsSafe.Should().BeFalse();
        result.RiskScore.Should().Be(80);
        result.DetectedCategories.Should().HaveCount(1);
        result.DetectedCategories.First().Category.Should().Be(HarmCategory.Violence);
        result.Recommendations.Should().Contain(r => r.Contains("blocked due to safety service failure"));
        result.Metadata!.AdditionalData["FallbackBehavior"].Should().Be("FailClosed");
    }

    /// <summary>
    /// Tests that threshold application works correctly through the service.
    /// </summary>
    [Fact]
    public async Task EvaluateUserInputAsync_WithCustomThresholds_AppliesThresholdsCorrectly()
    {
        // Arrange
        var highThresholdOptions = CreateTestOptions();
        highThresholdOptions.InputPolicy.Thresholds[HarmCategory.Hate] = 7; // Very high threshold
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(highThresholdOptions);
        var highThresholdService = new SafetyEvaluationService(_mockEvaluator.Object, optionsWrapper, _mockLogger.Object, _mockFilter.Object);

        var userInput = "Content with low severity hate";
        var lowSeverityHateResult = SafetyEvaluationResult.Unsafe(HarmCategory.Hate, 2); // Below threshold

        _mockEvaluator
            .Setup(x => x.EvaluateTextAsync(userInput, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lowSeverityHateResult);

        // Act
        var result = await highThresholdService.EvaluateUserInputAsync(userInput);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue(); // Should be safe due to high threshold
        result.DetectedCategories.Should().BeEmpty();
    }

    private void VerifyDebugLogging(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyWarningLogging(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyErrorLogging(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private void VerifyInformationLogging(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
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
            OutputPolicy = new PolicySettings
            {
                Thresholds = new Dictionary<HarmCategory, int>
                {
                    [HarmCategory.Hate] = 3,
                    [HarmCategory.SelfHarm] = 2,
                    [HarmCategory.Sexual] = 3,
                    [HarmCategory.Violence] = 3
                }
            }
        };
    }

    public void Dispose()
    {
        // Clean up any resources if needed
    }
}