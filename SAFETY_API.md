# Safety Services API Documentation

This document provides comprehensive API documentation for the Responsible AI safety services implemented in the AIChat project.

## Table of Contents

1. [Core Interfaces](#core-interfaces)
2. [Safety Evaluation Service](#safety-evaluation-service)
3. [Configuration API](#configuration-api)
4. [Health Check API](#health-check-api)
5. [Models and Data Structures](#models-and-data-structures)
6. [Error Handling](#error-handling)
7. [Usage Examples](#usage-examples)
8. [Performance Considerations](#performance-considerations)

## Core Interfaces

### ISafetyEvaluator

The main interface for safety evaluation operations.

```csharp
public interface ISafetyEvaluator
{
    /// <summary>
    /// Evaluates a single text block for safety violations.
    /// </summary>
    /// <param name="text">The text to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Safety evaluation result.</returns>
    Task<SafetyEvaluationResult> EvaluateTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates multiple texts in batch for improved performance.
    /// </summary>
    /// <param name="texts">Collection of texts to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of safety evaluation results.</returns>
    Task<IReadOnlyList<SafetyEvaluationResult>> EvaluateBatchAsync(
        IEnumerable<string> texts, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming evaluator for real-time analysis.
    /// </summary>
    /// <returns>Streaming safety evaluator instance.</returns>
    IStreamingSafetyEvaluator CreateStreamingEvaluator();

    /// <summary>
    /// Gets supported harm categories.
    /// </summary>
    /// <returns>Set of supported harm categories.</returns>
    ISet<HarmCategory> GetSupportedCategories();

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    /// <returns>Provider identifier.</returns>
    string GetProviderName();
}
```

### IStreamingSafetyEvaluator

Interface for real-time streaming safety evaluation.

```csharp
public interface IStreamingSafetyEvaluator : IDisposable
{
    /// <summary>
    /// Evaluates the next chunk of text in the stream.
    /// </summary>
    /// <param name="chunk">Text chunk to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Safety evaluation result for current content.</returns>
    Task<SafetyEvaluationResult> EvaluateChunkAsync(string chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the accumulated text content.
    /// </summary>
    /// <returns>Complete accumulated text.</returns>
    string GetAccumulatedContent();

    /// <summary>
    /// Resets the evaluator state.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the number of processed chunks.
    /// </summary>
    /// <returns>Chunk count.</returns>
    int GetProcessedChunkCount();

    /// <summary>
    /// Checks if violations have been detected.
    /// </summary>
    /// <returns>True if violations detected.</returns>
    bool HasViolations();
}
```

### ISafetyEvaluationService

High-level service interface for safety operations.

```csharp
public interface ISafetyEvaluationService
{
    /// <summary>
    /// Evaluates user input for safety violations.
    /// </summary>
    /// <param name="message">User message to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Safety evaluation result.</returns>
    Task<SafetyEvaluationResult> EvaluateUserInputAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates AI-generated output for safety violations.
    /// </summary>
    /// <param name="output">AI output to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Safety evaluation result.</returns>
    Task<SafetyEvaluationResult> EvaluateOutputAsync(string output, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates multiple messages in batch.
    /// </summary>
    /// <param name="messages">Messages to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of evaluation results.</returns>
    Task<IReadOnlyList<SafetyEvaluationResult>> EvaluateBatchAsync(
        IEnumerable<string> messages, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming evaluator.
    /// </summary>
    /// <returns>Streaming evaluator instance.</returns>
    IStreamingSafetyEvaluator CreateStreamingEvaluator();

    /// <summary>
    /// Filters and sanitizes text content.
    /// </summary>
    /// <param name="text">Text to filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered text result.</returns>
    Task<FilteredTextResult?> FilterTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets safety status information.
    /// </summary>
    /// <returns>Safety status.</returns>
    SafetyStatus GetSafetyStatus();
}
```

## Safety Evaluation Service

### Main Service Methods

#### EvaluateUserInputAsync

Evaluates user input content using input policy thresholds.

```csharp
[HttpPost("api/safety/evaluate-input")]
public async Task<ActionResult<SafetyEvaluationResult>> EvaluateUserInput([FromBody] EvaluateRequest request)
{
    var result = await _safetyService.EvaluateUserInputAsync(request.Text);
    return Ok(result);
}
```

**Request Body:**
```json
{
  "text": "Hello, how are you today?"
}
```

**Response:**
```json
{
  "isSafe": true,
  "detectedCategories": [],
  "riskScore": 0,
  "recommendations": ["Content is safe to proceed."],
  "metadata": {
    "provider": "OpenAI Moderation",
    "processingTimeMs": 245,
    "requestId": "req_123456789",
    "evaluatedAt": "2024-01-15T10:30:00Z"
  }
}
```

#### EvaluateOutputAsync

Evaluates AI-generated output using output policy thresholds.

```csharp
[HttpPost("api/safety/evaluate-output")]
public async Task<ActionResult<SafetyEvaluationResult>> EvaluateOutput([FromBody] EvaluateRequest request)
{
    var result = await _safetyService.EvaluateOutputAsync(request.Text);
    return Ok(result);
}
```

#### EvaluateBatchAsync

Evaluates multiple texts for improved performance.

```csharp
[HttpPost("api/safety/evaluate-batch")]
public async Task<ActionResult<IReadOnlyList<SafetyEvaluationResult>>> EvaluateBatch([FromBody] BatchEvaluateRequest request)
{
    var results = await _safetyService.EvaluateBatchAsync(request.Texts);
    return Ok(results);
}
```

**Request Body:**
```json
{
  "texts": [
    "Hello world",
    "How are you?",
    "Good morning"
  ]
}
```

**Response:**
```json
[
  {
    "isSafe": true,
    "detectedCategories": [],
    "riskScore": 0,
    "recommendations": ["Content is safe to proceed."],
    "metadata": {
      "provider": "OpenAI Moderation",
      "processingTimeMs": 120
    }
  },
  {
    "isSafe": true,
    "detectedCategories": [],
    "riskScore": 0,
    "recommendations": ["Content is safe to proceed."],
    "metadata": {
      "provider": "OpenAI Moderation",
      "processingTimeMs": 115
    }
  },
  {
    "isSafe": true,
    "detectedCategories": [],
    "riskScore": 0,
    "recommendations": ["Content is safe to proceed."],
    "metadata": {
      "provider": "OpenAI Moderation",
      "processingTimeMs": 130
    }
  }
]
```

### Streaming Evaluation

#### Create Streaming Evaluator

```csharp
[HttpPost("api/safety/create-streaming-evaluator")]
public async Task<ActionResult<StreamingEvaluatorResponse>> CreateStreamingEvaluator()
{
    var evaluator = _safetyService.CreateStreamingEvaluator();
    var evaluatorId = Guid.NewGuid().ToString();
    
    // Store evaluator for subsequent calls (in a real implementation, use a distributed cache)
    _streamingEvaluators[evaluatorId] = evaluator;
    
    return Ok(new StreamingEvaluatorResponse { EvaluatorId = evaluatorId });
}
```

#### Evaluate Chunk

```csharp
[HttpPost("api/safety/evaluate-chunk/{evaluatorId}")]
public async Task<ActionResult<SafetyEvaluationResult>> EvaluateChunk(
    string evaluatorId, 
    [FromBody] ChunkEvaluateRequest request)
{
    if (!_streamingEvaluators.TryGetValue(evaluatorId, out var evaluator))
    {
        return NotFound("Streaming evaluator not found");
    }

    var result = await evaluator.EvaluateChunkAsync(request.Chunk);
    
    if (!result.IsSafe)
    {
        // Clean up evaluator on violation
        evaluator.Dispose();
        _streamingEvaluators.Remove(evaluatorId);
    }

    return Ok(result);
}
```

**Request Body:**
```json
{
  "chunk": "Hello, I wanted to tell you that"
}
```

## Configuration API

### Get Safety Configuration

```csharp
[HttpGet("api/safety/config")]
public ActionResult<SafetyConfiguration> GetSafetyConfiguration()
{
    var status = _safetyService.GetSafetyStatus();
    var config = new SafetyConfiguration
    {
        IsEnabled = status.IsEnabled,
        Provider = status.Provider,
        SupportedCategories = status.SupportedCategories.ToList(),
        InputPolicy = status.InputPolicy,
        OutputPolicy = status.OutputPolicy,
        FallbackBehavior = status.FallbackBehavior
    };
    
    return Ok(config);
}
```

**Response:**
```json
{
  "isEnabled": true,
  "provider": "OpenAI Moderation",
  "supportedCategories": ["Hate", "SelfHarm", "Sexual", "Violence"],
  "inputPolicy": {
    "thresholds": {
      "hate": 4,
      "selfHarm": 6,
      "sexual": 4,
      "violence": 2
    },
    "blockOnViolation": true,
    "maxRiskScore": 70
  },
  "outputPolicy": {
    "thresholds": {
      "hate": 2,
      "selfHarm": 4,
      "sexual": 2,
      "violence": 2
    },
    "blockOnViolation": true,
    "maxRiskScore": 50
  },
  "fallbackBehavior": "FailOpen"
}
```

### Update Safety Configuration

```csharp
[HttpPut("api/safety/config")]
public ActionResult UpdateSafetyConfiguration([FromBody] UpdateSafetyConfigRequest request)
{
    // In a real implementation, update configuration dynamically
    // This would require configuration reload mechanisms
    
    return Ok(new { message = "Configuration updated successfully" });
}
```

## Health Check API

### Safety Service Health

```csharp
[HttpGet("api/health/safety")]
public async Task<ActionResult<HealthCheckResponse>> CheckSafetyHealth()
{
    try
    {
        var evaluator = _serviceProvider.GetRequiredService<ISafetyEvaluator>();
        var result = await evaluator.EvaluateTextAsync("Hello world");
        
        var health = new HealthCheckResponse
        {
            Status = result != null ? "Healthy" : "Degraded",
            Provider = evaluator.GetProviderName(),
            LastCheck = DateTimeOffset.UtcNow,
            ResponseTimeMs = result?.Metadata?.ProcessingTimeMs ?? 0
        };
        
        return Ok(health);
    }
    catch (Exception ex)
    {
        return StatusCode(503, new HealthCheckResponse
        {
            Status = "Unhealthy",
            Error = ex.Message,
            LastCheck = DateTimeOffset.UtcNow
        });
    }
}
```

**Response:**
```json
{
  "status": "Healthy",
  "provider": "OpenAI Moderation",
  "lastCheck": "2024-01-15T10:30:00Z",
  "responseTimeMs": 245
}
```

## Models and Data Structures

### SafetyEvaluationResult

```csharp
public class SafetyEvaluationResult
{
    public bool IsSafe { get; set; }
    public List<DetectedHarmCategory> DetectedCategories { get; set; } = new();
    public int RiskScore { get; set; }
    public EvaluationMetadata? Metadata { get; set; }
    public List<string> Recommendations { get; set; } = new();
}
```

### DetectedHarmCategory

```csharp
public class DetectedHarmCategory
{
    public HarmCategory Category { get; set; }
    public int Severity { get; set; }           // 0-7 severity level
    public string? Description { get; set; }
    public int Confidence { get; set; }         // 0-100 confidence score
    public List<string> TriggeringSegments { get; set; } = new();
}
```

### EvaluationMetadata

```csharp
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

### HarmCategory

```csharp
public enum HarmCategory
{
    Hate,           // Hate speech
    SelfHarm,       // Self-harm content
    Sexual,         // Sexually explicit content
    Violence,       // Violent content
    Suggestive,     // Sexually suggestive content
    Profanity,      // Profane language
    PersonalData,   // Personal identifiable information
    AgeInappropriate // Age-inappropriate content
}
```

### Request/Response Models

```csharp
public class EvaluateRequest
{
    public string Text { get; set; } = string.Empty;
}

public class BatchEvaluateRequest
{
    public List<string> Texts { get; set; } = new();
}

public class ChunkEvaluateRequest
{
    public string Chunk { get; set; } = string.Empty;
}

public class StreamingEvaluatorResponse
{
    public string EvaluatorId { get; set; } = string.Empty;
}

public class SafetyConfiguration
{
    public bool IsEnabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public List<HarmCategory> SupportedCategories { get; set; } = new();
    public PolicySettings InputPolicy { get; set; } = new();
    public PolicySettings OutputPolicy { get; set; } = new();
    public FallbackBehavior FallbackBehavior { get; set; }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset LastCheck { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? Error { get; set; }
}
```

## Error Handling

### Standard Error Response Format

```json
{
  "error": {
    "code": "SAFETY_EVALUATION_FAILED",
    "message": "Failed to evaluate content due to service unavailability",
    "details": {
      "provider": "OpenAI Moderation",
      "requestId": "req_123456789",
      "timestamp": "2024-01-15T10:30:00Z"
    }
  }
}
```

### Common Error Codes

| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| `SAFETY_EVALUATION_FAILED` | 500 | Safety evaluation service failed |
| `CONTENT_TOO_LONG` | 400 | Content exceeds maximum length limit |
| `INVALID_REQUEST` | 400 | Request format is invalid |
| `RATE_LIMIT_EXCEEDED` | 429 | Too many requests in time window |
| `SERVICE_UNAVAILABLE` | 503 | Safety service is temporarily unavailable |
| `UNAUTHORIZED` | 401 | Invalid or missing API credentials |
| `STREAMING_EVALUATOR_NOT_FOUND` | 404 | Streaming evaluator ID not found |

### Error Handling Best Practices

```csharp
[HttpPost("api/safety/evaluate-input")]
public async Task<ActionResult<SafetyEvaluationResult>> EvaluateUserInput([FromBody] EvaluateRequest request)
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = { code = "INVALID_REQUEST", message = "Text cannot be empty" } });
        }

        if (request.Text.Length > 10000)
        {
            return BadRequest(new { error = { code = "CONTENT_TOO_LONG", message = "Text exceeds maximum length" } });
        }

        var result = await _safetyService.EvaluateUserInputAsync(request.Text);
        return Ok(result);
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "HTTP request failed during safety evaluation");
        return StatusCode(503, new { error = { 
            code = "SERVICE_UNAVAILABLE", 
            message = "Safety service temporarily unavailable" 
        }});
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during safety evaluation");
        return StatusCode(500, new { error = { 
            code = "SAFETY_EVALUATION_FAILED", 
            message = "Internal server error during safety evaluation" 
        }});
    }
}
```

## Usage Examples

### Basic Content Evaluation

```csharp
// Evaluate user input
var userResult = await safetyService.EvaluateUserInputAsync("Hello, how are you?");
if (!userResult.IsSafe)
{
    Console.WriteLine($"Content blocked: {string.Join(", ", userResult.DetectedCategories.Select(c => c.Category))}");
    return;
}

// Evaluate AI output
var aiResult = await safetyService.EvaluateOutputAsync("I'm doing well, thank you for asking!");
if (!aiResult.IsSafe)
{
    Console.WriteLine($"AI response blocked: {string.Join(", ", aiResult.DetectedCategories.Select(c => c.Category))}");
    return;
}

Console.WriteLine("Content is safe to proceed");
```

### Streaming Evaluation

```csharp
// Create streaming evaluator
using var streamingEvaluator = safetyService.CreateStreamingEvaluator();

// Simulate AI response chunks
var chunks = new[] { "Hello, ", "I wanted ", "to tell you ", "something important." };

foreach (var chunk in chunks)
{
    var result = await streamingEvaluator.EvaluateChunkAsync(chunk);
    
    if (!result.IsSafe)
    {
        Console.WriteLine($"Violation detected in chunk: {chunk}");
        Console.WriteLine($"Categories: {string.Join(", ", result.DetectedCategories.Select(c => c.Category))}");
        break;
    }
    
    Console.WriteLine($"Chunk safe: {chunk}");
}

Console.WriteLine($"Total chunks processed: {streamingEvaluator.GetProcessedChunkCount()}");
Console.WriteLine($"Violations detected: {streamingEvaluator.HasViolations()}");
```

### Batch Evaluation

```csharp
var messages = new[]
{
    "Hello world",
    "How are you today?",
    "I hate everyone",  // This should be flagged
    "Good morning"
};

var results = await safetyService.EvaluateBatchAsync(messages);

for (int i = 0; i < messages.Length; i++)
{
    var message = messages[i];
    var result = results[i];
    
    Console.WriteLine($"Message: {message}");
    Console.WriteLine($"Safe: {result.IsSafe}");
    Console.WriteLine($"Risk Score: {result.RiskScore}");
    
    if (!result.IsSafe)
    {
        Console.WriteLine($"Violations: {string.Join(", ", result.DetectedCategories.Select(c => $"{c.Category}({c.Severity})"))}");
    }
    
    Console.WriteLine();
}
```

### HTTP Client Usage

```csharp
using var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri("https://your-api.com");

// Evaluate content
var evaluateRequest = new { text = "Hello, how are you?" };
var response = await httpClient.PostAsJsonAsync("/api/safety/evaluate-input", evaluateRequest);
var result = await response.Content.ReadFromJsonAsync<SafetyEvaluationResult>();

Console.WriteLine($"Content is safe: {result.IsSafe}");

// Get safety configuration
var configResponse = await httpClient.GetAsync("/api/safety/config");
var config = await configResponse.Content.ReadFromJsonAsync<SafetyConfiguration>();

Console.WriteLine($"Safety enabled: {config.IsEnabled}");
Console.WriteLine($"Provider: {config.Provider}");
```

## Performance Considerations

### Optimization Strategies

1. **Batch Processing**: Evaluate multiple texts together for better throughput
2. **Caching**: Cache results for repeated content
3. **Async Operations**: Use async/await throughout
4. **Connection Pooling**: Reuse HTTP connections
5. **Timeout Management**: Set appropriate timeouts

### Performance Metrics

```csharp
public class SafetyPerformanceMetrics
{
    public long AverageEvaluationTimeMs { get; set; }
    public long P95EvaluationTimeMs { get; set; }
    public long P99EvaluationTimeMs { get; set; }
    public double RequestsPerSecond { get; set; }
    public double ErrorRate { get; set; }
    public int CacheHitRate { get; set; }
}

// Collect metrics
var stopwatch = Stopwatch.StartNew();
var result = await safetyService.EvaluateUserInputAsync(text);
stopwatch.Stop();

// Record metrics
_metrics.RecordEvaluationTime(stopwatch.ElapsedMilliseconds);
_metrics.RecordEvaluationResult(result.IsSafe);
```

### Caching Implementation

```csharp
public class CachedSafetyEvaluator : ISafetyEvaluator
{
    private readonly ISafetyEvaluator _innerEvaluator;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration;

    public CachedSafetyEvaluator(ISafetyEvaluator innerEvaluator, IMemoryCache cache, TimeSpan expiration)
    {
        _innerEvaluator = innerEvaluator;
        _cache = cache;
        _cacheExpiration = expiration;
    }

    public async Task<SafetyEvaluationResult> EvaluateTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"safety:{ComputeHash(text)}";
        
        if (_cache.TryGetValue(cacheKey, out SafetyEvaluationResult? cachedResult))
        {
            return cachedResult!;
        }

        var result = await _innerEvaluator.EvaluateTextAsync(text, cancellationToken);
        _cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }

    private string ComputeHash(string text)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
```

### Rate Limiting

```csharp
public class RateLimitedSafetyEvaluator : ISafetyEvaluator
{
    private readonly ISafetyEvaluator _innerEvaluator;
    private readonly SemaphoreSlim _rateLimiter;

    public RateLimitedSafetyEvaluator(ISafetyEvaluator innerEvaluator, int maxConcurrentRequests)
    {
        _innerEvaluator = innerEvaluator;
        _rateLimiter = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
    }

    public async Task<SafetyEvaluationResult> EvaluateTextAsync(string text, CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            return await _innerEvaluator.EvaluateTextAsync(text, cancellationToken);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}
```

---

For additional information, see:
- [RESPONSIBLE_AI.md](RESPONSIBLE_AI.md) - Implementation guide
- [RAI-Architecture.md](RAI-Architecture.md) - Technical architecture
- [SAINTY_CONFIGURATION_DOCUMENTATION.md](SAINTY_CONFIGURATION_DOCUMENTATION.md) - Configuration options