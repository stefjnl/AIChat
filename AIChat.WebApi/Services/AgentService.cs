using Microsoft.Agents.AI;

namespace AIChat.WebApi.Services;

/// <summary>
/// Helper service for resolving AI agents by provider name
/// </summary>
public class AgentService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IServiceProvider serviceProvider,
        ILogger<AgentService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get an agent by provider name
    /// </summary>
    public AIAgent GetAgent(string providerName)
    {
        try
        {
            return _serviceProvider.GetRequiredKeyedService<AIAgent>(providerName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to resolve agent for provider: {Provider}", providerName);
            throw new ArgumentException($"Provider '{providerName}' not found or not configured", nameof(providerName));
        }
    }

    /// <summary>
    /// Check if a provider exists
    /// </summary>
    public bool ProviderExists(string providerName)
    {
        try
        {
            _serviceProvider.GetRequiredKeyedService<AIAgent>(providerName);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}