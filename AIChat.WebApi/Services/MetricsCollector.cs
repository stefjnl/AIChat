using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AIChat.WebApi.Services;

public class MetricsCollector
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly Process _currentProcess;
    private DateTime _lastCollectionTime;
    private long _lastRequestCount;
    private long _lastErrorCount;
    private double _lastTotalDuration;

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
        _lastCollectionTime = DateTime.UtcNow;
        _lastRequestCount = 0;
        _lastErrorCount = 0;
        _lastTotalDuration = 0;
    }

    public async Task<ApplicationMetrics> CollectMetrics()
    {
        try
        {
            var now = DateTime.UtcNow;
            var timeSpan = now - _lastCollectionTime;

            // Get process metrics
            _currentProcess.Refresh();
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();

            // For demo purposes, simulate some realistic metrics
            // In a real application, these would come from actual instrumentation
            var requestCount = _lastRequestCount + new Random().Next(10, 50);
            var errorCount = _lastErrorCount + new Random().Next(0, 3);
            var totalDuration = _lastTotalDuration + new Random().NextDouble() * 100;

            // Calculate rates per second
            var requestRate = timeSpan.TotalSeconds > 0 ? (requestCount - _lastRequestCount) / timeSpan.TotalSeconds : 0;
            var errorRate = requestRate > 0 ? ((errorCount - _lastErrorCount) / (requestCount - _lastRequestCount)) * 100 : 0;

            // Calculate latency (simulate P95)
            var avgDuration = requestCount > _lastRequestCount ?
                (totalDuration - _lastTotalDuration) / (requestCount - _lastRequestCount) : 0;
            var latencyP95 = avgDuration * 1.5; // Rough P95 estimate

            // Calculate trends (simplified)
            var requestRateChange = Math.Abs(requestRate - 25); // Assuming baseline of 25 req/s
            var errorRateChange = Math.Abs(errorRate - 0.8);
            var latencyChange = Math.Abs(latencyP95 - 200); // Assuming baseline of 200ms

            // Update last values
            _lastCollectionTime = now;
            _lastRequestCount = requestCount;
            _lastErrorCount = errorCount;
            _lastTotalDuration = totalDuration;

            return new ApplicationMetrics
            {
                RequestRate = Math.Round((double)requestRate, 1),
                RequestRateTrend = requestRate > 25 ? "up" : requestRate < 25 ? "down" : "stable",
                RequestRateChange = Math.Round((double)requestRateChange, 1),

                ErrorRate = Math.Round((double)errorRate, 2),
                ErrorRateTrend = errorRate > 0.8 ? "up" : errorRate < 0.8 ? "down" : "stable",
                ErrorRateChange = Math.Round((double)errorRateChange, 2),

                LatencyP95 = Math.Round((double)latencyP95, 0),
                LatencyTrend = latencyP95 > 200 ? "up" : latencyP95 < 200 ? "down" : "stable",
                LatencyChange = Math.Round((double)latencyChange, 0),

                CpuUsage = Math.Round((double)cpuUsage, 1),
                MemoryUsage = Math.Round(memoryUsage / 1024 / 1024, 1), // Convert to GB
                MemoryUsed = Math.Round(memoryUsage / 1024 / 1024, 1),
                MemoryTotal = 4.0, // Assume 4GB total
                MemoryPercentage = Math.Round((double)(memoryUsage / (4.0 * 1024 * 1024 * 1024)) * 100, 0, MidpointRounding.AwayFromZero)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting application metrics");

            // Return fallback metrics
            return new ApplicationMetrics
            {
                RequestRate = 25.0,
                RequestRateTrend = "stable",
                RequestRateChange = 0,
                ErrorRate = 0.8,
                ErrorRateTrend = "stable",
                ErrorRateChange = 0,
                LatencyP95 = 200,
                LatencyTrend = "stable",
                LatencyChange = 0,
                CpuUsage = 45.0,
                MemoryUsage = 2.8,
                MemoryUsed = 2.8,
                MemoryTotal = 4.0,
                MemoryPercentage = 70
            };
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            var cpuTime = _currentProcess.TotalProcessorTime.TotalMilliseconds;
            var elapsed = (DateTime.UtcNow - _currentProcess.StartTime).TotalMilliseconds;

            if (elapsed > 0)
            {
                return Math.Min((cpuTime / elapsed) * 100, 100);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting CPU usage");
        }

        return 45.0; // Fallback
    }

    private double GetMemoryUsage()
    {
        try
        {
            return _currentProcess.WorkingSet64;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting memory usage");
        }

        return 2.8 * 1024 * 1024 * 1024; // Fallback to 2.8GB in bytes
    }
}

public class ApplicationMetrics
{
    public double RequestRate { get; set; }
    public string RequestRateTrend { get; set; } = "stable";
    public double RequestRateChange { get; set; }

    public double ErrorRate { get; set; }
    public string ErrorRateTrend { get; set; } = "stable";
    public double ErrorRateChange { get; set; }

    public double LatencyP95 { get; set; }
    public string LatencyTrend { get; set; } = "stable";
    public double LatencyChange { get; set; }

    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double MemoryUsed { get; set; }
    public double MemoryTotal { get; set; }
    public double MemoryPercentage { get; set; }
}