# Safety Configuration Documentation

This document explains the comprehensive safety configuration options available in the AIChat application's safety system.

## Configuration Structure

The safety configuration is organized into several main sections:

### Safety.Enabled
**Type:** boolean  
**Default:** true  
**Description:** Enable/disable the entire safety evaluation system. When set to false, all safety checks are bypassed.

### Safety.OpenAI
Contains OpenAI Moderation API configuration settings.

#### OpenAI.ApiKey
**Type:** string
**Default:** "" (empty)
**Description:** API key for OpenAI moderation service. **Should be stored in user secrets for security.**

#### OpenAI.OrganizationId
**Type:** string
**Default:** "" (empty)
**Description:** OpenAI organization ID (optional, for organizational access control and billing).

#### OpenAI.Model
**Type:** string
**Default:** "omni-moderation-latest"
**Description:** OpenAI moderation model to use. Options include:
- "omni-moderation-latest" - Most recent model with comprehensive categories
- "text-moderation-latest" - Legacy text moderation model
- "text-moderation-stable" - Stable model with consistent behavior

#### OpenAI.Endpoint
**Type:** string
**Default:** "https://api.openai.com/v1/moderations"
**Description:** OpenAI API endpoint for moderation. Can be overridden for custom endpoints or proxy configurations.

## API Key Configuration

### User Secrets (Recommended for Development)

The OpenAI API key should be stored securely using .NET User Secrets. This prevents accidental exposure of sensitive API keys in source control.

#### Setting up User Secrets

1. **Initialize User Secrets** (if not already done):
   ```bash
   dotnet user-secrets init
   ```

2. **Set the OpenAI API Key**:
   ```bash
   dotnet user-secrets set "OpenAI:ApiKey" "your-actual-openai-api-key-here"
   ```

3. **Verify the secret**:
   ```bash
   dotnet user-secrets list
   ```

#### API Key Resolution Order

The safety system will look for the OpenAI API key in the following order of precedence:

1. **`Safety:ApiKey`** - Direct safety configuration (backward compatibility)
2. **`Safety:OpenAI:ApiKey`** - Nested OpenAI configuration
3. **`OpenAI:ApiKey`** - Standard OpenAI configuration path (recommended)
4. **`OPENAI_API_KEY`** - Environment variable
5. **Direct ApiKey property** - Fallback for legacy configurations

#### Production Deployment

For production environments, use one of the following secure methods:

1. **Azure Key Vault** - Recommended for Azure deployments
2. **Environment Variables** - For containerized deployments
3. **AWS Secrets Manager** - For AWS deployments
4. **Other secret management systems** - Based on your infrastructure

Example environment variable:
```bash
export OPENAI_API_KEY="your-production-api-key"
```

### Safety.FallbackBehavior
**Type:** string  
**Default:** "FailOpen"  
**Description:** Behavior when safety service is unavailable. Options:
- "FailOpen" - Allow content to proceed (less restrictive)
- "FailClosed" - Block all content (more restrictive)
- "FailRetry" - Retry with exponential backoff

## Content Policies

### Safety.InputPolicy
Applied to user prompts and incoming requests.

#### InputPolicy.Thresholds
**Type:** object  
**Description:** Harm category thresholds (0-7 scale, where 7 is most severe):
- **Hate:** 4 - Hate speech threshold
- **SelfHarm:** 6 - Self-harm content threshold
- **Sexual:** 4 - Sexually explicit content threshold
- **Violence:** 2 - Violent content threshold

#### InputPolicy.BlockOnViolation
**Type:** boolean  
**Default:** true  
**Description:** Block content when any threshold is exceeded.

#### InputPolicy.MaxRiskScore
**Type:** integer  
**Default:** 70  
**Description:** Maximum aggregate risk score before blocking (0-100 scale).

### Safety.OutputPolicy
Applied to AI-generated responses (typically more lenient than input).

#### OutputPolicy.Thresholds
**Type:** object  
**Description:** Harm category thresholds for output content:
- **Hate:** 2 - Hate speech threshold
- **SelfHarm:** 4 - Self-harm content threshold
- **Sexual:** 2 - Sexually explicit content threshold
- **Violence:** 2 - Violent content threshold

#### OutputPolicy.BlockOnViolation
**Type:** boolean  
**Default:** true  
**Description:** Block content when any threshold is exceeded.

#### OutputPolicy.MaxRiskScore
**Type:** integer  
**Default:** 50  
**Description:** Maximum aggregate risk score before blocking (0-100 scale).

## Performance and Resilience

### Safety.Resilience
Contains settings for handling failures and performance optimization.

#### Resilience.TimeoutInMilliseconds
**Type:** integer  
**Default:** 5000  
**Description:** Timeout for safety evaluation requests in milliseconds.

#### Resilience.MaxRetries
**Type:** integer  
**Default:** 2  
**Description:** Maximum number of retry attempts for failed safety checks.

#### Resilience.CircuitBreakerThreshold
**Type:** integer  
**Default:** 5  
**Description:** Consecutive failures before opening circuit breaker.

#### Resilience.CircuitBreakerDurationInSeconds
**Type:** integer  
**Default:** 30  
**Description:** Circuit breaker duration in seconds before attempting to close.

#### Resilience.UseExponentialBackoff
**Type:** boolean  
**Default:** true  
**Description:** Use exponential backoff for retry attempts.

#### Resilience.BaseRetryDelayInMilliseconds
**Type:** integer  
**Default:** 1000  
**Description:** Base delay for exponential backoff in milliseconds.

#### Resilience.MaxRetryDelayInMilliseconds
**Type:** integer  
**Default:** 10000  
**Description:** Maximum delay for exponential backoff in milliseconds.

## Audit Logging

### Safety.Audit
Contains audit logging configuration for compliance and monitoring.

#### Audit.Enabled
**Type:** boolean  
**Default:** true  
**Description:** Enable audit logging for safety evaluations.

#### Audit.LogFullContent
**Type:** boolean  
**Default:** false  
**Description:** Log full content (WARNING: may contain sensitive data and PII).

#### Audit.LogContentHashes
**Type:** boolean  
**Default:** true  
**Description:** Log content hashes instead of full content for privacy.

#### Audit.LogMetadata
**Type:** boolean  
**Default:** true  
**Description:** Log metadata (timestamps, user IDs, model info, etc.).

#### Audit.LogDetailedScores
**Type:** boolean  
**Default:** true  
**Description:** Log detailed harm category scores.

#### Audit.RetentionPeriodInDays
**Type:** integer  
**Default:** 90  
**Description:** Retention period for audit logs in days.

## Advanced Settings

### Safety.Advanced
Contains advanced safety evaluation features.

#### Advanced.EnableStreamingEvaluation
**Type:** boolean  
**Default:** true  
**Description:** Enable streaming safety evaluation for real-time content filtering.

#### Advanced.StreamingBufferSize
**Type:** integer  
**Default:** 50  
**Description:** Buffer size for streaming evaluation (number of tokens to evaluate at once).

#### Advanced.StreamingEvaluationInterval
**Type:** integer  
**Default:** 1000  
**Description:** Interval between streaming evaluations in milliseconds.

#### Advanced.EnableContextAwareEvaluation
**Type:** boolean  
**Default:** false  
**Description:** Enable context-aware safety evaluation (considers conversation context).

#### Advanced.CustomRulesPath
**Type:** string  
**Default:** "" (empty)  
**Description:** Path to custom safety evaluation rules file (JSON format).

#### Advanced.EnableCaching
**Type:** boolean  
**Default:** true  
**Description:** Enable safety evaluation caching for repeated content.

#### Advanced.CacheExpirationInMinutes
**Type:** integer  
**Default:** 30  
**Description:** Cache expiration time in minutes.

## Security Considerations

1. **API Key Management:**
   - **Never** store API keys in appsettings.json files
   - **Always** use user secrets for development
   - **Always** use secure secret management for production
   - Rotate API keys regularly and monitor usage

2. **User Secrets Best Practices:**
   - User secrets are stored in your user profile directory, not in source control
   - They're automatically loaded in development environment
   - They're not available in production (use appropriate secret management)
   - Never commit secrets.json files to version control

3. **Audit Logging:** Be cautious with `LogFullContent: true` as it may expose sensitive user data in logs.

4. **Threshold Tuning:** Regularly review and adjust thresholds based on your specific use case and compliance requirements.

5. **Circuit Breaker:** Monitor circuit breaker events to identify service health issues.

6. **Performance:** Consider the impact of safety evaluations on response times, especially with streaming enabled.

## Configuration Examples

### High Security Configuration
```json
{
  "Safety": {
    "Enabled": true,
    "FallbackBehavior": "FailClosed",
    "InputPolicy": {
      "Thresholds": {
        "Hate": 2,
        "SelfHarm": 2,
        "Sexual": 2,
        "Violence": 1
      },
      "MaxRiskScore": 30
    },
    "OutputPolicy": {
      "Thresholds": {
        "Hate": 1,
        "SelfHarm": 1,
        "Sexual": 1,
        "Violence": 1
      },
      "MaxRiskScore": 20
    }
  }
}
```

### Performance Optimized Configuration
```json
{
  "Safety": {
    "Enabled": true,
    "Resilience": {
      "TimeoutInMilliseconds": 2000,
      "MaxRetries": 1,
      "UseExponentialBackoff": false
    },
    "Advanced": {
      "EnableCaching": true,
      "CacheExpirationInMinutes": 60,
      "EnableStreamingEvaluation": false
    }
  }
}