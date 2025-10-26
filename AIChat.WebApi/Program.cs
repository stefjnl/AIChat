using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using AIChat.Agents.Providers;
using AIChat.Infrastructure.Configuration;
using AIChat.Infrastructure.Storage;
using AIChat.WebApi.Services;
using AIChat.WebApi.Hubs;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Azure.Monitor.OpenTelemetry.Exporter;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURATION
// ============================================================

// Add user secrets for development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Manual configuration loading (same approach as unit tests)
var providersSection = builder.Configuration.GetSection("Providers");
Console.WriteLine($"Providers section exists: {providersSection.Exists()}");

// Create configuration object using Get<>() which should work better
var providersConfig = builder.Configuration.GetSection("Providers").Get<ProvidersConfiguration>();

// If Get doesn't work, try manual construction (same as unit tests)
if (providersConfig?.Providers == null || providersConfig.Providers.Count == 0)
{
    providersConfig = new ProvidersConfiguration();
    
    // Manually bind each provider
    var openRouterSection = providersSection.GetSection("OpenRouter");
    if (openRouterSection.Exists())
    {
        providersConfig.Providers["OpenRouter"] = new ProviderSettings
        {
            BaseUrl = openRouterSection["BaseUrl"] ?? "",
            DefaultModel = openRouterSection["DefaultModel"] ?? "",
            ApiKey = builder.Configuration["Providers:OpenRouter:ApiKey"]
        };
    }
    
    var nanoGptSection = providersSection.GetSection("NanoGPT");
    if (nanoGptSection.Exists())
    {
        providersConfig.Providers["NanoGPT"] = new ProviderSettings
        {
            BaseUrl = nanoGptSection["BaseUrl"] ?? "",
            DefaultModel = nanoGptSection["DefaultModel"] ?? "",
            ApiKey = builder.Configuration["Providers:NanoGPT:ApiKey"]
        };
    }
    
    var lmStudioSection = providersSection.GetSection("LMStudio");
    if (lmStudioSection.Exists())
    {
        providersConfig.Providers["LMStudio"] = new ProviderSettings
        {
            BaseUrl = lmStudioSection["BaseUrl"] ?? "",
            DefaultModel = lmStudioSection["DefaultModel"] ?? "",
            ApiKey = builder.Configuration["Providers:LMStudio:ApiKey"]
        };
    }
}

Console.WriteLine($"Number of providers configured: {providersConfig?.Providers.Count ?? 0}");

// Register the configuration as a singleton
builder.Services.AddSingleton(providersConfig ?? new ProvidersConfiguration());

// Bind storage configuration
var storageConfig = new StorageOptions();
builder.Configuration.GetSection("Storage").Bind(storageConfig);
builder.Services.AddSingleton(storageConfig);

// ============================================================
// INFRASTRUCTURE SERVICES
// ============================================================

// Thread storage for conversation persistence
builder.Services.AddSingleton<IThreadStorage, FileThreadStorage>();

// Chat history storage for enhanced history management
builder.Services.AddSingleton<IChatHistoryStorage, FileChatHistoryStorage>();

// Provider client factory
builder.Services.AddSingleton<ProviderClientFactory>();

// Agent helper service
builder.Services.AddSingleton<AgentService>();

// ============================================================
// AGENT REGISTRATION
// ============================================================

if (providersConfig?.Providers != null && providersConfig.Providers.Count > 0)
{
    foreach (var providerName in providersConfig.Providers.Keys)
    {
        builder.AddAIAgent(providerName, (sp, key) =>
        {
            // Get the factory
            var factory = sp.GetRequiredService<ProviderClientFactory>();
            
            // Create the chat client for this provider
            var chatClient = factory.CreateChatClient(key);
            
            // Create the agent with ChatClientAgent
            return new ChatClientAgent(
                chatClient,
                new ChatClientAgentOptions
                {
                    Name = key,
                    Instructions = "You are a helpful AI assistant that provides accurate and concise responses."
                });
        });
    }
}

// ============================================================
// WEB SERVICES
// ============================================================

// Configure JSON serialization to use camelCase
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Register title generation service
builder.Services.AddSingleton<ITitleGenerator, TitleGenerationService>();
builder.Services.AddSingleton<TextAnalysisService>();

// Register telemetry service
builder.Services.AddSingleton<TelemetryService>();

// Register telemetry broadcast service
builder.Services.AddHostedService<TelemetryBroadcastService>();

// Controllers
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

// CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ============================================================
// OPENTELEMETRY CONFIGURATION
// ============================================================

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Microsoft.Extensions.AI")
               .AddSource("Microsoft.Agents.AI")
               .AddSource("agent-telemetry-source"); // For custom agent telemetry
        
        // Add ASP.NET Core instrumentation
        tracing.AddAspNetCoreInstrumentation();
        
        // Add HttpClient instrumentation for external API calls
        tracing.AddHttpClientInstrumentation();
        
        // Configure exporters based on environment
        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
        else
        {
            // For production, use Azure Monitor or OTLP
            var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                tracing.AddAzureMonitorTraceExporter(options =>
                {
                    options.ConnectionString = connectionString;
                });
            }
            else
            {
                // Fallback to OTLP
                tracing.AddOtlpExporter();
            }
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddMeter("Microsoft.Extensions.AI")
               .AddMeter("Microsoft.Agents.AI");
        
        // Configure metric exporters
        if (builder.Environment.IsDevelopment())
        {
            metrics.AddConsoleExporter();
        }
        else
        {
            var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                metrics.AddAzureMonitorMetricExporter(options =>
                {
                    options.ConnectionString = connectionString;
                });
            }
        }
    });

// Configure logging to integrate with OpenTelemetry
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    
    if (!builder.Environment.IsDevelopment())
    {
        var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            logging.AddAzureMonitorLogExporter(options =>
            {
                options.ConnectionString = connectionString;
            });
        }
    }
});

// ============================================================
// BUILD & CONFIGURE PIPELINE
// ============================================================

var app = builder.Build();

// Development-only middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors();
app.UseRouting();

// Map endpoints
app.MapControllers();

// SignalR Hub
app.MapHub<ChatHub>("/chathub");
app.MapHub<TelemetryHub>("/telemetryhub");

// Map root endpoint to serve the chat interface
app.MapGet("/", (IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.ContentRootPath, "wwwroot", "index.html");
    return Results.File(filePath, "text/html");
});

app.Run();