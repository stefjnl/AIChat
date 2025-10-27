# Responsible AI (RAI) Architecture for AIChat (v3)

This document outlines the production-ready architecture for integrating Responsible AI safety evaluators into the AIChat .NET Aspire project, incorporating detailed feedback for a robust, resilient, and observable system.

**Status**: ✅ Implemented with OpenAI Moderation API integration
**Version**: 3.0 - Production Ready
**Last Updated**: 2024-01-15

## 1. Core Concepts

The RAI system is a streaming-first, modular component designed to prevent harmful content from being processed by or displayed to users. It is built on the principles of resilience, configurability, and detailed observability.

## 2. Project Structure (Implemented)

The `AIChat.Safety` project has been created with OpenAI Moderation API integration:

```
AIChat.Safety/
├── AIChat.Safety.csproj
├── Contracts/
│   ├── ISafetyEvaluator.cs                 # Main evaluation interface
│   ├── IStreamingSafetyEvaluator.cs        # Real-time streaming interface
│   ├── IStreamingSafetyEvaluator.cs        # Streaming interface
│   ├── ISafetyFilter.cs                    # Content filtering interface
│   ├── SafetyEvaluationResult.cs           # Evaluation result model
│   └── HarmCategory.cs                     # Harm category enumeration
├── Providers/
│   ├── OpenAIModerationEvaluator.cs        # OpenAI Moderation API implementation
│   └── OpenAIStreamingSafetyEvaluator.cs   # Streaming evaluation implementation
├── Options/
│   ├── SafetyOptions.cs                    # Comprehensive configuration
│   ├── FilteringSettings.cs                # Content filtering options
│   ├── AuditSettings.cs                    # Audit logging options
│   └── RateLimitSettings.cs                # Rate limiting options
├── Services/
│   ├── SafetyEvaluationService.cs          # Main safety service
│   └── SafetyHealthCheck.cs                # Health monitoring
├── DependencyInjection/
│   └── SafetyServiceCollectionExtensions.cs # Service registration
└── Tests/
    ├── Integration/
    ├── Providers/
    └── Services/
```

## 3. Core Contracts

### 3.1. `ISafetyEvaluator` and `IStreamingSafetyEvaluator`

These interfaces define the contract for safety evaluation, supporting both full-text and incremental (streaming) analysis.

```csharp
// AIChat.Safety/Contracts/ISafetyEvaluator.cs
public interface ISafetyEvaluator
{
    Task<SafetyEvaluationResult> EvaluateTextAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SafetyEvaluationResult>> EvaluateBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
    IStreamingSafetyEvaluator CreateStreamingEvaluator();
    ISet<HarmCategory> GetSupportedCategories();
    string GetProviderName();
}

// AIChat.Safety/Contracts/IStreamingSafetyEvaluator.cs
public interface IStreamingSafetyEvaluator : IDisposable
{
    Task<SafetyEvaluationResult> EvaluateChunkAsync(string chunk, CancellationToken cancellationToken = default);
    string GetAccumulatedContent();
    void Reset();
    int GetProcessedChunkCount();
    bool HasViolations();
}
```

### 3.2. `SafetyEvaluationResult`

This class encapsulates the outcome of an evaluation with comprehensive metadata.

```csharp
// AIChat.Safety/Contracts/SafetyEvaluationResult.cs
public class SafetyEvaluationResult
{
    public bool IsSafe { get; set; }
    public List<DetectedHarmCategory> DetectedCategories { get; set; } = new();
    public int RiskScore { get; set; }
    public EvaluationMetadata? Metadata { get; set; }
    public List<string> Recommendations { get; set; } = new();
    
    public static SafetyEvaluationResult Safe => new()
    {
        IsSafe = true,
        RiskScore = 0,
        Recommendations = { "Content is safe to proceed." }
    };
}

public class DetectedHarmCategory
{
    public HarmCategory Category { get; set; }
    public int Severity { get; set; }
    public string? Description { get; set; }
    public int Confidence { get; set; }
    public List<string> TriggeringSegments { get; set; } = new();
}

public class EvaluationMetadata
{
    public string Provider { get; set; } = string.Empty;
    public string? ProviderVersion { get; set; }
    public long ProcessingTimeMs { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RequestId { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
```

## 4. Evaluation Pipeline and Streaming Strategy

### 4.1. High-Level Flow

1.  **User Input**: The user's complete message is evaluated via `EvaluateTextAsync`. If unsafe, the process is terminated, audited, and an error is returned.
2.  **AI Response**: An `IStreamingSafetyEvaluator` instance is created for the response stream.
3.  **Streaming Evaluation**: As the AI streams back chunks of text, they are passed to `EvaluateChunkAsync`. The evaluator buffers content to form meaningful segments (e.g., sentences) before calling the backing safety API to reduce latency and cost.
4.  **Mid-stream Termination**: If any segment is found to be unsafe, the stream to the user is immediately terminated, the event is audited, and a final error chunk is sent.

### 4.2. `ChatHub` Integration

```csharp
// AIChat.WebApi/Hubs/ChatHub.cs (Implemented)
public async IAsyncEnumerable<ChatChunk> StreamChat(
    string message,
    string provider,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // 1. Evaluate user's message
    var userMessageEvaluation = await _safetyService.EvaluateUserInputAsync(message, cancellationToken);
    if (!userMessageEvaluation.IsSafe)
    {
        _logger.LogWarning("User message blocked due to safety violations: {Categories}",
            userMessageEvaluation.DetectedCategories.Select(c => c.Category));
        yield break;
    }

    // 2. Create streaming evaluator for AI response
    var streamingEvaluator = _safetyService.CreateStreamingEvaluator();
    
    try
    {
        // 3. Process AI response with safety checks
        await foreach (var update in agent.RunStreamingAsync(...))
        {
            var chunkEvaluation = await streamingEvaluator.EvaluateChunkAsync(update.Text, cancellationToken);
            if (!chunkEvaluation.IsSafe)
            {
                _logger.LogWarning("AI response stream terminated due to safety violations: {Categories}",
                    chunkEvaluation.DetectedCategories.Select(c => c.Category));
                yield break;
            }
            yield return new ChatChunk { Text = update.Text };
        }
    }
    finally
    {
        streamingEvaluator?.Dispose();
    }
}
```

## 5. Concrete Provider: OpenAI Moderation API

### 5.1. Implementation Details

The `OpenAIModerationEvaluator` uses OpenAI's Moderation API with comprehensive error handling and resilience patterns:

- **HTTP Client Configuration**: Configured with timeouts, retries, and circuit breakers
- **Response Mapping**: Maps OpenAI's 0-1 confidence scores to 0-7 severity levels
- **Batch Processing**: Supports efficient batch evaluation of multiple texts
- **Streaming Support**: Real-time evaluation during AI response generation

### 5.2. Category and Severity Mapping

OpenAI provides four main categories with confidence scores (0-1):

```csharp
// Score to Severity Mapping
private static int CalculateSeverity(double score) => score switch
{
    <= 0.1 => 0,  // Very low confidence
    <= 0.2 => 1,  // Low confidence
    <= 0.3 => 2,  // Low-medium confidence
    <= 0.4 => 3,  // Medium confidence
    <= 0.5 => 4,  // Medium-high confidence
    <= 0.6 => 5,  // High confidence
    <= 0.8 => 6,  // Very high confidence
    <= 1.0 => 7,  // Maximum confidence
    _ => 0
};
```

### 5.3. Streaming Evaluation Strategy

The streaming evaluator implements intelligent buffering:

```csharp
private bool ShouldEvaluateBuffer()
{
    var text = GetAccumulatedContent();
    
    // Multiple triggers for evaluation:
    // 1. Character count threshold (> 300 chars)
    // 2. Sentence boundaries (regex detection)
    // 3. Paragraph boundaries (double newlines)
    // 4. Time-based evaluation (every 10 chunks)
    
    return text.Length > 300 ||
           Regex.IsMatch(text, @"[.!?]+\s*$") ||
           text.Contains("\n\n") ||
           _chunkCount % 10 == 0;
}
```

## 6. Configuration

The configuration is centralized under a single `Safety` section in `appsettings.json` with comprehensive options.

### 6.1. `SafetyOptions` Model

```csharp
// AIChat.Safety/Options/SafetyOptions.cs
public class SafetyOptions
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "https://api.openai.com/v1/moderations";
    public string? ApiKey { get; set; }
    public string? OrganizationId { get; set; }
    public string Model { get; set; } = "text-moderation-latest";
    public FallbackBehavior FallbackBehavior { get; set; } = FallbackBehavior.FailOpen;
    public PolicySettings InputPolicy { get; set; } = new();
    public PolicySettings OutputPolicy { get; set; } = new();
    public ResilienceSettings Resilience { get; set; } = new();
    public FilteringSettings Filtering { get; set; } = new();
    public AuditSettings Audit { get; set; } = new();
    public RateLimitSettings RateLimit { get; set; } = new();
}
```

### 6.2. `appsettings.json` Example

```json
{
  "Safety": {
    "Enabled": true,
    "OpenAI": {
      "ApiKey": "",
      "OrganizationId": "",
      "Model": "text-moderation-latest",
      "Endpoint": "https://api.openai.com/v1/moderations"
    },
    "FallbackBehavior": "FailOpen",
    "InputPolicy": {
      "Thresholds": {
        "Hate": 4,
        "SelfHarm": 6,
        "Sexual": 4,
        "Violence": 2
      },
      "BlockOnViolation": true,
      "MaxRiskScore": 70
    },
    "OutputPolicy": {
      "Thresholds": {
        "Hate": 2,
        "SelfHarm": 4,
        "Sexual": 2,
        "Violence": 2
      },
      "BlockOnViolation": true,
      "MaxRiskScore": 50
    },
    "Resilience": {
      "TimeoutInMilliseconds": 5000,
      "MaxRetries": 2,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationInSeconds": 30,
      "UseExponentialBackoff": true
    },
    "Audit": {
      "Enabled": true,
      "LogFullContent": false,
      "LogContentHashes": true,
      "LogMetadata": true,
      "RetentionPeriodInDays": 90
    }
  }
}
```

## 7. Dependency Injection and Service Lifetimes

Services are registered in [`Program.cs`](AIChat.WebApi/Program.cs) using the extension methods in [`SafetyServiceCollectionExtensions.cs`](AIChat.Safety/DependencyInjection/SafetyServiceCollectionExtensions.cs):

```csharp
// Core registration
builder.Services.AddAISafetyServices(builder.Configuration);

// Environment-specific configuration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevelopmentSafety();
}
else
{
    builder.Services.AddProductionSafety();
}
```

### Service Lifetimes

- **`HttpClient`**: Registered for OpenAIModerationEvaluator with proper configuration
- **`SafetyOptions`**: Singleton `IOptions<SafetyOptions>` loaded from configuration
- **`ISafetyEvaluator` (`OpenAIModerationEvaluator`)**: Singleton for efficiency
- **`SafetyEvaluationService`**: Singleton with thread-safe operations
- **`SafetyHealthCheck`**: Registered with health check system

### Resilience Policies

Built-in Polly resilience policies:

```csharp
private static IAsyncPolicy<HttpResponseMessage> GetResiliencePolicy()
{
    var delay = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(1000), retryCount: 3);
    
    var retryPolicy = Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(response => response.StatusCode >= HttpStatusCode.InternalServerError)
        .WaitAndRetryAsync(delay);
    
    var circuitBreakerPolicy = Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    
    return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
}
```

## 8. Error Handling and Resilience

### Fallback Behaviors

```csharp
private SafetyEvaluationResult GetFallbackResult(string context, Exception? exception = null)
{
    if (_options.Value.FallbackBehavior == FallbackBehavior.FailClosed)
    {
        return new SafetyEvaluationResult
        {
            IsSafe = false,
            RiskScore = 80,
            DetectedCategories = { new DetectedHarmCategory { Category = HarmCategory.Violence, Severity = 6 } },
            Recommendations = { $"Content blocked due to safety service failure in {context}." }
        };
    }
    
    // FailOpen - allow content but log the issue
    return new SafetyEvaluationResult
    {
        IsSafe = true,
        RiskScore = 0,
        Recommendations = { $"Content allowed due to safety service failure in {context} (fail-open policy)." }
    };
}
```

### Comprehensive Error Handling

- **HTTP Errors**: Automatic retries with exponential backoff
- **Timeout Protection**: Configurable timeouts for all operations
- **Circuit Breaker**: Prevents cascading failures
- **Graceful Degradation**: Fallback behaviors when services are unavailable
- **Detailed Logging**: All errors logged with context and metadata

## 9. Audit Logging

### Audit Implementation

```csharp
private Task LogSafetyViolationAsync(string content, SafetyEvaluationResult result, string contentType)
{
    var auditEntry = new
    {
        Timestamp = DateTimeOffset.UtcNow,
        ContentType = contentType,
        ContentLength = content.Length,
        ContentHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)),
        IsSafe = result.IsSafe,
        RiskScore = result.RiskScore,
        DetectedCategories = result.DetectedCategories.Select(c => new
        {
            Category = c.Category.ToString(),
            Severity = c.Severity,
            Confidence = c.Confidence,
            Description = c.Description
        }),
        Recommendations = result.Recommendations,
        Provider = result.Metadata?.Provider,
        ProcessingTimeMs = result.Metadata?.ProcessingTimeMs,
        RequestId = result.Metadata?.RequestId
    };

    _logger.LogInformation("Safety violation detected: {@AuditEntry}", auditEntry);
    return Task.CompletedTask;
}
```

### Audit Features

- **Privacy-First**: Content hashes instead of full text by default
- **Structured Logging**: JSON format for easy analysis
- **Configurable Detail Level**: Control what gets logged
- **Retention Policies**: Automatic cleanup of old audit logs
- **Alerting Integration**: Webhook support for critical violations

## 10. Testing Strategy

### Implemented Test Coverage

#### Unit Tests (`AIChat.Safety.Tests/`)
- **Provider Tests**: Mock OpenAI API responses for consistent testing
- **Service Tests**: Business logic and configuration validation
- **Configuration Tests**: Settings binding and validation
- **Utility Tests**: Helper functions and mapping logic

#### Integration Tests
- **End-to-End Pipeline**: Complete safety evaluation flow
- **Real API Testing**: Against OpenAI Moderation API with test data
- **Performance Tests**: Latency and throughput validation
- **Resilience Tests**: Circuit breaker and retry behavior

#### Test Data Management

```json
{
  "testCases": [
    {
      "category": "Hate",
      "content": "I hate [protected group] people",
      "expectedSafe": false,
      "expectedCategories": ["Hate"],
      "minSeverity": 4
    },
    {
      "category": "Safe",
      "content": "Hello, how are you today?",
      "expectedSafe": true,
      "expectedCategories": []
    }
  ]
}
```

### Continuous Integration

- **Automated Testing**: All tests run on every PR
- **Code Coverage**: Minimum 80% coverage requirement
- **Performance Benchmarks**: Regression detection for latency
- **Security Scanning**: Dependency and code vulnerability scanning

### Canary Deployments

The safety system supports gradual rollout:

```csharp
public class SafetyFeatureFlags
{
    public bool EnableSafetyEvaluation { get; set; } = true;
    public double SafetyEvaluationRolloutPercentage { get; set; } = 0.1; // 10% initially
    public bool EnableStrictMode { get; set; } = false;
}
```

## 11. Performance Monitoring

### Key Metrics

- **Latency**: P50, P95, P99 response times
- **Throughput**: Requests per second capacity
- **Error Rates**: Failure percentages by category
- **API Costs**: Token usage and cost tracking
- **Cache Hit Rates**: Effectiveness of caching strategies

### OpenTelemetry Integration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("AIChat.Safety"))
    .WithMetrics(metrics => metrics.AddMeter("AIChat.Safety"));
```

### Dashboard Examples

- **Real-time Safety Metrics**: Current system status
- **Historical Trends**: Violation patterns over time
- **Performance Analytics**: Latency and throughput trends
- **Cost Monitoring**: API usage and cost optimization

## 12. Security Considerations

### API Key Management

- **Secure Storage**: Azure Key Vault integration
- **Rotation Support**: Automated key rotation capabilities
- **Access Control**: Principle of least privilege
- **Audit Trail**: Key access logging

### Data Privacy

- **GDPR Compliance**: Right to be forgotten implementations
- **Data Minimization**: Only collect necessary data
- **Encryption**: All data encrypted at rest and in transit
- **Retention Policies**: Automatic data cleanup

### Threat Protection

- **Rate Limiting**: Prevent abuse and DoS attacks
- **Input Validation**: Comprehensive input sanitization
- **Output Encoding**: Prevent injection attacks
- **CORS Configuration**: Proper cross-origin resource sharing