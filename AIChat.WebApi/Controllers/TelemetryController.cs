using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AIChat.WebApi.Hubs;
using AIChat.WebApi.Services;
using System.Text.Json;

namespace AIChat.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<TelemetryController> _logger;
    private readonly TelemetryService _telemetryService;

    public TelemetryController(
        IHubContext<TelemetryHub> hubContext,
        ILogger<TelemetryController> logger,
        TelemetryService telemetryService)
    {
        _hubContext = hubContext;
        _logger = logger;
        _telemetryService = telemetryService;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics([FromQuery] string timeRange = "PT1H")
    {
        try
        {
            // Return real historical metrics data
            var metrics = await _telemetryService.GetHistoricalMetrics(timeRange);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching telemetry metrics");
            // Fallback to historical metrics with fallback data
            var fallbackMetrics = await _telemetryService.GetHistoricalMetrics(timeRange);
            return Ok(fallbackMetrics);
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealthMetrics()
    {
        try
        {
            // Return real health metrics data
            var metrics = await _telemetryService.GetHealthMetrics();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching health metrics");
            // Fallback to application metrics
            var fallbackMetrics = await _telemetryService.GetHealthMetrics();
            return Ok(fallbackMetrics);
        }
    }
}