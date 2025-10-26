using Microsoft.AspNetCore.Mvc;
using AIChat.Infrastructure.Configuration;

namespace AIChat.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvidersController : ControllerBase
{
    private readonly ProvidersConfiguration _config;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(
        ProvidersConfiguration config,
        ILogger<ProvidersController> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get list of available providers
    /// </summary>
    [HttpGet]
    public IActionResult GetProviders()
    {
        try
        {
            _logger.LogInformation("Providers count: {Count}", _config.Providers?.Count ?? 0);
            
            if (_config.Providers == null)
            {
                _logger.LogWarning("Providers configuration is null");
                return Ok(new object[0]);
            }

            // Log each provider for debugging
            foreach (var provider in _config.Providers)
            {
                _logger.LogInformation("Provider: {Name}, Model: {Model}, BaseUrl: {BaseUrl}", 
                    provider.Key, provider.Value.DefaultModel, provider.Value.BaseUrl);
            }

            var providers = _config.Providers.Select(p => new
            {
                Name = p.Key,
                Model = p.Value.DefaultModel,
                BaseUrl = p.Value.BaseUrl
            });

            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving providers");
            return StatusCode(500, "Failed to retrieve providers");
        }
    }

    /// <summary>
    /// Check if a specific provider is available
    /// </summary>
    [HttpGet("{providerName}/status")]
    public IActionResult GetProviderStatus(string providerName)
    {
        if (!_config.Providers.ContainsKey(providerName))
        {
            return NotFound(new { message = $"Provider '{providerName}' not found" });
        }

        var settings = _config.Providers[providerName];
        return Ok(new
        {
            Name = providerName,
            Model = settings.DefaultModel,
            BaseUrl = settings.BaseUrl,
            HasApiKey = !string.IsNullOrEmpty(settings.ApiKey)
        });
    }
}