using AIChat.Safety.Contracts;
using AIChat.Safety.Options;
using AIChat.Safety.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AIChat.Safety.Tests.Providers;

/// <summary>
/// Unit tests for the OpenAIStreamingSafetyEvaluator class.
/// </summary>
public class OpenAIStreamingSafetyEvaluatorTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OpenAIStreamingSafetyEvaluator>> _mockLogger;
    private readonly SafetyOptions _testOptions;
    private readonly OpenAIStreamingSafetyEvaluator _evaluator;

    public OpenAIStreamingSafetyEvaluatorTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _mockLogger = new Mock<ILogger<OpenAIStreamingSafetyEvaluator>>();

        _testOptions = CreateTestOptions();
        _evaluator = new OpenAIStreamingSafetyEvaluator(_httpClient, _testOptions, _mockLogger.Object);
    }

    /// <summary>
    /// Tests that safe chunks are evaluated correctly and return safe results.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_WithSafeChunks_ReturnsSafeResults()
    {
        // Arrange
        var safeChunks = new[]
        {
            "Hello, how are you",
            "today? I hope you're",
            "having a wonderful day!"
        };

        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act & Assert
        foreach (var chunk in safeChunks)
        {
            var result = await _evaluator.EvaluateChunkAsync(chunk);
            
            result.Should().NotBeNull();
            result.IsSafe.Should().BeTrue();
            result.RiskScore.Should().Be(0);
            result.DetectedCategories.Should().BeEmpty();
        }

        _evaluator.GetProcessedChunkCount().Should().Be(safeChunks.Length);
        _evaluator.HasViolations().Should().BeFalse();
    }

    /// <summary>
    /// Tests that harmful content is detected during streaming evaluation.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_WithHarmfulContent_DetectsViolations()
    {
        // Arrange
        var safeChunk = "Hello, this is a normal message";
        var harmfulChunk = "but I hate everyone and want to cause violence";
        
        var safeResponse = CreateSafeModerationResponse();
        var harmfulResponse = CreateHateAndViolenceModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(harmfulResponse));

        // Act
        var safeResult = await _evaluator.EvaluateChunkAsync(safeChunk);
        var harmfulResult = await _evaluator.EvaluateChunkAsync(harmfulChunk);

        // Assert
        safeResult.IsSafe.Should().BeTrue();
        harmfulResult.IsSafe.Should().BeFalse();
        harmfulResult.DetectedCategories.Should().HaveCountGreaterThan(0);
        harmfulResult.DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Hate);
        harmfulResult.DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Violence);
        
        _evaluator.HasViolations().Should().BeTrue();
        _evaluator.GetProcessedChunkCount().Should().Be(2);
    }

    /// <summary>
    /// Tests that buffer management works correctly during streaming evaluation.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_ManagesBufferCorrectly()
    {
        // Arrange
        var chunks = new[]
        {
            "This is the first chunk",
            " that continues the sentence",
            " and completes it with more text.",
            " Now we start a new sentence that should trigger evaluation."
        };

        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var results = new List<SafetyEvaluationResult>();
        foreach (var chunk in chunks)
        {
            var result = await _evaluator.EvaluateChunkAsync(chunk);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(chunks.Length);
        _evaluator.GetProcessedChunkCount().Should().Be(chunks.Length);
        
        // The accumulated content should contain all chunks
        var accumulatedContent = _evaluator.GetAccumulatedContent();
        accumulatedContent.Should().Contain("first chunk");
        accumulatedContent.Should().Contain("continues the sentence");
        accumulatedContent.Should().Contain("completes it");
        accumulatedContent.Should().Contain("new sentence");
    }

    /// <summary>
    /// Tests that context accumulation works across multiple chunks.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_AccumulatesContextAcrossChunks()
    {
        // Arrange
        var chunks = new[]
        {
            "I really dislike",
            " when people are mean",
            " to each other. It makes me angry."
        };

        var expectedResponse = CreateHateModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var results = new List<SafetyEvaluationResult>();
        foreach (var chunk in chunks)
        {
            var result = await _evaluator.EvaluateChunkAsync(chunk);
            results.Add(result);
        }

        // Assert
        _evaluator.GetAccumulatedContent().Should().Be(string.Concat(chunks));
        _evaluator.GetProcessedChunkCount().Should().Be(chunks.Length);
        
        // The last chunk should trigger evaluation and detect hate content
        var lastResult = results.Last();
        lastResult.IsSafe.Should().BeFalse();
        lastResult.DetectedCategories.Should().Contain(c => c.Category == HarmCategory.Hate);
    }

    /// <summary>
    /// Tests that evaluation is triggered based on character count threshold.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_TriggersEvaluationOnCharacterCount()
    {
        // Arrange
        var longChunk = new string('a', 350); // Exceeds 300 character threshold
        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateChunkAsync(longChunk);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        
        // Should have made an HTTP request due to character count threshold
        _mockHttp.GetMatchCount(_mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)).Should().Be(1);
    }

    /// <summary>
    /// Tests that evaluation is triggered on sentence boundaries.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_TriggersEvaluationOnSentenceBoundary()
    {
        // Arrange
        var sentenceChunk = "This is a complete sentence. ";
        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateChunkAsync(sentenceChunk);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        
        // Should have made an HTTP request due to sentence boundary
        _mockHttp.GetMatchCount(_mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)).Should().Be(1);
    }

    /// <summary>
    /// Tests that evaluation is triggered on paragraph boundaries.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_TriggersEvaluationOnParagraphBoundary()
    {
        // Arrange
        var paragraphChunk = "This is a paragraph.\n\nThis is another paragraph.";
        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateChunkAsync(paragraphChunk);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        
        // Should have made an HTTP request due to paragraph boundary
        _mockHttp.GetMatchCount(_mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)).Should().Be(1);
    }

    /// <summary>
    /// Tests that evaluation is triggered periodically based on chunk count.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_TriggersEvaluationPeriodically()
    {
        // Arrange
        var smallChunks = Enumerable.Repeat("small chunk ", 10).ToList();
        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var results = new List<SafetyEvaluationResult>();
        foreach (var chunk in smallChunks)
        {
            var result = await _evaluator.EvaluateChunkAsync(chunk);
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(smallChunks.Count);
        
        // Should have made at least one HTTP request due to periodic evaluation (every 10 chunks)
        _mockHttp.GetMatchCount(_mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)).Should().BeGreaterOrEqualTo(1);
    }

    /// <summary>
    /// Tests that empty or whitespace chunks are handled gracefully.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    public async Task EvaluateChunkAsync_WithEmptyChunk_ReturnsSafeResult(string chunk)
    {
        // Act
        var result = await _evaluator.EvaluateChunkAsync(chunk);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        
        // Should not have made any HTTP requests for empty content
        var mockRequest = _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint);
        _mockHttp.GetMatchCount(mockRequest).Should().Be(0);
    }

    /// <summary>
    /// Tests that when safety is disabled, chunks are always considered safe.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_WhenDisabled_ReturnsSafeResult()
    {
        // Arrange
        var disabledOptions = CreateTestOptions();
        disabledOptions.Enabled = false;
        var disabledEvaluator = new OpenAIStreamingSafetyEvaluator(_httpClient, disabledOptions, _mockLogger.Object);

        var harmfulChunk = "This would normally be flagged as harmful content";

        // Act
        var result = await disabledEvaluator.EvaluateChunkAsync(harmfulChunk);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.DetectedCategories.Should().BeEmpty();
        
        // Should not have made any HTTP requests when disabled
        var mockRequest = _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint);
        _mockHttp.GetMatchCount(mockRequest).Should().Be(0);
    }

    /// <summary>
    /// Tests that HTTP errors are handled gracefully during streaming evaluation.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_WithHttpError_ReturnsFallbackResult()
    {
        // Arrange
        var errorChunk = "Text that will cause HTTP error";
        
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.InternalServerError);

        // Act
        var result = await _evaluator.EvaluateChunkAsync(errorChunk);

        // Assert
        result.Should().NotBeNull();
        
        // With FailOpen behavior, should return safe
        result.IsSafe.Should().BeTrue();
        result.RiskScore.Should().Be(0);
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");
        result.Metadata.AdditionalData["EvaluationType"].Should().Be("Streaming");
    }

    /// <summary>
    /// Tests that timeout is handled gracefully during streaming evaluation.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_WithTimeout_ReturnsFallbackResult()
    {
        // Arrange
        var timeoutChunk = "Text that will cause timeout";
        
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(req => throw new TaskCanceledException("Request timed out"));

        // Act
        var result = await _evaluator.EvaluateChunkAsync(timeoutChunk);

        // Assert
        result.Should().NotBeNull();
        result.IsSafe.Should().BeTrue(); // FailOpen behavior
        result.Recommendations.Should().Contain(r => r.Contains("fail-open"));
        result.Metadata!.AdditionalData.Should().ContainKey("FallbackReason");
    }

    /// <summary>
    /// Tests that Reset() clears all accumulated state.
    /// </summary>
    [Fact]
    public async Task Reset_ClearsAllState()
    {
        // Arrange
        var chunks = new[]
        {
            "First chunk",
            "Second chunk",
            "Third chunk"
        };

        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act - Process some chunks
        foreach (var chunk in chunks)
        {
            await _evaluator.EvaluateChunkAsync(chunk);
        }

        // Verify state before reset
        _evaluator.GetProcessedChunkCount().Should().Be(chunks.Length);
        _evaluator.GetAccumulatedContent().Should().NotBeEmpty();

        // Reset
        _evaluator.Reset();

        // Assert - Verify state after reset
        _evaluator.GetProcessedChunkCount().Should().Be(0);
        _evaluator.GetAccumulatedContent().Should().BeEmpty();
        _evaluator.HasViolations().Should().BeFalse();
    }

    /// <summary>
    /// Tests that GetAccumulatedContent returns the correct accumulated text.
    /// </summary>
    [Fact]
    public async Task GetAccumulatedContent_ReturnsCorrectAccumulatedText()
    {
        // Arrange
        var chunks = new[]
        {
            "Hello",
            " world",
            "!",
            " How are you?"
        };

        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        foreach (var chunk in chunks)
        {
            await _evaluator.EvaluateChunkAsync(chunk);
        }

        // Assert
        var accumulatedContent = _evaluator.GetAccumulatedContent();
        accumulatedContent.Should().Be("Hello world! How are you?");
    }

    /// <summary>
    /// Tests that GetProcessedChunkCount returns the correct count.
    /// </summary>
    [Fact]
    public async Task GetProcessedChunkCount_ReturnsCorrectCount()
    {
        // Arrange
        var chunkCount = 5;
        var chunks = Enumerable.Repeat("test chunk ", chunkCount);

        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        foreach (var chunk in chunks)
        {
            await _evaluator.EvaluateChunkAsync(chunk);
        }

        // Assert
        _evaluator.GetProcessedChunkCount().Should().Be(chunkCount);
    }

    /// <summary>
    /// Tests that HasViolations returns the correct state.
    /// </summary>
    [Fact]
    public async Task HasViolations_ReturnsCorrectState()
    {
        // Arrange
        var safeChunk = "This is safe content";
        var harmfulChunk = "I hate everyone and want to cause violence";
        
        var safeResponse = CreateSafeModerationResponse();
        var harmfulResponse = CreateHateAndViolenceModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(safeResponse));
        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(harmfulResponse));

        // Act & Assert - Initially no violations
        await _evaluator.EvaluateChunkAsync(safeChunk);
        _evaluator.HasViolations().Should().BeFalse();

        // After harmful content, should have violations
        await _evaluator.EvaluateChunkAsync(harmfulChunk);
        _evaluator.HasViolations().Should().BeTrue();

        // After reset, should have no violations
        _evaluator.Reset();
        _evaluator.HasViolations().Should().BeFalse();
    }

    /// <summary>
    /// Tests that disposed evaluator throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _evaluator.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _evaluator.EvaluateChunkAsync("test chunk"));
    }

    /// <summary>
    /// Tests that streaming-specific recommendations are included for violations.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_WithViolations_IncludesStreamingRecommendations()
    {
        // Arrange
        var harmfulChunk = "I hate everyone and want to cause violence";
        var harmfulResponse = CreateHateAndViolenceModerationResponse();

        _mockHttp.When(HttpMethod.Post, _testOptions.Endpoint)
            .Respond(HttpStatusCode.OK, JsonContent.Create(harmfulResponse));

        // Act
        var result = await _evaluator.EvaluateChunkAsync(harmfulChunk);

        // Assert
        result.IsSafe.Should().BeFalse();
        result.Recommendations.Should().Contain(r => r.Contains("Streaming content violation detected"));
        result.Recommendations.Should().Contain(r => r.Contains("terminating the stream"));
    }

    /// <summary>
    /// Tests that metadata includes streaming-specific information.
    /// </summary>
    [Fact]
    public async Task EvaluateChunkAsync_IncludesStreamingMetadata()
    {
        // Arrange
        var chunk = "Test chunk for metadata";
        var expectedResponse = CreateSafeModerationResponse();

        _mockHttp.When(HttpMethod.Post, "")
            .Respond(HttpStatusCode.OK, JsonContent.Create(expectedResponse));

        // Act
        var result = await _evaluator.EvaluateChunkAsync(chunk);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Provider.Should().Be("OpenAI Moderation Streaming");
        result.Metadata.AdditionalData.Should().ContainKey("ChunkNumber");
        result.Metadata.AdditionalData.Should().ContainKey("EvaluationType");
        result.Metadata.AdditionalData.Should().ContainKey("BufferLength");
        result.Metadata.AdditionalData["EvaluationType"].Should().Be("Streaming");
        result.Metadata.AdditionalData["ChunkNumber"].Should().Be(1);
    }

    private static SafetyOptions CreateTestOptions()
    {
        return new SafetyOptions
        {
            Enabled = true,
            Endpoint = "https://api.openai.com/v1/moderations",
#pragma warning disable CS0618 // Type or member is obsolete
            ApiKey = "test-api-key",
#pragma warning restore CS0618
            Model = "omni-moderation-latest",
            FallbackBehavior = FallbackBehavior.FailOpen,
            OutputPolicy = new PolicySettings
            {
                Thresholds = new Dictionary<HarmCategory, int>
                {
                    [HarmCategory.Hate] = 2,
                    [HarmCategory.Harassment] = 2,
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
            id = "modr-streaming-safe-123",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool>
                    {
                        ["harassment"] = false,
                        ["harassment_threatening"] = false,
                        ["hate"] = false,
                        ["hate_threatening"] = false,
                        ["self_harm"] = false,
                        ["self_harm_instructions"] = false,
                        ["self_harm_intent"] = false,
                        ["sexual"] = false,
                        ["sexual_minors"] = false,
                        ["violence"] = false,
                        ["violence_graphic"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["harassment"] = 0.01,
                        ["harassment_threatening"] = 0.01,
                        ["hate"] = 0.01,
                        ["hate_threatening"] = 0.01,
                        ["self_harm"] = 0.02,
                        ["self_harm_instructions"] = 0.01,
                        ["self_harm_intent"] = 0.01,
                        ["sexual"] = 0.01,
                        ["sexual_minors"] = 0.01,
                        ["violence"] = 0.01,
                        ["violence_graphic"] = 0.01
                    }
                }
            }
        };
    }

    private static object CreateHateModerationResponse()
    {
        return new
        {
            id = "modr-streaming-hate-123",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new Dictionary<string, bool>
                    {
                        ["harassment"] = false,
                        ["harassment_threatening"] = false,
                        ["hate"] = true,
                        ["hate_threatening"] = false,
                        ["self_harm"] = false,
                        ["self_harm_instructions"] = false,
                        ["self_harm_intent"] = false,
                        ["sexual"] = false,
                        ["sexual_minors"] = false,
                        ["violence"] = false,
                        ["violence_graphic"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["harassment"] = 0.1,
                        ["harassment_threatening"] = 0.05,
                        ["hate"] = 0.8,
                        ["hate_threatening"] = 0.1,
                        ["self_harm"] = 0.1,
                        ["self_harm_instructions"] = 0.02,
                        ["self_harm_intent"] = 0.05,
                        ["sexual"] = 0.05,
                        ["sexual_minors"] = 0.01,
                        ["violence"] = 0.2,
                        ["violence_graphic"] = 0.1
                    }
                }
            }
        };
    }

    private static object CreateHateAndViolenceModerationResponse()
    {
        return new
        {
            id = "modr-streaming-multiple-123",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new Dictionary<string, bool>
                    {
                        ["harassment"] = false,
                        ["harassment_threatening"] = false,
                        ["hate"] = true,
                        ["hate_threatening"] = false,
                        ["self_harm"] = false,
                        ["self_harm_instructions"] = false,
                        ["self_harm_intent"] = false,
                        ["sexual"] = false,
                        ["sexual_minors"] = false,
                        ["violence"] = true,
                        ["violence_graphic"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["harassment"] = 0.2,
                        ["harassment_threatening"] = 0.1,
                        ["hate"] = 0.7,
                        ["hate_threatening"] = 0.3,
                        ["self_harm"] = 0.1,
                        ["self_harm_instructions"] = 0.02,
                        ["self_harm_intent"] = 0.05,
                        ["sexual"] = 0.05,
                        ["sexual_minors"] = 0.01,
                        ["violence"] = 0.8,
                        ["violence_graphic"] = 0.4
                    }
                }
            }
        };
    }

    public void Dispose()
    {
        _evaluator?.Dispose();
        _httpClient?.Dispose();
        _mockHttp?.Dispose();
    }
}