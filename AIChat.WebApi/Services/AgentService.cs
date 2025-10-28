using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AIChat.WebApi.Services;

/// <summary>
/// Helper service for resolving AI agents by provider name
/// </summary>
public class AgentService
{
    private readonly IAgentRegistry _registry;
    private readonly ILogger<AgentService> _logger;

    public AgentService(IAgentRegistry registry, ILogger<AgentService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Get an agent by provider name
    /// </summary>
    public AIAgent GetAgent(string providerName)
    {
        if (_registry.TryGet(providerName, out var agent) && agent != null)
        {
            return agent;
        }

        _logger.LogError("Agent not found for provider: {Provider}", providerName);
        throw new ArgumentException($"Provider '{providerName}' not found or not configured", nameof(providerName));
    }

    /// <summary>
    /// Check if a provider exists
    /// </summary>
    public bool ProviderExists(string providerName)
    {
        return _registry.TryGet(providerName, out var agent) && agent != null;
    }
}