using AIChat.Safety.Middleware;
using AIChat.Safety.Services;
using AIChat.Safety.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIChat.Safety.Tests.Middleware;

/// <summary>
/// Unit tests for SafetyChatClientMiddleware.
/// </summary>
public class SafetyChatClientMiddlewareTests
{
    private readonly Mock<IChatClient> _mockInnerClient;
    private readonly Mock<ISafetyEvaluationService> _mockSafetyService;
    private readonly Mock<ILogger<SafetyChatClientMiddleware>> _mockLogger;
    private readonly SafetyChatClientMiddleware _middleware;

    public SafetyChatClientMiddlewareTests()
    {
        _mockInnerClient = new Mock<IChatClient>();
        _mockSafetyService = new Mock<ISafetyEvaluationService>();
        _mockLogger = new Mock<ILogger<SafetyChatClientMiddleware>>();

        _middleware = new SafetyChatClientMiddleware(
   _mockInnerClient.Object,
            _mockSafetyService.Object,
     _mockLogger.Object);
    }

    [Fact]
    public async Task CompleteAsync_WithSafeUserInput_CallsInnerClient()
    {
  // Arrange
       var messages = new List<ChatMessage>
   {
      new ChatMessage(ChatRole.User, "Hello, how are you?")
        };

        var expectedResponse = new ChatCompletion(
    new ChatMessage(ChatRole.Assistant, "I'm doing well, thank you!"));

        _mockSafetyService
            .Setup(s => s.EvaluateUserInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
 .ReturnsAsync(SafetyEvaluationResult.Safe);

        _mockSafetyService
            .Setup(s => s.EvaluateOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SafetyEvaluationResult.Safe);

        _mockInnerClient
  .Setup(c => c.CompleteAsync(It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
     .ReturnsAsync(expectedResponse);

        // Act
  var result = await _middleware.CompleteAsync(messages);

        // Assert
      Assert.NotNull(result);
        Assert.Equal("I'm doing well, thank you!", result.Message.Text);
        _mockInnerClient.Verify(c => c.CompleteAsync(
            It.IsAny<IList<ChatMessage>>(),
        It.IsAny<ChatOptions>(),
      It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_WithUnsafeUserInput_ThrowsSafetyViolationException()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
    new ChatMessage(ChatRole.User, "Some unsafe content")
    };

        var unsafeResult = new SafetyEvaluationResult
        {
   IsSafe = false,
 RiskScore = 80,
            DetectedCategories =
            {
      new DetectedHarmCategory
{
        Category = HarmCategory.Hate,
         Severity = 6
     }
  }
   };

        _mockSafetyService
            .Setup(s => s.EvaluateUserInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unsafeResult);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SafetyViolationException>(
            () => _middleware.CompleteAsync(messages));

 Assert.Equal(SafetyViolationType.UserInput, exception.ViolationType);
        Assert.False(exception.EvaluationResult.IsSafe);
        Assert.Equal(80, exception.EvaluationResult.RiskScore);
     
        // Inner client should not be called
        _mockInnerClient.Verify(c => c.CompleteAsync(
      It.IsAny<IList<ChatMessage>>(),
   It.IsAny<ChatOptions>(),
          It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_WithUnsafeAIOutput_ThrowsSafetyViolationException()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Tell me something")
        };

        var aiResponse = new ChatCompletion(
            new ChatMessage(ChatRole.Assistant, "Some unsafe AI response"));

        var unsafeResult = new SafetyEvaluationResult
        {
          IsSafe = false,
            RiskScore = 70,
            DetectedCategories =
            {
              new DetectedHarmCategory
             {
        Category = HarmCategory.Violence,
           Severity = 5
         }
   }
    };

  _mockSafetyService
         .Setup(s => s.EvaluateUserInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(SafetyEvaluationResult.Safe);

 _mockSafetyService
            .Setup(s => s.EvaluateOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unsafeResult);

        _mockInnerClient
     .Setup(c => c.CompleteAsync(It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(aiResponse);

     // Act & Assert
        var exception = await Assert.ThrowsAsync<SafetyViolationException>(
            () => _middleware.CompleteAsync(messages));

     Assert.Equal(SafetyViolationType.AIOutput, exception.ViolationType);
        Assert.False(exception.EvaluationResult.IsSafe);
      Assert.Equal(70, exception.EvaluationResult.RiskScore);
    }

    [Fact]
    public async Task CompleteAsync_WithEmptyUserMessage_SkipsUserInputEvaluation()
    {
    // Arrange
  var messages = new List<ChatMessage>
        {
   new ChatMessage(ChatRole.System, "You are a helpful assistant")
        };

        var expectedResponse = new ChatCompletion(
       new ChatMessage(ChatRole.Assistant, "Hello!"));

  _mockSafetyService
            .Setup(s => s.EvaluateOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SafetyEvaluationResult.Safe);

        _mockInnerClient
            .Setup(c => c.CompleteAsync(It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

  // Act
 var result = await _middleware.CompleteAsync(messages);

        // Assert
        Assert.NotNull(result);
_mockSafetyService.Verify(s => s.EvaluateUserInputAsync(
 It.IsAny<string>(),
      It.IsAny<CancellationToken>()), Times.Never);
}

    [Fact]
    public async Task CompleteAsync_WithMultipleMessages_EvaluatesOnlyLastUserMessage()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
       new ChatMessage(ChatRole.User, "First message"),
            new ChatMessage(ChatRole.Assistant, "Response"),
            new ChatMessage(ChatRole.User, "Second message")
  };

        var expectedResponse = new ChatCompletion(
            new ChatMessage(ChatRole.Assistant, "Final response"));

        _mockSafetyService
.Setup(s => s.EvaluateUserInputAsync("Second message", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SafetyEvaluationResult.Safe);

  _mockSafetyService
  .Setup(s => s.EvaluateOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(SafetyEvaluationResult.Safe);

_mockInnerClient
       .Setup(c => c.CompleteAsync(It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
 .ReturnsAsync(expectedResponse);

     // Act
        var result = await _middleware.CompleteAsync(messages);

        // Assert
        Assert.NotNull(result);
        _mockSafetyService.Verify(s => s.EvaluateUserInputAsync(
            "Second message",
   It.IsAny<CancellationToken>()), Times.Once);
 _mockSafetyService.Verify(s => s.EvaluateUserInputAsync(
            "First message",
  It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullInnerClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        new SafetyChatClientMiddleware(null!, _mockSafetyService.Object, _mockLogger.Object));
    }

    [Fact]
public void Constructor_WithNullSafetyService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SafetyChatClientMiddleware(_mockInnerClient.Object, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
       new SafetyChatClientMiddleware(_mockInnerClient.Object, _mockSafetyService.Object, null!));
    }

    [Fact]
  public void GetService_RequestingSafetyService_ReturnsSafetyService()
    {
        // Act
      var service = _middleware.GetService(typeof(ISafetyEvaluationService));

        // Assert
        Assert.NotNull(service);
        Assert.Same(_mockSafetyService.Object, service);
    }

    [Fact]
    public async Task CompleteAsync_SafetyServiceThrowsException_PropagatesException()
    {
      // Arrange
        var messages = new List<ChatMessage>
        {
    new ChatMessage(ChatRole.User, "Test message")
  };

   _mockSafetyService
            .Setup(s => s.EvaluateUserInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("Safety service error"));

  // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
 () => _middleware.CompleteAsync(messages));
  }
}
