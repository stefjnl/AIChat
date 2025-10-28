using AIChat.WebApi.Services;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Microsoft.Extensions.DependencyInjection;
using AIChat.WebApi.Hubs;

namespace AIChat.WebApi.DependencyInjection;

internal static class TelemetryExtensions
{
 public static WebApplicationBuilder AddTelemetryServices(this WebApplicationBuilder builder)
 {
 builder.Services.AddSingleton<TelemetryService>();
 builder.Services.AddHostedService<TelemetryBroadcastService>();

 builder.Services.AddOpenTelemetry()
 .WithTracing(tracing =>
 {
 tracing.AddSource("Microsoft.Extensions.AI")
 .AddSource("Microsoft.Agents.AI")
 .AddSource("AIChat.Safety")
 .AddSource("agent-telemetry-source");

 tracing.AddAspNetCoreInstrumentation();
 tracing.AddHttpClientInstrumentation();

 if (builder.Environment.IsDevelopment())
 {
 tracing.AddConsoleExporter();
 }
 else
 {
 var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
 if (!string.IsNullOrEmpty(connectionString))
 {
 tracing.AddAzureMonitorTraceExporter(options => { options.ConnectionString = connectionString; });
 }
 else
 {
 tracing.AddOtlpExporter();
 }
 }
 })
 .WithMetrics(metrics =>
 {
 metrics.AddAspNetCoreInstrumentation()
 .AddHttpClientInstrumentation()
 .AddMeter("Microsoft.Extensions.AI")
 .AddMeter("Microsoft.Agents.AI")
 .AddMeter("AIChat.Safety");

 if (builder.Environment.IsDevelopment())
 {
 metrics.AddConsoleExporter();
 }
 else
 {
 var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
 if (!string.IsNullOrEmpty(connectionString))
 {
 metrics.AddAzureMonitorMetricExporter(options => { options.ConnectionString = connectionString; });
 }
 }
 });

 builder.Logging.AddOpenTelemetry(logging =>
 {
 logging.IncludeFormattedMessage = true;
 logging.IncludeScopes = true;

 if (!builder.Environment.IsDevelopment())
 {
 var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
 if (!string.IsNullOrEmpty(connectionString))
 {
 logging.AddAzureMonitorLogExporter(options => { options.ConnectionString = connectionString; });
 }
 }
 });

 return builder;
 }
}
