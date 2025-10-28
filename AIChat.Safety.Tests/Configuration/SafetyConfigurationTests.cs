using AIChat.Safety.DependencyInjection;
using AIChat.Safety.Options;
using AIChat.Safety.Contracts;
using AIChat.Safety.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIChat.Safety.Tests.Configuration;

/// <summary>
/// Tests for safety configuration validation and dependency injection setup.
/// </summary>
public class SafetyConfigurationTests
{
    /// <summary>
    /// Tests that safety options are correctly bound from configuration.
    /// </summary>
    [Fact]
    public void SafetyOptions_BindFromConfiguration_CorrectlyMapsAllProperties()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["Safety:Enabled"] = "true",
            ["Safety:Endpoint"] = "https://api.openai.com/v1/moderations",
            ["Safety:ApiKey"] = "test-api-key",
            ["Safety:OrganizationId"] = "test-org-id",
            ["Safety:Model"] = "text-moderation-latest",
            ["Safety:FallbackBehavior"] = "FailOpen",
            ["Safety:InputPolicy:BlockOnViolation"] = "true",
            ["Safety:InputPolicy:RequireMultipleCategories"] = "false",
            ["Safety:InputPolicy:MinimumCategoryViolations"] = "2",
            ["Safety:InputPolicy:MaxRiskScore"] = "70",
            ["Safety:InputPolicy:Thresholds:Hate"] = "2",
            ["Safety:InputPolicy:Thresholds:SelfHarm"] = "2",
            ["Safety:InputPolicy:Thresholds:Sexual"] = "2",
            ["Safety:InputPolicy:Thresholds:Violence"] = "2",
            ["Safety:OutputPolicy:BlockOnViolation"] = "true",
            ["Safety:OutputPolicy:RequireMultipleCategories"] = "false",
            ["Safety:OutputPolicy:MinimumCategoryViolations"] = "2",
            ["Safety:OutputPolicy:MaxRiskScore"] = "60",
            ["Safety:OutputPolicy:Thresholds:Hate"] = "3",
            ["Safety:OutputPolicy:Thresholds:SelfHarm"] = "2",
            ["Safety:OutputPolicy:Thresholds:Sexual"] = "3",
            ["Safety:OutputPolicy:Thresholds:Violence"] = "3",
            ["Safety:Resilience:TimeoutInMilliseconds"] = "3000",
            ["Safety:Resilience:MaxRetries"] = "2",
            ["Safety:Resilience:RetryDelayInMilliseconds"] = "1000",
            ["Safety:Resilience:CircuitBreakerThreshold"] = "5",
            ["Safety:Resilience:CircuitBreakerDurationInSeconds"] = "30",
            ["Safety:Resilience:UseExponentialBackoff"] = "true",
            ["Safety:Resilience:MaxBackoffMultiplier"] = "8.0",
            ["Safety:Filtering:Enabled"] = "false",
            ["Safety:Filtering:DefaultAction"] = "Mask",
            ["Safety:Filtering:MaskCharacter"] = "*",
            ["Safety:Filtering:RedactionText"] = "[REDACTED]",
            ["Safety:Filtering:PreserveLength"] = "true",
            ["Safety:Audit:Enabled"] = "true",
            ["Safety:Audit:LogFullContent"] = "false",
            ["Safety:Audit:LogContentHashes"] = "true",
            ["Safety:Audit:LogMetadata"] = "true",
            ["Safety:Audit:RetentionDays"] = "90",
            ["Safety:Audit:AlertThreshold"] = "4",
            ["Safety:RateLimit:Enabled"] = "false",
            ["Safety:RateLimit:MaxEvaluationsPerWindow"] = "1000",
            ["Safety:RateLimit:WindowInSeconds"] = "60",
            ["Safety:RateLimit:PerUserLimiting"] = "true",
            ["Safety:RateLimit:PerIPLimiting"] = "false",
            ["Safety:RateLimit:ExceededAction"] = "Throttle"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        var options = new SafetyOptions();
        configuration.GetSection("Safety").Bind(options);

        // Assert
        options.Enabled.Should().BeTrue();
        options.Endpoint.Should().Be("https://api.openai.com/v1/moderations");
        options.ApiKey.Should().Be("test-api-key");
        options.OrganizationId.Should().Be("test-org-id");
        options.Model.Should().Be("text-moderation-latest");
        options.FallbackBehavior.Should().Be(FallbackBehavior.FailOpen);

        // Input Policy
        options.InputPolicy.BlockOnViolation.Should().BeTrue();
        options.InputPolicy.RequireMultipleCategories.Should().BeFalse();
        options.InputPolicy.MinimumCategoryViolations.Should().Be(2);
        options.InputPolicy.MaxRiskScore.Should().Be(70);
        options.InputPolicy.Thresholds[HarmCategory.Hate].Should().Be(2);
        options.InputPolicy.Thresholds[HarmCategory.SelfHarm].Should().Be(2);
        options.InputPolicy.Thresholds[HarmCategory.Sexual].Should().Be(2);
        options.InputPolicy.Thresholds[HarmCategory.Violence].Should().Be(2);

        // Output Policy
        options.OutputPolicy.BlockOnViolation.Should().BeTrue();
        options.OutputPolicy.RequireMultipleCategories.Should().BeFalse();
        options.OutputPolicy.MinimumCategoryViolations.Should().Be(2);
        options.OutputPolicy.MaxRiskScore.Should().Be(60);
        options.OutputPolicy.Thresholds[HarmCategory.Hate].Should().Be(3);
        options.OutputPolicy.Thresholds[HarmCategory.SelfHarm].Should().Be(2);
        options.OutputPolicy.Thresholds[HarmCategory.Sexual].Should().Be(3);
        options.OutputPolicy.Thresholds[HarmCategory.Violence].Should().Be(3);

        // Resilience
        options.Resilience.TimeoutInMilliseconds.Should().Be(3000);
        options.Resilience.MaxRetries.Should().Be(2);
        options.Resilience.RetryDelayInMilliseconds.Should().Be(1000);
        options.Resilience.CircuitBreakerThreshold.Should().Be(5);
        options.Resilience.CircuitBreakerDurationInSeconds.Should().Be(30);
        options.Resilience.UseExponentialBackoff.Should().BeTrue();
        options.Resilience.MaxBackoffMultiplier.Should().Be(8.0);

        // Filtering
        options.Filtering.Enabled.Should().BeFalse();
        options.Filtering.DefaultAction.Should().Be(FilterActionType.Mask);
        options.Filtering.MaskCharacter.Should().Be("*");
        options.Filtering.RedactionText.Should().Be("[REDACTED]");
        options.Filtering.PreserveLength.Should().BeTrue();

        // Audit
        options.Audit.Enabled.Should().BeTrue();
        options.Audit.LogFullContent.Should().BeFalse();
        options.Audit.LogContentHashes.Should().BeTrue();
        options.Audit.LogMetadata.Should().BeTrue();
        options.Audit.RetentionDays.Should().Be(90);
        options.Audit.AlertThreshold.Should().Be(4);

        // Rate Limit
        options.RateLimit.Enabled.Should().BeFalse();
        options.RateLimit.MaxEvaluationsPerWindow.Should().Be(1000);
        options.RateLimit.WindowInSeconds.Should().Be(60);
        options.RateLimit.PerUserLimiting.Should().BeTrue();
        options.RateLimit.PerIPLimiting.Should().BeFalse();
        options.RateLimit.ExceededAction.Should().Be(RateLimitAction.Throttle);
    }

    /// <summary>
    /// Tests that default values are correctly applied when configuration is missing.
    /// </summary>
    [Fact]
    public void SafetyOptions_WithMissingConfiguration_AppliesDefaultValues()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            // Only provide minimal configuration
            ["Safety:Enabled"] = "true"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        var options = new SafetyOptions();
        configuration.GetSection("Safety").Bind(options);

        // Assert
        options.Enabled.Should().BeTrue();
        options.Endpoint.Should().Be("https://api.openai.com/v1/moderations"); // Default value
        options.ApiKey.Should().BeNull(); // Default value
        options.Model.Should().Be("omni-moderation-latest"); // Default value
        options.FallbackBehavior.Should().Be(FallbackBehavior.FailOpen); // Default value

        // Check default policy values
        options.InputPolicy.BlockOnViolation.Should().BeTrue();
        options.InputPolicy.RequireMultipleCategories.Should().BeFalse();
        options.InputPolicy.MinimumCategoryViolations.Should().Be(2);
        options.InputPolicy.MaxRiskScore.Should().Be(70);

        options.OutputPolicy.BlockOnViolation.Should().BeTrue();
        options.OutputPolicy.RequireMultipleCategories.Should().BeFalse();
        options.OutputPolicy.MinimumCategoryViolations.Should().Be(2);
        options.OutputPolicy.MaxRiskScore.Should().Be(70);

        // Check default resilience values
        options.Resilience.TimeoutInMilliseconds.Should().Be(3000);
        options.Resilience.MaxRetries.Should().Be(2);
        options.Resilience.RetryDelayInMilliseconds.Should().Be(1000);
        options.Resilience.CircuitBreakerThreshold.Should().Be(5);
        options.Resilience.CircuitBreakerDurationInSeconds.Should().Be(30);
        options.Resilience.UseExponentialBackoff.Should().BeTrue();
        options.Resilience.MaxBackoffMultiplier.Should().Be(8.0);
    }

    /// <summary>
    /// Tests that dependency injection correctly registers all safety services.
    /// </summary>
    [Fact]
    public void AddAISafetyServices_RegistersAllRequiredServices()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["Safety:Enabled"] = "true",
            ["Safety:Endpoint"] = "https://api.openai.com/v1/moderations",
            ["Safety:ApiKey"] = "test-api-key"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddAISafetyServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetRequiredService<IOptions<SafetyOptions>>().Should().NotBeNull();
        serviceProvider.GetRequiredService<ISafetyEvaluationService>().Should().NotBeNull();
        serviceProvider.GetRequiredService<ISafetyEvaluator>().Should().NotBeNull();
        
        // Verify the evaluator is the expected type
        var evaluator = serviceProvider.GetRequiredService<ISafetyEvaluator>();
        evaluator.GetType().Name.Should().Be("OpenAIModerationEvaluator");
    }

    /// <summary>
    /// Tests that AddAISafetyServices with custom configuration works correctly.
    /// </summary>
    [Fact]
    public void AddAISafetyServices_WithCustomConfiguration_UsesCustomOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAISafetyServices(options =>
        {
            options.Enabled = false;
            options.ApiKey = "custom-key";
            options.FallbackBehavior = FallbackBehavior.FailClosed;
            options.Resilience.TimeoutInMilliseconds = 5000;
        });

        var serviceProvider = services.BuildServiceProvider();
        var safetyOptions = serviceProvider.GetRequiredService<IOptions<SafetyOptions>>().Value;

        // Assert
        safetyOptions.Enabled.Should().BeFalse();
        safetyOptions.ApiKey.Should().Be("custom-key");
        safetyOptions.FallbackBehavior.Should().Be(FallbackBehavior.FailClosed);
        safetyOptions.Resilience.TimeoutInMilliseconds.Should().Be(5000);
    }

    /// <summary>
    /// Tests that development configuration is applied correctly.
    /// </summary>
    [Fact]
    public void AddDevelopmentSafety_AppliesDevelopmentDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAISafetyServices(_ => { }); // Basic setup

        // Act
        services.AddDevelopmentSafety();
        var serviceProvider = services.BuildServiceProvider();
        var safetyOptions = serviceProvider.GetRequiredService<IOptions<SafetyOptions>>().Value;

        // Assert
        safetyOptions.Enabled.Should().BeTrue();
        safetyOptions.FallbackBehavior.Should().Be(FallbackBehavior.FailOpen);
        safetyOptions.Resilience.TimeoutInMilliseconds.Should().Be(5000);
        safetyOptions.Resilience.MaxRetries.Should().Be(1);
        safetyOptions.Audit.Enabled.Should().BeFalse();
        safetyOptions.RateLimit.Enabled.Should().BeFalse();
    }

    /// <summary>
    /// Tests that production configuration is applied correctly.
    /// </summary>
    [Fact]
    public void AddProductionSafety_AppliesProductionDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAISafetyServices(_ => { }); // Basic setup

        // Act
        services.AddProductionSafety();
        var serviceProvider = services.BuildServiceProvider();
        var safetyOptions = serviceProvider.GetRequiredService<IOptions<SafetyOptions>>().Value;

        // Assert
        safetyOptions.Enabled.Should().BeTrue();
        safetyOptions.FallbackBehavior.Should().Be(FallbackBehavior.FailClosed);
        safetyOptions.Resilience.TimeoutInMilliseconds.Should().Be(3000);
        safetyOptions.Resilience.MaxRetries.Should().Be(3);
        safetyOptions.Audit.Enabled.Should().BeTrue();
        safetyOptions.Audit.LogFullContent.Should().BeFalse();
        safetyOptions.Audit.LogContentHashes.Should().BeTrue();
        safetyOptions.RateLimit.Enabled.Should().BeTrue();
    }

    /// <summary>
    /// Tests that enum values are correctly parsed from configuration.
    /// </summary>
    [Theory]
    [InlineData("FailOpen", FallbackBehavior.FailOpen)]
    [InlineData("FailClosed", FallbackBehavior.FailClosed)]
    [InlineData("Mask", FilterActionType.Mask)]
    [InlineData("Redact", FilterActionType.Redact)]
    [InlineData("Remove", FilterActionType.Remove)]
    [InlineData("Replace", FilterActionType.Replace)]
    [InlineData("Reject", RateLimitAction.Reject)]
    [InlineData("Throttle", RateLimitAction.Throttle)]
    [InlineData("Queue", RateLimitAction.Queue)]
    [InlineData("LogOnly", RateLimitAction.LogOnly)]
    public void SafetyOptions_WithEnumValues_ParsesCorrectly(string configValue, object expectedEnum)
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["Safety:Enabled"] = "true"
        };

        // Add the specific enum value to test
        if (expectedEnum is FallbackBehavior fallbackBehavior)
        {
            configurationData["Safety:FallbackBehavior"] = configValue;
        }
        else if (expectedEnum is FilterActionType filterAction)
        {
            configurationData["Safety:Filtering:DefaultAction"] = configValue;
        }
        else if (expectedEnum is RateLimitAction rateLimitAction)
        {
            configurationData["Safety:RateLimit:ExceededAction"] = configValue;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        var options = new SafetyOptions();
        configuration.GetSection("Safety").Bind(options);

        // Assert
        if (expectedEnum is FallbackBehavior fb)
        {
            options.FallbackBehavior.Should().Be(fb);
        }
        else if (expectedEnum is FilterActionType fa)
        {
            options.Filtering.DefaultAction.Should().Be(fa);
        }
        else if (expectedEnum is RateLimitAction rl)
        {
            options.RateLimit.ExceededAction.Should().Be(rl);
        }
    }

    /// <summary>
    /// Tests that invalid enum values fall back to defaults.
    /// </summary>
    [Fact]
    public void SafetyOptions_WithInvalidEnumValues_UsesDefaults()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["Safety:Enabled"] = "true",
            ["Safety:FallbackBehavior"] = "InvalidValue",
            ["Safety:Filtering:DefaultAction"] = "InvalidAction",
            ["Safety:RateLimit:ExceededAction"] = "InvalidRateLimit"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        var options = new SafetyOptions();
        configuration.GetSection("Safety").Bind(options);

        // Assert
        options.FallbackBehavior.Should().Be(FallbackBehavior.FailOpen); // Default
        options.Filtering.DefaultAction.Should().Be(FilterActionType.Mask); // Default
        options.RateLimit.ExceededAction.Should().Be(RateLimitAction.Throttle); // Default
    }

    /// <summary>
    /// Tests that numeric values are correctly parsed with validation.
    /// </summary>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("-1", -1)]
    [InlineData("999999", 999999)]
    public void SafetyOptions_WithNumericValues_ParsesCorrectly(string configValue, int expectedValue)
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["Safety:Enabled"] = "true",
            ["Safety:Resilience:TimeoutInMilliseconds"] = configValue,
            ["Safety:Resilience:MaxRetries"] = configValue,
            ["Safety:InputPolicy:MaxRiskScore"] = configValue,
            ["Safety:OutputPolicy:MaxRiskScore"] = configValue,
            ["Safety:Audit:RetentionDays"] = configValue,
            ["Safety:RateLimit:MaxEvaluationsPerWindow"] = configValue,
            ["Safety:RateLimit:WindowInSeconds"] = configValue
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        var options = new SafetyOptions();
        configuration.GetSection("Safety").Bind(options);

        // Assert
        options.Resilience.TimeoutInMilliseconds.Should().Be(expectedValue);
        options.Resilience.MaxRetries.Should().Be(expectedValue);
        options.InputPolicy.MaxRiskScore.Should().Be(expectedValue);
        options.OutputPolicy.MaxRiskScore.Should().Be(expectedValue);
        options.Audit.RetentionDays.Should().Be(expectedValue);
        options.RateLimit.MaxEvaluationsPerWindow.Should().Be(expectedValue);
        options.RateLimit.WindowInSeconds.Should().Be(expectedValue);
    }

    /// <summary>
    /// Tests that boolean values are correctly parsed.
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void SafetyOptions_WithBooleanValues_ParsesCorrectly(string configValue, bool expectedValue)
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["Safety:Enabled"] = configValue,
            ["Safety:InputPolicy:BlockOnViolation"] = configValue,
            ["Safety:InputPolicy:RequireMultipleCategories"] = configValue,
            ["Safety:OutputPolicy:BlockOnViolation"] = configValue,
            ["Safety:OutputPolicy:RequireMultipleCategories"] = configValue,
            ["Safety:Resilience:UseExponentialBackoff"] = configValue,
            ["Safety:Filtering:Enabled"] = configValue,
            ["Safety:Filtering:PreserveLength"] = configValue,
            ["Safety:Audit:Enabled"] = configValue,
            ["Safety:Audit:LogFullContent"] = configValue,
            ["Safety:Audit:LogContentHashes"] = configValue,
            ["Safety:Audit:LogMetadata"] = configValue,
            ["Safety:RateLimit:Enabled"] = configValue,
            ["Safety:RateLimit:PerUserLimiting"] = configValue,
            ["Safety:RateLimit:PerIPLimiting"] = configValue
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        var options = new SafetyOptions();
        configuration.GetSection("Safety").Bind(options);

        // Assert
        options.Enabled.Should().Be(expectedValue);
        options.InputPolicy.BlockOnViolation.Should().Be(expectedValue);
        options.InputPolicy.RequireMultipleCategories.Should().Be(expectedValue);
        options.OutputPolicy.BlockOnViolation.Should().Be(expectedValue);
        options.OutputPolicy.RequireMultipleCategories.Should().Be(expectedValue);
        options.Resilience.UseExponentialBackoff.Should().Be(expectedValue);
        options.Filtering.Enabled.Should().Be(expectedValue);
        options.Filtering.PreserveLength.Should().Be(expectedValue);
        options.Audit.Enabled.Should().Be(expectedValue);
        options.Audit.LogFullContent.Should().Be(expectedValue);
        options.Audit.LogContentHashes.Should().Be(expectedValue);
        options.Audit.LogMetadata.Should().Be(expectedValue);
        options.RateLimit.Enabled.Should().Be(expectedValue);
        options.RateLimit.PerUserLimiting.Should().Be(expectedValue);
        options.RateLimit.PerIPLimiting.Should().Be(expectedValue);
    }

    /// <summary>
    /// Tests that harm category thresholds are correctly parsed and applied.
    /// </summary>
    [Fact]
    public void SafetyOptions_WithHarmCategoryThresholds_ParsesCorrectly()
    {
        // Arrange
        var configurationData = new Dictionary<string, string?>
        {
            ["Safety:Enabled"] = "true",
            ["Safety:InputPolicy:Thresholds:Hate"] = "1",
            ["Safety:InputPolicy:Thresholds:SelfHarm"] = "2",
            ["Safety:InputPolicy:Thresholds:Sexual"] = "3",
            ["Safety:InputPolicy:Thresholds:Violence"] = "4",
            ["Safety:InputPolicy:Thresholds:Suggestive"] = "5",
            ["Safety:InputPolicy:Thresholds:Profanity"] = "6",
            ["Safety:InputPolicy:Thresholds:PersonalData"] = "7",
            ["Safety:InputPolicy:Thresholds:AgeInappropriate"] = "8"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        var options = new SafetyOptions();
        configuration.GetSection("Safety").Bind(options);

        // Assert
        options.InputPolicy.Thresholds[HarmCategory.Hate].Should().Be(1);
        options.InputPolicy.Thresholds[HarmCategory.SelfHarm].Should().Be(2);
        options.InputPolicy.Thresholds[HarmCategory.Sexual].Should().Be(3);
        options.InputPolicy.Thresholds[HarmCategory.Violence].Should().Be(4);
        options.InputPolicy.Thresholds[HarmCategory.Suggestive].Should().Be(5);
        options.InputPolicy.Thresholds[HarmCategory.Profanity].Should().Be(6);
        options.InputPolicy.Thresholds[HarmCategory.PersonalData].Should().Be(7);
        options.InputPolicy.Thresholds[HarmCategory.AgeInappropriate].Should().Be(8);
    }
}
