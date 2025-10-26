using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AIChat.WebApi.Controllers;
using AIChat.WebApi.Services;

namespace AIChat.WebApi.Hubs;

public class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard client disconnected: {ConnectionId}", exception?.Message ?? "Normal disconnect");
        await base.OnDisconnectedAsync(exception);
    }
}

public class TelemetryBroadcastService : BackgroundService
{
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<TelemetryBroadcastService> _logger;
    private readonly TelemetryService _telemetryService;

    public TelemetryBroadcastService(
        IHubContext<TelemetryHub> hubContext,
        ILogger<TelemetryBroadcastService> logger,
        TelemetryService telemetryService)
    {
        _hubContext = hubContext;
        _logger = logger;
        _telemetryService = telemetryService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry broadcast service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Fetch current health metrics
                var healthMetrics = await _telemetryService.GetHealthMetrics();

                // Broadcast to all connected dashboard clients
                await _hubContext.Clients.All.SendAsync("ReceiveMetrics", healthMetrics, stoppingToken);

                _logger.LogDebug("Broadcasted telemetry metrics to all connected clients");

                // Wait 5 seconds before next broadcast
                await Task.Delay(5000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting telemetry metrics");
                await Task.Delay(10000, stoppingToken); // Wait longer on error
            }
        }

        _logger.LogInformation("Telemetry broadcast service stopping");
    }
}