# AIChat - Multi-Provider AI Chat Application

A modern, scalable .NET 9 web application that provides a unified interface for interacting with multiple AI providers including OpenRouter, NanoGPT, and LM Studio. Built with .Net Clean Architecture principles and featuring real-time chat capabilities through SignalR.

## 🚀 Features

- **Multi-Provider Support**: Seamlessly switch between OpenRouter, NanoGPT, and LM Studio
- **Real-time Chat**: Live streaming responses using SignalR
- **Conversation Management**: Thread-based conversation persistence
- **Token Usage Tracking**: Monitor input/output tokens and response times
- **Modern UI**: Responsive, beautiful interface with Tailwind CSS
- **Comprehensive Testing**: Unit and integration tests for reliability
- **Clean Architecture**: Well-structured, maintainable codebase
- **Responsible AI**: Built-in safety evaluation and content moderation

## 🏗️ Architecture

The project follows Clean Architecture principles with clear separation of concerns:

```
AIChat/
├── AIChat.Shared/           # Shared models and contracts
├── AIChat.Infrastructure/   # Configuration and storage implementations
├── AIChat.Agents/          # AI provider integrations and client factory
├── AIChat.Safety/          # Responsible AI safety evaluation system
├── AIChat.WebApi/          # Web API and SignalR hub
├── AIChat.AppHost/         # Application hosting (Aspire)
├── AIChat.Agents.Tests/    # AI provider integration tests
└── AIChat.Safety.Tests/    # Safety system comprehensive tests
```

## 🛠️ Technology Stack

- **.NET 9**: Latest framework with performance optimizations
- **ASP.NET Core**: Web API and SignalR for real-time communication
- **Microsoft.Extensions.AI**: Unified AI client abstraction
- **SignalR**: Real-time bidirectional communication
- **Tailwind CSS**: Modern, responsive UI framework
- **Entity Framework Core**: Data access (ready for expansion)
- **OpenAI Moderation API**: Content safety evaluation and moderation
- **OpenTelemetry**: Distributed tracing and metrics
- **Polly**: Resilience and transient-fault handling
- **xUnit**: Testing framework
- **Docker**: Containerization support

<img width="2557" height="1248" alt="afbeelding" src="https://github.com/user-attachments/assets/f4aee165-f5fd-423f-b967-f8c401e93e02" />

<img width="2557" height="1252" alt="afbeelding" src="https://github.com/user-attachments/assets/7fe61f57-c9c4-4100-8390-476e69f1b5cf" />

## 📋 Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- API keys for desired AI providers (OpenRouter, NanoGPT)
- LM Studio (optional, for local AI models)

## 🔧 Configuration

### Provider Configuration

Configure your AI providers in [`appsettings.json`](AIChat.WebApi/appsettings.json):

```json
{
  "Providers": {
    "OpenRouter": {
      "BaseUrl": "https://openrouter.ai/api/v1",
      "DefaultModel": "anthropic/claude-3.5-sonnet"
    },
    "NanoGPT": {
      "BaseUrl": "https://nano-gpt.com/api/v1",
      "DefaultModel": "chatgpt-4o-latest"
    },
    "LMStudio": {
      "BaseUrl": "http://localhost:1234/v1",
      "DefaultModel": "openai/gpt-oss-20b"
    }
  }
}
```

### API Keys

Store API keys securely using .NET User Secrets:

```bash
# For OpenRouter
dotnet user-secrets set "Providers:OpenRouter:ApiKey" "your-openrouter-api-key"

# For NanoGPT
dotnet user-secrets set "Providers:NanoGPT:ApiKey" "your-nanogpt-api-key"
```

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/AIChat.git
cd AIChat
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Configure API Keys

```bash
# Set up user secrets for development
dotnet user-secrets init

# Add your API keys
dotnet user-secrets set "Providers:OpenRouter:ApiKey" "your-api-key"
dotnet user-secrets set "Providers:NanoGPT:ApiKey" "your-api-key"
```

### 4. Run the Application

```bash
# Run the Web API
dotnet run --project AIChat.WebApi

# Or run from the solution directory
dotnet run --project src/AIChat.WebApi
```

The application will start at `https://localhost:5001` with the chat interface available at the root URL.

### 5. Run Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test AIChat.Agents.Tests/
```

## 🧪 Testing

The project includes comprehensive integration tests that verify:

- Provider connectivity and response validation
- Multi-message conversation handling
- Error handling for unknown providers
- Token usage tracking
- **Safety system functionality and content moderation**

### Safety System Testing
The AIChat.Safety project includes extensive test coverage:

```bash
# Run all safety tests
dotnet test AIChat.Safety.Tests/

# Run specific safety test categories
dotnet test AIChat.Safety.Tests/ --filter Category=Unit
dotnet test AIChat.Safety.Tests/ --filter Category=Integration
```

Safety tests cover:
- OpenAI Moderation API integration
- Streaming safety evaluation
- Configuration validation
- Fallback behavior testing
- Performance and timeout handling

Run the test harness separately:

```bash
# Run the provider test harness
dotnet run --project AIChat.Agents
```

## 📁 Project Structure

### Core Components

- **[`AIChat.Shared`](AIChat.Shared/)**: Shared models like [`ChatRequest`](AIChat.Shared/Models/ChatRequest.cs) and [`ChatChunk`](AIChat.Shared/Models/ChatChunk.cs)
- **[`AIChat.Infrastructure`](AIChat.Infrastructure/)**: Configuration classes and storage interfaces
- **[`AIChat.Agents`](AIChat.Agents/)**: Provider client factory and AI integrations
- **[`AIChat.WebApi`](AIChat.WebApi/)**: Web API controllers, SignalR hub, and frontend

### Key Files

- [`ProviderClientFactory.cs`](AIChat.Agents/Providers/ProviderClientFactory.cs): Creates AI clients for different providers
- [`ChatHub.cs`](AIChat.WebApi/Hubs/ChatHub.cs): SignalR hub for real-time chat
- [`ThreadsController.cs`](AIChat.WebApi/Controllers/ThreadsController.cs): REST API for conversation management
- [`index.html`](AIChat.WebApi/wwwroot/index.html): Modern chat interface
- [`SafetyEvaluationService.cs`](AIChat.Safety/Services/SafetyEvaluationService.cs): Main safety evaluation orchestration
- [`OpenAIModerationEvaluator.cs`](AIChat.Safety/Providers/OpenAIModerationEvaluator.cs): OpenAI Moderation API integration
- [`SafetyServiceCollectionExtensions.cs`](AIChat.Safety/DependencyInjection/SafetyServiceCollectionExtensions.cs): Safety service registration

## 🔌 Provider Integration

### Adding a New Provider

1. Add provider configuration in [`appsettings.json`](AIChat.WebApi/appsettings.json)
2. Update [`ProviderClientFactory.cs`](AIChat.Agents/Providers/ProviderClientFactory.cs) to handle the new provider
3. Add integration tests in [`ProviderIntegrationTests.cs`](AIChat.Agents.Tests/ProviderIntegrationTests.cs)
4. Update the UI provider selector in [`index.html`](AIChat.WebApi/wwwroot/index.html)

### Supported Providers

- **OpenRouter**: Access to multiple AI models including Claude, GPT-4, and more
- **NanoGPT**: Alternative AI provider with various models
- **LM Studio**: Local AI model hosting for privacy and offline usage

## 🎯 Usage

### Web Interface

1. Navigate to `https://localhost:5001`
2. Select your preferred AI provider from the dropdown
3. Start chatting in the modern interface
4. Monitor token usage and response times in real-time
5. Create new threads for different conversations

### API Endpoints

- `GET /api/providers` - List available providers
- `POST /api/chat` - Send chat message (via SignalR)
- `POST /api/threads/new` - Create new conversation thread
- `GET /api/threads/{threadId}` - Get conversation history
- `GET /api/threads/list` - List all threads

## 🔒 Security

- API keys stored securely using .NET User Secrets
- CORS configured for development environment
- Input validation on all endpoints
- No sensitive data logged

## 🐳 Docker Support

The project includes Docker support for containerized deployment:

```bash
# Build and run with Docker (coming soon)
docker build -t aichat .
docker run -p 5001:80 aichat
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow Clean Architecture principles
- Write tests for new features
- Update documentation as needed
- Use meaningful commit messages
- Ensure all tests pass before submitting

## 🛡️ Responsible AI

AIChat includes a comprehensive Responsible AI safety system that helps ensure safe and appropriate interactions. The system has been migrated from Azure Content Safety to OpenAI Moderation API for improved performance and cost-effectiveness.

### Safety Evaluation Features
- **Real-time Content Moderation**: Uses OpenAI's latest omni-moderation-latest model for comprehensive content analysis
- **Streaming Safety Analysis**: Evaluates AI responses in real-time as they're generated with configurable evaluation strategies
- **Configurable Policies**: Separate policies for user input and AI output with customizable thresholds and risk scores
- **Harm Category Detection**: Identifies hate speech, harassment, self-harm, sexual content, and violence with detailed severity scoring
- **Fallback Mechanisms**: Graceful degradation with configurable fail-open or fail-closed behavior when safety services are unavailable
- **Resilience & Performance**: Built-in retry policies, circuit breakers, and timeout handling for reliable operation

### Key Components
- **[`SafetyEvaluationService`](AIChat.Safety/Services/SafetyEvaluationService.cs)**: Main orchestration service providing high-level safety operations
- **[`OpenAIModerationEvaluator`](AIChat.Safety/Providers/OpenAIModerationEvaluator.cs)**: OpenAI Moderation API integration with support for latest model formats
- **[`OpenAIStreamingSafetyEvaluator`](AIChat.Safety/Providers/OpenAIStreamingSafetyEvaluator.cs)**: Real-time streaming content analysis with multiple evaluation strategies
- **[`SafetyChatClientMiddleware`](AIChat.Safety/Middleware/SafetyChatClientMiddleware.cs)**: AI client middleware for automatic safety integration
- **[`SafetyOptions`](AIChat.Safety/Options/SafetyOptions.cs)**: Comprehensive configuration system with resilience, audit, and filtering settings

### Migration from Azure Content Safety
The safety system has been successfully migrated from Azure Content Safety to OpenAI Moderation API, providing:
- **Cost-Effective**: More affordable pricing model compared to Azure Content Safety
- **Better Performance**: HTTP-based implementation with proper async/await patterns
- **Enhanced Categories**: Support for additional harm categories including harassment
- **Backward Compatibility**: All existing interfaces and contracts remain unchanged
- **Improved Scoring**: More granular severity levels (0-7) based on OpenAI's confidence scores

### Safety Configuration
The safety system is configured through the `Safety` section in [`appsettings.json`](AIChat.WebApi/appsettings.json):

```json
{
  "Safety": {
    "Enabled": true,
    "Endpoint": "https://api.openai.com/v1/moderations",
    "ApiKey": "",
    "OrganizationId": "",
    "Model": "omni-moderation-latest",
    "FallbackBehavior": "FailOpen",
    "InputPolicy": {
      "Thresholds": {
        "Hate": 4,
        "Harassment": 4,
        "SelfHarm": 6,
        "Sexual": 4,
        "Violence": 2
      },
      "BlockOnViolation": true,
      "MaxRiskScore": 70
    },
    "OutputPolicy": {
      "Thresholds": {
        "Hate": 2,
        "Harassment": 2,
        "SelfHarm": 4,
        "Sexual": 2,
        "Violence": 2
      },
      "BlockOnViolation": true,
      "MaxRiskScore": 50
    },
    "Resilience": {
      "TimeoutInMilliseconds": 5000,
      "MaxRetries": 2,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationInSeconds": 30,
      "UseExponentialBackoff": true
    },
    "Audit": {
      "Enabled": true,
      "LogFullContent": false,
      "LogContentHashes": true,
      "LogMetadata": true,
      "RetentionPeriodInDays": 90
    }
  }
}
```

### API Key Configuration
Store your OpenAI API key securely using .NET User Secrets:

```bash
# For OpenAI Moderation API
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key"

# Alternative configuration paths
dotnet user-secrets set "Safety:ApiKey" "your-openai-api-key"
dotnet user-secrets set "Safety:OpenAI:ApiKey" "your-openai-api-key"
```

The system will look for the API key in the following order:
1. `Safety:ApiKey` configuration
2. `Safety:OpenAI:ApiKey` configuration
3. `OpenAI:ApiKey` configuration
4. `OPENAI_API_KEY` environment variable

### Integration Points
- **Chat Pipeline**: Automatic evaluation of user input and AI responses with configurable policies
- **SignalR Streaming**: Real-time safety checks during streaming responses with buffering strategies
- **Audit Logging**: Comprehensive logging of safety violations with content hashes and metadata
- **Health Monitoring**: Built-in health checks for safety service availability and performance
- **OpenTelemetry Integration**: Distributed tracing and metrics for safety operations
- **Dependency Injection**: Seamless integration with .NET's DI container

### Testing & Quality Assurance
The safety system includes comprehensive test coverage:
- **Unit Tests**: Individual component testing with mocked dependencies
- **Integration Tests**: End-to-end testing with real API calls
- **Configuration Tests**: Validation of configuration binding and options
- **Performance Tests**: Load testing and timeout validation
- **Mock Providers**: Test implementations for development scenarios

For detailed implementation information, see [RESPONSIBLE_AI.md](RESPONSIBLE_AI.md), [RAI-Architecture.md](RAI-Architecture.md), and [AIChat.Safety/OPENAI_MIGRATION_SUMMARY.md](AIChat.Safety/OPENAI_MIGRATION_SUMMARY.md).
