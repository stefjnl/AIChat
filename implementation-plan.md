# Detailed Implementation Plan

## Phase 1: Project Setup & Infrastructure (Day 1)

### 1.1 Create Solution Structure
```bash
dotnet new sln -n AIChat
dotnet new aspire-apphost -n AIChat.AppHost
dotnet new web -n AIChat.WebApi
dotnet new classlib -n AIChat.Agents
dotnet new classlib -n AIChat.Infrastructure
dotnet new classlib -n AIChat.Shared

dotnet sln add **/*.csproj
```

### 1.2 Install NuGet Packages

**AIChat.WebApi**
```bash
dotnet add package Microsoft.Agents.AI --prerelease
dotnet add package Microsoft.Agents.AI.Hosting --prerelease
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Aspire.Hosting.AppHost
```

**AIChat.Agents**
```bash
dotnet add package Microsoft.Extensions.AI
dotnet add package Microsoft.Agents.AI --prerelease
```

**AIChat.Infrastructure**
```bash
dotnet add package System.Text.Json
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Options
```

### 1.3 Project References
```bash
cd AIChat.WebApi
dotnet add reference ../AIChat.Agents/AIChat.Agents.csproj
dotnet add reference ../AIChat.Infrastructure/AIChat.Infrastructure.csproj
dotnet add reference ../AIChat.Shared/AIChat.Shared.csproj

cd ../AIChat.Agents
dotnet add reference ../AIChat.Shared/AIChat.Shared.csproj
dotnet add reference ../AIChat.Infrastructure/AIChat.Infrastructure.csproj
```

### 1.4 Setup User Secrets
```bash
cd AIChat.WebApi
dotnet user-secrets init
dotnet user-secrets set "Providers:OpenRouter:ApiKey" "your-key-here"
dotnet user-secrets set "Providers:NanoGPT:ApiKey" "your-key-here"
dotnet user-secrets set "Providers:LMStudio:ApiKey" ""
```

---

## Phase 2: Shared Models & Configuration (Day 1-2)

### 2.1 Create Shared DTOs

**AIChat.Shared/Models/ChatChunk.cs**
```csharp
namespace AIChat.Shared.Models;

public class ChatChunk
{
    public string? Text { get; set; }
    public TokenUsage? Usage { get; set; }
    public bool IsFinal { get; set; }
}

public class TokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
}
```

**AIChat.Shared/Models/ChatRequest.cs**
```csharp
namespace AIChat.Shared.Models;

public class ChatRequest
{
    public required string Provider { get; set; }
    public required string Message { get; set; }
    public string? ThreadId { get; set; }
}
```

### 2.2 Configuration Models

**AIChat.Infrastructure/Configuration/ProviderSettings.cs**
```csharp
namespace AIChat.Infrastructure.Configuration;

public class ProvidersConfiguration
{
    public Dictionary<string, ProviderSettings> Providers { get; set; } = new();
}

public class ProviderSettings
{
    public required string BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public required string DefaultModel { get; set; }
}
```

### 2.3 appsettings.json

**AIChat.WebApi/appsettings.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Providers": {
    "OpenRouter": {
      "BaseUrl": "https://openrouter.ai/api/v1",
      "DefaultModel": "anthropic/claude-3.5-sonnet"
    },
    "NanoGPT": {
      "BaseUrl": "https://nano-gpt.com/api/v1",
      "DefaultModel": "gpt-4"
    },
    "LMStudio": {
      "BaseUrl": "http://localhost:1234/v1",
      "DefaultModel": "local-model"
    }
  },
  "Storage": {
    "ThreadsPath": "./data/threads"
  }
}
```

---

## Phase 3: Conversation Persistence (Day 2-3)

### 3.1 Thread Storage Interface

**AIChat.Infrastructure/Storage/IThreadStorage.cs**
```csharp
using System.Text.Json;

namespace AIChat.Infrastructure.Storage;

public interface IThreadStorage
{
    Task<JsonElement?> LoadThreadAsync(string threadId, CancellationToken ct = default);
    Task SaveThreadAsync(string threadId, JsonElement threadData, CancellationToken ct = default);
    Task<bool> ThreadExistsAsync(string threadId, CancellationToken ct = default);
    string CreateNewThreadId();
}
```

### 3.2 File-Based Implementation

**AIChat.Infrastructure/Storage/FileThreadStorage.cs**
```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AIChat.Infrastructure.Storage;

public class FileThreadStorage : IThreadStorage
{
    private readonly string _basePath;

    public FileThreadStorage(IOptions<StorageOptions> options)
    {
        _basePath = options.Value.ThreadsPath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<JsonElement?> LoadThreadAsync(string threadId, CancellationToken ct = default)
    {
        var filePath = GetThreadPath(threadId);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonDocument.Parse(json).RootElement;
    }

    public async Task SaveThreadAsync(string threadId, JsonElement threadData, CancellationToken ct = default)
    {
        var filePath = GetThreadPath(threadId);
        await File.WriteAllTextAsync(filePath, threadData.ToString(), ct);
    }

    public Task<bool> ThreadExistsAsync(string threadId, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(GetThreadPath(threadId)));
    }

    public string CreateNewThreadId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private string GetThreadPath(string threadId)
    {
        return Path.Combine(_basePath, $"{threadId}.json");
    }
}

public class StorageOptions
{
    public string ThreadsPath { get; set; } = "./data/threads";
}
```

### 3.3 Custom ChatMessageStore (Optional Alternative)

**AIChat.Infrastructure/Storage/FileChatMessageStore.cs**
```csharp
using Microsoft.Agents.AI;
using System.Text.Json;

namespace AIChat.Infrastructure.Storage;

public class FileChatMessageStore : ChatMessageStore
{
    private readonly string _threadId;
    private readonly string _filePath;

    public FileChatMessageStore(string threadId, string basePath)
    {
        _threadId = threadId;
        _filePath = Path.Combine(basePath, $"{threadId}_messages.json");
        Directory.CreateDirectory(basePath);
    }

    public override async Task AddMessagesAsync(
        IEnumerable<ChatMessage> messages, 
        CancellationToken cancellationToken = default)
    {
        var existing = await GetMessagesAsync(cancellationToken);
        var all = existing.Concat(messages).ToList();
        
        var json = JsonSerializer.Serialize(all);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return Enumerable.Empty<ChatMessage>();

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        return JsonSerializer.Deserialize<List<ChatMessage>>(json) 
               ?? Enumerable.Empty<ChatMessage>();
    }
}
```

---

## Phase 4: IChatClient Implementations (Day 3-5)

### 4.1 Base HTTP Client

**AIChat.Agents/Common/BaseHttpChatClient.cs**
```csharp
using Microsoft.Extensions.AI;
using System.Net.Http.Json;

namespace AIChat.Agents.Common;

public abstract class BaseHttpChatClient : IChatClient
{
    protected readonly HttpClient _httpClient;
    protected readonly string _apiKey;
    protected readonly string _model;

    protected BaseHttpChatClient(HttpClient httpClient, string apiKey, string model)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public abstract Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<StreamingChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    public ChatClientMetadata Metadata => new("CustomClient", null, _model);

    public TService? GetService<TService>(object? key = null) where TService : class => null;

    public void Dispose() { }
}
```

### 4.2 OpenRouter Implementation

**AIChat.Agents/Providers/OpenRouterChatClient.cs**
```csharp
using Microsoft.Extensions.AI;
using AIChat.Agents.Common;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AIChat.Agents.Providers;

public class OpenRouterChatClient : BaseHttpChatClient
{
    public OpenRouterChatClient(HttpClient httpClient, string apiKey, string model)
        : base(httpClient, apiKey, model)
    {
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://your-app.com");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "AI Chat App");
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _model,
            messages = messages.Select(m => new
            {
                role = m.Role.ToString().ToLowerInvariant(),
                content = m.Text
            }),
            temperature = options?.Temperature ?? 0.7,
            max_tokens = options?.MaxOutputTokens
        };

        var response = await _httpClient.PostAsJsonAsync(
            "chat/completions", 
            request, 
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(cancellationToken);

        return new ChatResponse(new[]
        {
            new ChatMessage(ChatRole.Assistant, result!.Choices[0].Message.Content)
        })
        {
            Usage = new UsageDetails
            {
                InputTokenCount = result.Usage.PromptTokens,
                OutputTokenCount = result.Usage.CompletionTokens,
                TotalTokenCount = result.Usage.TotalTokens
            }
        };
    }

    public override async IAsyncEnumerable<StreamingChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _model,
            messages = messages.Select(m => new
            {
                role = m.Role.ToString().ToLowerInvariant(),
                content = m.Text
            }),
            temperature = options?.Temperature ?? 0.7,
            max_tokens = options?.MaxOutputTokens,
            stream = true
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest, 
            HttpCompletionOption.ResponseHeadersRead, 
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        UsageDetails? finalUsage = null;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) 
                continue;

            var data = line.Substring(6);
            if (data == "[DONE]") 
                break;

            var chunk = JsonSerializer.Deserialize<OpenRouterStreamChunk>(data);
            if (chunk?.Choices?.Length > 0)
            {
                var delta = chunk.Choices[0].Delta;
                if (!string.IsNullOrEmpty(delta?.Content))
                {
                    yield return new StreamingChatResponseUpdate
                    {
                        Text = delta.Content,
                        Role = ChatRole.Assistant
                    };
                }
            }

            // Capture usage from final chunk if available
            if (chunk?.Usage != null)
            {
                finalUsage = new UsageDetails
                {
                    InputTokenCount = chunk.Usage.PromptTokens,
                    OutputTokenCount = chunk.Usage.CompletionTokens,
                    TotalTokenCount = chunk.Usage.TotalTokens
                };
            }
        }

        // Send final chunk with usage
        if (finalUsage != null)
        {
            yield return new StreamingChatResponseUpdate
            {
                Contents = new List<AIContent> { new UsageContent(finalUsage) }
            };
        }
    }

    // DTOs for deserialization
    private class OpenRouterResponse
    {
        public Choice[] Choices { get; set; } = Array.Empty<Choice>();
        public Usage Usage { get; set; } = new();
    }

    private class OpenRouterStreamChunk
    {
        public StreamChoice[]? Choices { get; set; }
        public Usage? Usage { get; set; }
    }

    private class Choice
    {
        public Message Message { get; set; } = new();
    }

    private class StreamChoice
    {
        public Delta? Delta { get; set; }
    }

    private class Message
    {
        public string Content { get; set; } = "";
    }

    private class Delta
    {
        public string? Content { get; set; }
    }

    private class Usage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
```

### 4.3 NanoGPT Implementation

**AIChat.Agents/Providers/NanoGPTChatClient.cs**
```csharp
// Similar structure to OpenRouter
// Adjust API format based on NanoGPT docs
// Key differences:
// - Different auth header format
// - Different request/response schema
// - Handle their specific streaming format
```

### 4.4 LM Studio Implementation

**AIChat.Agents/Providers/LMStudioChatClient.cs**
```csharp
// LM Studio uses OpenAI-compatible API
// Can reuse most of OpenRouter logic
// Main difference: no API key needed for local
// BaseUrl points to localhost:1234
```

---

## Phase 5: Agent Registration & DI (Day 5-6)

### 5.1 Provider Factory

**AIChat.Agents/Factory/ChatClientFactory.cs**
```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using AIChat.Infrastructure.Configuration;
using AIChat.Agents.Providers;

namespace AIChat.Agents.Factory;

public class ChatClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProvidersConfiguration _config;

    public ChatClientFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<ProvidersConfiguration> config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
    }

    public IChatClient CreateClient(string providerName)
    {
        if (!_config.Providers.TryGetValue(providerName, out var settings))
            throw new ArgumentException($"Provider '{providerName}' not configured");

        var httpClient = _httpClientFactory.CreateClient(providerName);
        httpClient.BaseAddress = new Uri(settings.BaseUrl);

        return providerName switch
        {
            "OpenRouter" => new OpenRouterChatClient(
                httpClient, 
                settings.ApiKey ?? "", 
                settings.DefaultModel),
            
            "NanoGPT" => new NanoGPTChatClient(
                httpClient, 
                settings.ApiKey ?? "", 
                settings.DefaultModel),
            
            "LMStudio" => new LMStudioChatClient(
                httpClient, 
                "", 
                settings.DefaultModel),
            
            _ => throw new ArgumentException($"Unknown provider: {providerName}")
        };
    }
}
```

### 5.2 Program.cs Configuration

**AIChat.WebApi/Program.cs**
```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using AIChat.Agents.Factory;
using AIChat.Agents.Providers;
using AIChat.Infrastructure.Configuration;
using AIChat.Infrastructure.Storage;
using AIChat.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddUserSecrets<Program>();
builder.Services.Configure<ProvidersConfiguration>(
    builder.Configuration.GetSection("Providers"));
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));

// HTTP Clients
builder.Services.AddHttpClient("OpenRouter");
builder.Services.AddHttpClient("NanoGPT");
builder.Services.AddHttpClient("LMStudio");

// Infrastructure
builder.Services.AddSingleton<IThreadStorage, FileThreadStorage>();
builder.Services.AddSingleton<ChatClientFactory>();

// Register agents for each provider
var providers = new[] { "OpenRouter", "NanoGPT", "LMStudio" };
foreach (var provider in providers)
{
    builder.AddAIAgent(provider, (sp, key) =>
    {
        var factory = sp.GetRequiredService<ChatClientFactory>();
        var chatClient = factory.CreateClient(key);
        
        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = key,
                Instructions = "You are a helpful AI assistant."
            });
    });
}

// SignalR
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseRouting();
app.MapControllers();
app.MapHub<ChatHub>("/chathub");
app.UseStaticFiles();

app.Run();
```

---

## Phase 6: SignalR Hub (Day 6-7)

### 6.1 Chat Hub Implementation

**AIChat.WebApi/Hubs/ChatHub.cs**
```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIChat.Infrastructure.Storage;
using AIChat.Shared.Models;
using System.Runtime.CompilerServices;

namespace AIChat.WebApi.Hubs;

public class ChatHub : Hub
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThreadStorage _threadStorage;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IServiceProvider serviceProvider,
        IThreadStorage threadStorage,
        ILogger<ChatHub> logger)
    {
        _serviceProvider = serviceProvider;
        _threadStorage = threadStorage;
        _logger = logger;
    }

    public async IAsyncEnumerable<ChatChunk> StreamChat(
        string provider,
        string message,
        string? threadId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting chat stream for provider: {Provider}", provider);

        try
        {
            // Get agent
            var agent = _serviceProvider.GetRequiredKeyedService<AIAgent>(provider);

            // Load or create thread
            threadId ??= _threadStorage.CreateNewThreadId();
            var thread = await LoadOrCreateThreadAsync(agent, threadId, cancellationToken);

            UsageDetails? usage = null;

            // Stream response
            await foreach (var update in agent.RunStreamingAsync(
                message, 
                thread, 
                cancellationToken: cancellationToken))
            {
                // Extract usage from update
                var usageContent = update.Contents?.OfType<UsageContent>().FirstOrDefault();
                if (usageContent != null)
                {
                    usage = usageContent.Details;
                }

                // Send text chunk
                if (!string.IsNullOrEmpty(update.Text))
                {
                    yield return new ChatChunk
                    {
                        Text = update.Text,
                        IsFinal = false
                    };
                }
            }

            // Save thread
            await SaveThreadAsync(agent, thread, threadId, cancellationToken);

            // Send final chunk with usage
            yield return new ChatChunk
            {
                IsFinal = true,
                Usage = usage != null ? new TokenUsage
                {
                    InputTokens = usage.InputTokenCount ?? 0,
                    OutputTokens = usage.OutputTokenCount ?? 0,
                    TotalTokens = usage.TotalTokenCount ?? 0
                } : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat stream");
            throw;
        }
    }

    private async Task<AgentThread> LoadOrCreateThreadAsync(
        AIAgent agent, 
        string threadId, 
        CancellationToken ct)
    {
        var threadData = await _threadStorage.LoadThreadAsync(threadId, ct);
        
        if (threadData.HasValue)
        {
            return await agent.DeserializeThreadAsync(threadData.Value, ct);
        }

        return agent.GetNewThread();
    }

    private async Task SaveThreadAsync(
        AIAgent agent, 
        AgentThread thread, 
        string threadId, 
        CancellationToken ct)
    {
        var serialized = await thread.SerializeAsync(ct);
        await _threadStorage.SaveThreadAsync(threadId, serialized, ct);
    }
}
```

---

## Phase 7: API Controllers (Day 7)

### 7.1 Provider Info Controller

**AIChat.WebApi/Controllers/ProvidersController.cs**
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AIChat.Infrastructure.Configuration;

namespace AIChat.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvidersController : ControllerBase
{
    private readonly ProvidersConfiguration _config;

    public ProvidersController(IOptions<ProvidersConfiguration> config)
    {
        _config = config.Value;
    }

    [HttpGet]
    public IActionResult GetProviders()
    {
        var providers = _config.Providers.Select(p => new
        {
            Name = p.Key,
            Model = p.Value.DefaultModel
        });

        return Ok(providers);
    }
}
```

### 7.2 Thread Management Controller

**AIChat.WebApi/Controllers/ThreadsController.cs**
```csharp
using Microsoft.AspNetCore.Mvc;
using AIChat.Infrastructure.Storage;

namespace AIChat.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ThreadsController : ControllerBase
{
    private readonly IThreadStorage _threadStorage;

    public ThreadsController(IThreadStorage threadStorage)
    {
        _threadStorage = threadStorage;
    }

    [HttpPost("new")]
    public IActionResult CreateNewThread()
    {
        var threadId = _threadStorage.CreateNewThreadId();
        return Ok(new { threadId });
    }

    [HttpGet("{threadId}/exists")]
    public async Task<IActionResult> CheckThreadExists(string threadId)
    {
        var exists = await _threadStorage.ThreadExistsAsync(threadId);
        return Ok(new { exists });
    }
}
```

---

## Phase 8: Frontend (Day 8-9)

### 8.1 HTML Structure

**AIChat.WebApi/wwwroot/index.html**
```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>AI Chat</title>
    <script src="https://cdn.tailwindcss.com?plugins=forms,typography"></script>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8/dist/browser/signalr.min.js"></script>
</head>
<body class="bg-gray-50 h-screen flex flex-col">
    <!-- Header -->
    <header class="bg-white border-b border-gray-200 px-6 py-4">
        <div class="max-w-7xl mx-auto flex items-center justify-between">
            <h1 class="text-2xl font-semibold text-gray-900">AI Chat</h1>
            
            <!-- Provider Selector -->
            <div class="flex items-center gap-4">
                <label class="text-sm font-medium text-gray-700">Provider:</label>
                <select id="providerSelect" 
                        class="rounded-lg border-gray-300 text-sm focus:ring-blue-500 focus:border-blue-500">
                    <!-- Populated dynamically -->
                </select>
                
                <!-- Token Counter -->
                <div id="tokenCounter" class="text-sm text-gray-600 hidden">
                    <span class="font-medium">Tokens:</span>
                    <span id="inputTokens" class="text-blue-600">0</span> in /
                    <span id="outputTokens" class="text-green-600">0</span> out /
                    <span id="totalTokens" class="font-semibold">0</span> total
                </div>
            </div>
        </div>
    </header>

    <!-- Chat Container -->
    <main class="flex-1 overflow-hidden">
        <div class="max-w-4xl mx-auto h-full flex flex-col py-6 px-4">
            <!-- Messages Area -->
            <div id="messagesContainer" 
                 class="flex-1 overflow-y-auto space-y-4 mb-4 scroll-smooth">
                <!-- Messages rendered here -->
            </div>

            <!-- Input Area -->
            <div class="bg-white rounded-lg border border-gray-300 shadow-sm">
                <textarea id="messageInput"
                          placeholder="Type your message..."
                          rows="3"
                          class="w-full px-4 py-3 resize-none border-0 focus:ring-0 focus:outline-none"></textarea>
                <div class="flex items-center justify-between px-4 py-2 border-t border-gray-200">
                    <button id="newChatBtn"
                            class="text-sm text-gray-600 hover:text-gray-900">
                        New Chat
                    </button>
                    <button id="sendBtn"
                            class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed">
                        Send
                    </button>
                </div>
            </div>
        </div>
    </main>

    <script src="app.js"></script>
</body>
</html>
```

### 8.2 JavaScript Application

**AIChat.WebApi/wwwroot/app.js**
```javascript
class ChatApp {
    constructor() {
        this.connection = null;
        this.currentThreadId = null;
        this.currentProvider = null;
        this.isStreaming = false;
        
        this.elements = {
            providerSelect: document.getElementById('providerSelect'),
            messagesContainer: document.getElementById('messagesContainer'),
            messageInput: document.getElementById('messageInput'),
            sendBtn: document.getElementById('sendBtn'),
            newChatBtn: document.getElementById('newChatBtn'),
            tokenCounter: document.getElementById('tokenCounter'),
            inputTokens: document.getElementById('inputTokens'),
            outputTokens: document.getElementById('outputTokens'),
            totalTokens: document.getElementById('totalTokens')
        };
        
        this.init();
    }
    
    async init() {
        await this.loadProviders();
        this.setupSignalR();
        this.setupEventListeners();
        this.createNewThread();
    }
    
    async loadProviders() {
        try {
            const response = await fetch('/api/providers');
            const providers = await response.json();
            
            providers.forEach(provider => {
                const option = document.createElement('option');
                option.value = provider.name;
                option.textContent = `${provider.name} (${provider.model})`;
                this.elements.providerSelect.appendChild(option);
            });
            
            this.currentProvider = providers[0]?.name;
        } catch (error) {
            console.error('Failed to load providers:', error);
        }
    }
    
    setupSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/chathub")
            .withAutomaticReconnect()
            .build();
        
        this.connection.start()
            .then(() => console.log('SignalR connected'))
            .catch(err => console.error('SignalR connection error:', err));
    }
    
    setupEventListeners() {
        this.elements.sendBtn.addEventListener('click', () => this.sendMessage());
        this.elements.newChatBtn.addEventListener('click', () => this.createNewThread());
        this.elements.providerSelect.addEventListener('change', (e) => {
            this.currentProvider = e.target.value;
        });
        
        this.elements.messageInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });
    }
    
    createNewThread() {
        this.currentThreadId = this.generateThreadId();
        this.elements.messagesContainer.innerHTML = '';
        this.resetTokenCounter();
    }
    
    generateThreadId() {
        return 'thread_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }
    
    async sendMessage() {
        const message = this.elements.messageInput.value.trim();
        if (!message || this.isStreaming) return;
        
        this.isStreaming = true;
        this.elements.sendBtn.disabled = true;
        this.elements.messageInput.value = '';
        
        // Add user message
        this.addMessage('user', message);
        
        // Add assistant message placeholder
        const assistantMsgId = 'msg_' + Date.now();
        this.addMessage('assistant', '', assistantMsgId);
        
        try {
            // Stream response
            const stream = this.connection.stream(
                "StreamChat",
                this.currentProvider,
                message,
                this.currentThreadId
            );
            
            let fullResponse = '';
            
            stream.subscribe({
                next: (chunk) => {
                    if (chunk.text) {
                        fullResponse += chunk.text;
                        this.updateMessage(assistantMsgId, fullResponse);
                    }
                    
                    if (chunk.isFinal && chunk.usage) {
                        this.updateTokenCounter(chunk.usage);
                    }
                },
                error: (err) => {
                    console.error('Stream error:', err);
                    this.updateMessage(assistantMsgId, 'Error: ' + err.message);
                },
                complete: () => {
                    this.isStreaming = false;
                    this.elements.sendBtn.disabled = false;
                    this.elements.messageInput.focus();
                }
            });
        } catch (error) {
            console.error('Send error:', error);
            this.isStreaming = false;
            this.elements.sendBtn.disabled = false;
        }
    }
    
    addMessage(role, content, id = null) {
        const messageId = id || 'msg_' + Date.now();
        const messageDiv = document.createElement('div');
        messageDiv.id = messageId;
        messageDiv.className = role === 'user' 
            ? 'flex justify-end'
            : 'flex justify-start';
        
        messageDiv.innerHTML = `
            <div class="max-w-3xl ${role === 'user' ? 'bg-blue-600 text-white' : 'bg-white border border-gray-200'} rounded-lg px-4 py-3 shadow-sm">
                <div class="text-sm font-medium mb-1 ${role === 'user' ? 'text-blue-100' : 'text-gray-600'}">
                    ${role === 'user' ? 'You' : this.currentProvider}
                </div>
                <div class="prose prose-sm max-w-none ${role === 'user' ? 'text-white' : 'text-gray-900'}">
                    ${this.escapeHtml(content)}
                </div>
            </div>
        `;
        
        this.elements.messagesContainer.appendChild(messageDiv);
        this.scrollToBottom();
        
        return messageId;
    }
    
    updateMessage(messageId, content) {
        const messageDiv = document.getElementById(messageId);
        if (messageDiv) {
            const contentDiv = messageDiv.querySelector('.prose');
            contentDiv.textContent = content;
        }
    }
    
    updateTokenCounter(usage) {
        this.elements.tokenCounter.classList.remove('hidden');
        this.elements.inputTokens.textContent = usage.inputTokens;
        this.elements.outputTokens.textContent = usage.outputTokens;
        this.elements.totalTokens.textContent = usage.totalTokens;
    }
    
    resetTokenCounter() {
        this.elements.tokenCounter.classList.add('hidden');
        this.elements.inputTokens.textContent = '0';
        this.elements.outputTokens.textContent = '0';
        this.elements.totalTokens.textContent = '0';
    }
    
    scrollToBottom() {
        this.elements.messagesContainer.scrollTop = 
            this.elements.messagesContainer.scrollHeight;
    }
    
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Initialize app when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    new ChatApp();
});
```

---

## Phase 9: Aspire Orchestration (Day 9)

### 9.1 AppHost Configuration

**AIChat.AppHost/Program.cs**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var webapi = builder.AddProject<Projects.AIChat_WebApi>("webapi")
    .WithExternalHttpEndpoints();

builder.Build().Run();
```

---

## Phase 10: Testing & Refinement (Day 10)

### 10.1 Manual Testing Checklist
- [ ] Provider switching works
- [ ] Messages stream correctly
- [ ] Token counts display accurately
- [ ] Thread persistence works across sessions
- [ ] New chat clears conversation
- [ ] Error handling displays properly
- [ ] All three providers work

### 10.2 Edge Cases to Test
- Network interruption during streaming
- Invalid API key
- Provider API down
- Very long messages
- Rapid successive messages
- Browser refresh (thread recovery)

---

## Phase 11: Documentation (Day 10)

### 11.1 README.md
```markdown
# AI Chat Application

## Setup

1. Clone repository
2. Set user secrets:
   ```bash
   cd AIChat.WebApi
   dotnet user-secrets set "Providers:OpenRouter:ApiKey" "your-key"
   ```
3. Run: `dotnet run --project AIChat.AppHost`

## Configuration

Edit `appsettings.json` to add providers or change models.

## Architecture

- **Agents Layer**: IChatClient implementations
- **Infrastructure**: Thread storage
- **WebApi**: SignalR hub + controllers
- **Frontend**: Vanilla JS + Tailwind

## Adding New Provider

1. Implement `IChatClient` in `AIChat.Agents/Providers`
2. Add case in `ChatClientFactory`
3. Add config in `appsettings.json`
4. Register HttpClient in `Program.cs`
```

---

