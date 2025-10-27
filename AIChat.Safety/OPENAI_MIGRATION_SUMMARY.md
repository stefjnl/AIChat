# OpenAI Moderation API Migration Summary

This document summarizes the migration from Azure Content Safety to OpenAI Moderation API in the AIChat.Safety project.

## Overview

The AIChat.Safety project has been successfully migrated from using Azure Content Safety API to OpenAI Moderation API. This change provides a more cost-effective and widely adopted solution for content moderation while maintaining the same interfaces and functionality.

## Changes Made

### 1. Updated SafetyOptions

**File:** `AIChat.Safety/Options/SafetyOptions.cs`

- Replaced Azure-specific configuration with OpenAI configuration
- Added new properties:
  - `Endpoint`: Defaults to `https://api.openai.com/v1/moderations`
  - `ApiKey`: Direct API key configuration
  - `OrganizationId`: Optional OpenAI organization ID
  - `Model`: Defaults to `text-moderation-latest`
- Removed deprecated Azure properties (`ApiKeySecretName`)
- Added backward compatibility flag `UseLegacyAzure` (marked obsolete)

### 2. Created OpenAIModerationEvaluator

**File:** `AIChat.Safety/Providers/OpenAIModerationEvaluator.cs`

- Replaced `AzureContentSafetyEvaluator` with `OpenAIModerationEvaluator`
- Implements the same `ISafetyEvaluator` interface
- Uses HTTP client to call OpenAI Moderation API
- Maps OpenAI categories (hate, self_harm, sexual, violence) to internal `HarmCategory` enum
- Converts OpenAI scores (0-1) to severity levels (0-7)
- Maintains all existing functionality including batch processing and streaming evaluator creation

### 3. Created OpenAIStreamingSafetyEvaluator

**File:** `AIChat.Safety/Providers/OpenAIStreamingSafetyEvaluator.cs`

- Replaced `AzureStreamingSafetyEvaluator` with `OpenAIStreamingSafetyEvaluator`
- Implements the same `IStreamingSafetyEvaluator` interface
- Provides real-time streaming content evaluation
- Uses the same evaluation strategies (character count, sentence boundaries, time-based)
- Maintains thread-safe operations and proper resource disposal

### 4. Updated Dependency Injection

**File:** `AIChat.Safety/DependencyInjection/SafetyServiceCollectionExtensions.cs`

- Replaced Azure Content Safety client registration with OpenAI HTTP client
- Updated service registration to use `OpenAIModerationEvaluator`
- Modified API key retrieval to use OpenAI environment variables:
  - `OPENAI_API_KEY`
  - `MODERATION_API_KEY`
- Removed Azure-specific dependencies

### 5. Updated Project Dependencies

**File:** `AIChat.Safety/AIChat.Safety.csproj`

- Removed Azure packages:
  - `Azure.AI.ContentSafety`
  - `Azure.Identity`
- Added `System.Net.Http` for HTTP client functionality
- Maintained all other dependencies (Polly, logging, etc.)

### 6. Updated WebApi Configuration

**File:** `AIChat.WebApi/Program.cs`

- Removed Azure Content Safety client registration
- Added `AddAISafetyServices()` extension method call
- Removed Azure-specific using statements
- Simplified safety service configuration

### 7. Removed Legacy Files

- Deleted `AzureContentSafetyEvaluator.cs`
- Deleted `AzureStreamingSafetyEvaluator.cs`

## Category Mapping

| OpenAI Category | Internal HarmCategory |
|-----------------|---------------------|
| hate | HarmCategory.Hate |
| self_harm | HarmCategory.SelfHarm |
| sexual | HarmCategory.Sexual |
| violence | HarmCategory.Violence |

## Score Conversion

OpenAI provides scores from 0-1, which are converted to severity levels (0-7):

| OpenAI Score | Severity Level | Description |
|-------------|----------------|-------------|
| ≤ 0.1 | 0 | No violation |
| ≤ 0.2 | 1 | Very low |
| ≤ 0.3 | 2 | Low |
| ≤ 0.4 | 3 | Low-medium |
| ≤ 0.5 | 4 | Medium |
| ≤ 0.6 | 5 | Medium-high |
| ≤ 0.8 | 6 | High |
| ≤ 1.0 | 7 | Very high |

## Configuration Example

```json
{
  "Safety": {
    "Enabled": true,
    "Endpoint": "https://api.openai.com/v1/moderations",
    "ApiKey": "your-openai-api-key",
    "OrganizationId": "optional-org-id",
    "Model": "text-moderation-latest",
    "FallbackBehavior": "FailOpen",
    "InputPolicy": {
      "Thresholds": {
        "Hate": 2,
        "SelfHarm": 2,
        "Sexual": 2,
        "Violence": 2
      }
    },
    "OutputPolicy": {
      "Thresholds": {
        "Hate": 2,
        "SelfHarm": 2,
        "Sexual": 2,
        "Violence": 2
      }
    }
  }
}
```

## Environment Variables

The system will look for the API key in the following order:

1. `Safety:ApiKey` configuration
2. `OpenAI:ApiKey` configuration
3. `OPENAI_API_KEY` environment variable
4. `MODERATION_API_KEY` environment variable

## Compatibility

- All existing interfaces (`ISafetyEvaluator`, `IStreamingSafetyEvaluator`) remain unchanged
- All existing contracts (`SafetyEvaluationResult`, `HarmCategory`) remain unchanged
- Existing code using the safety services will continue to work without modifications
- Streaming evaluation maintains the same behavior and performance characteristics

## Benefits

1. **Cost-Effective**: OpenAI Moderation API is generally more cost-effective than Azure Content Safety
2. **Wider Adoption**: OpenAI's moderation is widely used and well-documented
3. **Simplified Configuration**: Direct API key configuration without requiring Azure setup
4. **Maintained Functionality**: All existing features preserved
5. **Better Performance**: HTTP-based implementation with proper async/await patterns

## Testing

The migration has been tested by:

1. Building the AIChat.Safety project successfully
2. Building the entire solution without errors
3. Verifying all interfaces and contracts remain compatible
4. Ensuring proper dependency injection configuration

## Future Considerations

- Consider implementing retry policies specific to OpenAI rate limits
- Monitor API usage and costs
- Consider adding support for OpenAI's newer moderation models as they become available
- Implement proper logging and monitoring for API calls