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

## 🏗️ Architecture

The project follows Clean Architecture principles with clear separation of concerns:

```
AIChat/
├── AIChat.Shared/           # Shared models and contracts
├── AIChat.Infrastructure/   # Configuration and storage implementations
├── AIChat.Agents/          # AI provider integrations and client factory
├── AIChat.WebApi/          # Web API and SignalR hub
├── AIChat.AppHost/         # Application hosting (Aspire)
└── AIChat.Agents.Tests/    # Comprehensive test suite
```

## 🛠️ Technology Stack

- **.NET 9**: Latest framework with performance optimizations
- **ASP.NET Core**: Web API and SignalR for real-time communication
- **Microsoft.Extensions.AI**: Unified AI client abstraction
- **SignalR**: Real-time bidirectional communication
- **Tailwind CSS**: Modern, responsive UI framework
- **Entity Framework Core**: Data access (ready for expansion)
- **xUnit**: Testing framework
- **Docker**: Containerization support

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