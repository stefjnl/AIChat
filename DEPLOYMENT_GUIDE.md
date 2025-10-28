# Responsible AI Deployment Guide

This guide provides comprehensive instructions for deploying and configuring the Responsible AI safety system in various environments.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Environment Setup](#environment-setup)
3. [Configuration Management](#configuration-management)
4. [Deployment Options](#deployment-options)
5. [Monitoring and Observability](#monitoring-and-observability)
6. [Security Considerations](#security-considerations)
7. [Troubleshooting](#troubleshooting)
8. [Maintenance and Updates](#maintenance-and-updates)

## Prerequisites

### System Requirements

- **.NET 9.0 SDK** or later
- **ASP.NET Core Runtime** 9.0 or later
- **OpenAI API Key** with access to Moderation API

### API Requirements

- **OpenAI API Key**: Valid API key with moderation endpoint access
- **Rate Limits**: Understand OpenAI's rate limits and quotas
- **Organization ID** (optional): For organizational access control

### Infrastructure Requirements

- **Web Server**: IIS, Nginx, Apache, or cloud hosting
- **Load Balancer** (optional): For high availability
- **Monitoring**: Application Insights, Prometheus, or similar
- **Logging**: Structured logging solution (Serilog, ELK stack)

## Environment Setup

### Development Environment

#### 1. Clone and Build

```bash
# Clone the repository
git clone https://github.com/your-org/AIChat.git
cd AIChat

# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release
```

#### 2. Configure Development Settings

Create `appsettings.Development.json`:

```json
{
  "Safety": {
    "Enabled": true,
    "FallbackBehavior": "FailOpen",
    "OpenAI": {
      "ApiKey": "${OPENAI_API_KEY}",
      "Model": "text-moderation-latest"
    },
    "Resilience": {
      "TimeoutInMilliseconds": 10000,
      "MaxRetries": 3
    },
    "Audit": {
      "Enabled": true,
      "LogFullContent": true
    },
    "RateLimit": {
      "Enabled": false
    }
  },
  "Logging": {
    "LogLevel": {
      "AIChat.Safety": "Debug",
      "Microsoft": "Information"
    }
  }
}
```

#### 3. Set Up User Secrets

```bash
# Navigate to WebApi project
cd AIChat.WebApi

# Initialize user secrets
dotnet user-secrets init

# Set OpenAI API key
dotnet user-secrets set "Safety:OpenAI:ApiKey" "your-openai-api-key"

# Set organization ID (optional)
dotnet user-secrets set "Safety:OpenAI:OrganizationId" "your-org-id"
```

#### 4. Run Development Server

```bash
# Run the Web API
dotnet run --project AIChat.WebApi

# Or using the .NET CLI
cd AIChat.WebApi
dotnet run
```

### Staging Environment

#### 1. Configuration

Create `appsettings.Staging.json`:

```json
{
  "Safety": {
    "Enabled": true,
    "FallbackBehavior": "FailOpen",
    "OpenAI": {
      "ApiKey": "${SAFETY_OPENAI_API_KEY}",
      "OrganizationId": "${SAFETY_OPENAI_ORG_ID}",
      "Model": "text-moderation-latest"
    },
    "InputPolicy": {
      "Thresholds": {
        "Hate": 3,
        "SelfHarm": 5,
        "Sexual": 3,
        "Violence": 2
      },
      "MaxRiskScore": 60
    },
    "OutputPolicy": {
      "Thresholds": {
        "Hate": 2,
        "SelfHarm": 4,
        "Sexual": 2,
        "Violence": 1
      },
      "MaxRiskScore": 40
    },
    "Resilience": {
      "TimeoutInMilliseconds": 5000,
      "MaxRetries": 2,
      "CircuitBreakerThreshold": 10
    },
    "Audit": {
      "Enabled": true,
      "LogFullContent": false,
      "LogContentHashes": true
    },
    "RateLimit": {
      "Enabled": true,
      "MaxEvaluationsPerWindow": 5000,
      "WindowInSeconds": 60
    }
  }
}
```

#### 2. Environment Variables

```bash
# Set environment variables
export ASPNETCORE_ENVIRONMENT=Staging
export SAFETY_OPENAI_API_KEY="your-staging-api-key"
export SAFETY_OPENAI_ORG_ID="your-staging-org-id"
```

### Production Environment

#### 1. Configuration

Create `appsettings.Production.json`:

```json
{
  "Safety": {
    "Enabled": true,
    "FallbackBehavior": "FailClosed",
    "OpenAI": {
      "ApiKey": "${SAFETY_OPENAI_API_KEY}",
      "OrganizationId": "${SAFETY_OPENAI_ORG_ID}",
      "Model": "text-moderation-latest",
      "Endpoint": "https://api.openai.com/v1/moderations"
    },
    "InputPolicy": {
      "Thresholds": {
        "Hate": 4,
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
        "SelfHarm": 4,
        "Sexual": 2,
        "Violence": 2
      },
      "BlockOnViolation": true,
      "MaxRiskScore": 50
    },
    "Resilience": {
      "TimeoutInMilliseconds": 3000,
      "MaxRetries": 3,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationInSeconds": 60,
      "UseExponentialBackoff": true,
      "BaseRetryDelayInMilliseconds": 1000,
      "MaxRetryDelayInMilliseconds": 10000
    },
    "Audit": {
      "Enabled": true,
      "LogFullContent": false,
      "LogContentHashes": true,
      "LogMetadata": true,
      "LogDetailedScores": true,
      "RetentionPeriodInDays": 365,
      "AlertThreshold": 3
    },
    "Advanced": {
      "EnableStreamingEvaluation": true,
      "StreamingBufferSize": 100,
      "EnableCaching": true,
      "CacheExpirationInMinutes": 60
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "AIChat.Safety": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Configuration Management

### Azure App Service Configuration

#### 1. Application Settings

```bash
# Using Azure CLI
az webapp config appsettings set \
  --resource-group your-resource-group \
  --name your-app-name \
  --settings \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "Safety:OpenAI:ApiKey=@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/OpenAIApiKey/)" \
    "Safety:OpenAI:OrganizationId=@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/OpenAIOrgId/)" \
    "APPLICATIONINSIGHTS_CONNECTION_STRING=your-connection-string"
```

#### 2. Key Vault Integration

```bash
# Create Key Vault
az keyvault create \
  --name your-safety-vault \
  --resource-group your-resource-group \
  --location your-location

# Store API keys
az keyvault secret set \
  --vault-name your-safety-vault \
  --name OpenAIApiKey \
  --value your-openai-api-key

az keyvault secret set \
  --vault-name your-safety-vault \
  --name OpenAIOrgId \
  --value your-organization-id

# Grant access to App Service
az webapp identity assign \
  --resource-group your-resource-group \
  --name your-app-name

az keyvault set-policy \
  --name your-safety-vault \
  --spn <app-service-principal-id> \
  --secret-permissions get list
```

### Docker Configuration

#### 1. Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AIChat.WebApi/AIChat.WebApi.csproj", "AIChat.WebApi/"]
COPY ["AIChat.Safety/AIChat.Safety.csproj", "AIChat.Safety/"]
COPY ["AIChat.Shared/AIChat.Shared.csproj", "AIChat.Shared/"]
COPY ["AIChat.Infrastructure/AIChat.Infrastructure.csproj", "AIChat.Infrastructure/"]
COPY ["AIChat.Agents/AIChat.Agents.csproj", "AIChat.Agents/"]
RUN dotnet restore "AIChat.WebApi/AIChat.WebApi.csproj"
COPY . .
WORKDIR "/src/AIChat.WebApi"
RUN dotnet build "AIChat.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AIChat.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AIChat.WebApi.dll"]
```

#### 2. Docker Compose

```yaml
version: '3.8'

services:
  aichat-webapi:
    build: .
    ports:
      - "5001:80"
      - "5002:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Safety__OpenAI__ApiKey=${OPENAI_API_KEY}
      - Safety__OpenAI__OrganizationId=${OPENAI_ORG_ID}
      - APPLICATIONINSIGHTS_CONNECTION_STRING=${APPINSIGHTS_CONNECTION_STRING}
    volumes:
      - ./data:/app/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    restart: unless-stopped

volumes:
  redis_data:
```

#### 3. Environment File

```bash
# .env.production
ASPNETCORE_ENVIRONMENT=Production
OPENAI_API_KEY=your-production-api-key
OPENAI_ORG_ID=your-production-org-id
APPINSIGHTS_CONNECTION_STRING=your-connection-string
```

## Deployment Options

### Azure Container Instances

#### 1. Deployment Script

```bash
#!/bin/bash

# Variables
RESOURCE_GROUP="aichat-rg"
LOCATION="eastus"
CONTAINER_NAME="aichat-safety"
IMAGE_NAME="aichat/webapi:latest"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Deploy container
az container create \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --image $IMAGE_NAME \
  --dns-name-label aichat-safety-unique \
  --ports 80 \
  --environment-variables \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "Safety__OpenAI__ApiKey=$OPENAI_API_KEY" \
    "Safety__OpenAI__OrganizationId=$OPENAI_ORG_ID" \
  --secure-environment-variables \
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$APPINSIGHTS_CONNECTION_STRING"
```

### Kubernetes Deployment

#### 1. Kubernetes Manifest

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: aichat-safety
  labels:
    app: aichat-safety
spec:
  replicas: 3
  selector:
    matchLabels:
      app: aichat-safety
  template:
    metadata:
      labels:
        app: aichat-safety
    spec:
      containers:
      - name: aichat-webapi
        image: aichat/webapi:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Safety__OpenAI__ApiKey
          valueFrom:
            secretKeyRef:
              name: aichat-secrets
              key: openai-api-key
        - name: Safety__OpenAI__OrganizationId
          valueFrom:
            secretKeyRef:
              name: aichat-secrets
              key: openai-org-id
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /healthz
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: aichat-safety-service
spec:
  selector:
    app: aichat-safety
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: LoadBalancer

---
apiVersion: v1
kind: Secret
metadata:
  name: aichat-secrets
type: Opaque
data:
  openai-api-key: <base64-encoded-api-key>
  openai-org-id: <base64-encoded-org-id>
```

#### 2. Helm Chart

```yaml
# values.yaml
replicaCount: 3

image:
  repository: aichat/webapi
  tag: latest
  pullPolicy: IfNotPresent

service:
  type: LoadBalancer
  port: 80

ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
  hosts:
    - host: aichat.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: aichat-tls
      hosts:
        - aichat.example.com

resources:
  limits:
    cpu: 500m
    memory: 1Gi
  requests:
    cpu: 250m
    memory: 512Mi

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70

config:
  aspnetCoreEnvironment: Production
  safety:
    enabled: true
    fallbackBehavior: FailClosed
    openai:
      model: text-moderation-latest

secrets:
  openaiApiKey: ""
  openaiOrgId: ""
  appInsightsConnectionString: ""
```

### AWS Deployment

#### 1. ECS Task Definition

```json
{
  "family": "aichat-safety",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "executionRoleArn": "arn:aws:iam::account:role/ecsTaskExecutionRole",
  "taskRoleArn": "arn:aws:iam::account:role/ecsTaskRole",
  "containerDefinitions": [
    {
      "name": "aichat-webapi",
      "image": "your-account.dkr.ecr.region.amazonaws.com/aichat/webapi:latest",
      "portMappings": [
        {
          "containerPort": 80,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ],
      "secrets": [
        {
          "name": "Safety__OpenAI__ApiKey",
          "valueFrom": "arn:aws:secretsmanager:region:account:secret:aichat/openai-api-key"
        },
        {
          "name": "Safety__OpenAI__OrganizationId",
          "valueFrom": "arn:aws:secretsmanager:region:account:secret:aichat/openai-org-id"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/aichat-safety",
          "awslogs-region": "us-west-2",
          "awslogs-stream-prefix": "ecs"
        }
      },
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost/healthz || exit 1"],
        "interval": 30,
        "timeout": 5,
        "retries": 3
      }
    }
  ]
}
```

## Monitoring and Observability

### Application Insights Configuration

#### 1. Configure OpenTelemetry

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("AIChat.Safety")
               .AddSource("Microsoft.Extensions.AI")
               .AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddAzureMonitorTraceExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddMeter("AIChat.Safety")
               .AddAzureMonitorMetricExporter();
    })
    .WithLogging(logging =>
    {
        logging.AddAzureMonitorLogExporter();
    });
```

#### 2. Custom Metrics

```csharp
public class SafetyMetrics
{
    private readonly Counter<int> _evaluationCounter;
    private readonly Histogram<double> _evaluationDuration;
    private readonly Counter<int> _violationCounter;

    public SafetyMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("AIChat.Safety");
        
        _evaluationCounter = meter.CreateCounter<int>(
            "safety_evaluations_total",
            description: "Total number of safety evaluations");
            
        _evaluationDuration = meter.CreateHistogram<double>(
            "safety_evaluation_duration_seconds",
            description: "Duration of safety evaluations");
            
        _violationCounter = meter.CreateCounter<int>(
            "safety_violations_total",
            description: "Total number of safety violations");
    }

    public void RecordEvaluation(bool isViolation, TimeSpan duration)
    {
        _evaluationCounter.Add(1);
        _evaluationDuration.Record(duration.TotalSeconds);
        
        if (isViolation)
        {
            _violationCounter.Add(1);
        }
    }
}
```

### Health Checks

#### 1. Comprehensive Health Check

```csharp
public class SafetyHealthCheck : IHealthCheck
{
    private readonly ISafetyEvaluator _evaluator;
    private readonly ILogger<SafetyHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _evaluator.EvaluateTextAsync("Health check test", cancellationToken);
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["provider"] = _evaluator.GetProviderName(),
                ["responseTimeMs"] = stopwatch.ElapsedMilliseconds,
                ["supportedCategories"] = _evaluator.GetSupportedCategories().Count
            };

            return result?.IsSafe == true
                ? HealthCheckResult.Healthy("Safety service is operational", data)
                : HealthCheckResult.Degraded("Safety service returned unexpected result", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Safety service health check failed");
            return HealthCheckResult.Unhealthy("Safety service is not operational", ex);
        }
    }
}
```

### Dashboard Configuration

#### 1. Azure Dashboard Template

```json
{
  "properties": {
    "displayName": "AIChat Safety Monitoring",
    "lenses": [
      {
        "order": 0,
        "parts": [
          {
            "position": {
              "x": 0,
              "y": 0,
              "colSpan": 6,
              "rowSpan": 3
            },
            "metadata": {
              "type": "Extension/Microsoft_Azure_Monitoring/PartType/MetricsChart",
              "inputs": [
                {
                  "name": "metrics",
                  "value": {
                    "resourceId": "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/microsoft.insights/components/{app-insights-name}",
                    "metricNamespace": "azure.applicationinsights",
                    "metricName": "safety_evaluations_total",
                    "aggregation": "Sum"
                  }
                }
              ]
            }
          },
          {
            "position": {
              "x": 6,
              "y": 0,
              "colSpan": 6,
              "rowSpan": 3
            },
            "metadata": {
              "type": "Extension/Microsoft_Azure_Monitoring/PartType/MetricsChart",
              "inputs": [
                {
                  "name": "metrics",
                  "value": {
                    "resourceId": "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/microsoft.insights/components/{app-insights-name}",
                    "metricNamespace": "azure.applicationinsights",
                    "metricName": "safety_evaluation_duration_seconds",
                    "aggregation": "Avg"
                  }
                }
              ]
            }
          }
        ]
      }
    ]
  }
}
```

## Security Considerations

### API Key Security

#### 1. Key Rotation Strategy

```bash
# Rotate OpenAI API key
az keyvault secret set \
  --vault-name your-safety-vault \
  --name OpenAIApiKey \
  --value new-api-key \
  --expires $(date -d "+90 days" --iso-8601)

# Update application to use new key
# Application will automatically pick up new key on next restart
```

#### 2. Access Control

```bash
# Create managed identity
az identity create \
  --resource-group your-resource-group \
  --name aichat-identity

# Grant access to Key Vault
az keyvault set-policy \
  --name your-safety-vault \
  --object-principal-id $(az identity show --resource-group your-resource-group --name aichat-identity --query principalId -o tsv) \
  --secret-permissions get list
```

### Network Security

#### 1. VNET Integration

```bash
# Create VNET
az network vnet create \
  --resource-group your-resource-group \
  --name aichat-vnet \
  --address-prefix 10.0.0.0/16

# Create subnet
az network vnet subnet create \
  --resource-group your-resource-group \
  --vnet-name aichat-vnet \
  --name web-subnet \
  --address-prefix 10.0.1.0/24

# Integrate App Service with VNET
az webapp vnet-integration add \
  --resource-group your-resource-group \
  --name your-app-name \
  --vnet aichat-vnet \
  --subnet web-subnet
```

#### 2. NSG Configuration

```bash
# Create Network Security Group
az network nsg create \
  --resource-group your-resource-group \
  --name aichat-nsg

# Add outbound rule for OpenAI API
az network nsg rule create \
  --resource-group your-resource-group \
  --nsg-name aichat-nsg \
  --name allow-openai \
  --direction Outbound \
  --priority 100 \
  --access Allow \
  --protocol Tcp \
  --destination-address-prefixes "api.openai.com" \
  --destination-port-ranges 443
```

## Troubleshooting

### Common Issues

#### 1. API Key Not Working

**Symptoms**: 401 Unauthorized errors

**Solutions**:
```bash
# Test API key
curl -X POST https://api.openai.com/v1/moderations \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"input": "test"}'

# Check configuration
az webapp config appsettings list \
  --resource-group your-resource-group \
  --name your-app-name \
  --query "[?name=='Safety:OpenAI:ApiKey']"
```

#### 2. High Latency

**Symptoms**: Slow response times

**Solutions**:
- Check network connectivity to OpenAI API
- Review timeout configurations
- Monitor circuit breaker status
- Check for rate limiting

#### 3. Memory Leaks

**Symptoms**: Increasing memory usage over time

**Solutions**:
- Monitor streaming evaluator disposal
- Check for caching issues
- Review object lifecycle management

### Diagnostic Tools

#### 1. Health Check Endpoints

```bash
# Basic health check
curl https://your-app.com/healthz

# Detailed health check
curl https://your-app.com/health/detailed

# Safety service health
curl https://your-app.com/api/health/safety
```

#### 2. Log Analysis

```bash
# View recent logs
az webapp log tail \
  --resource-group your-resource-group \
  --name your-app-name

# Filter safety-related logs
az webapp log tail \
  --resource-group your-resource-group \
  --name your-app-name \
  --filter "AIChat.Safety"
```

## Maintenance and Updates

### Update Process

#### 1. Zero-Downtime Deployment

```bash
# Deploy to staging slot
az webapp deployment source config-zip \
  --resource-group your-resource-group \
  --name your-app-name \
  --slot staging \
  --src deployment-package.zip

# Swap slots
az webapp deployment slot swap \
  --resource-group your-resource-group \
  --name your-app-name \
  --slot staging \
  --target-slot production
```

#### 2. Configuration Updates

```bash
# Update configuration without downtime
az webapp config appsettings set \
  --resource-group your-resource-group \
  --name your-app-name \
  --settings "Safety:InputPolicy:Thresholds:Hate=3"
```

### Backup and Recovery

#### 1. Configuration Backup

```bash
# Export configuration
az webapp config appsettings list \
  --resource-group your-resource-group \
  --name your-app-name \
  --output json > appsettings-backup.json

# Export Key Vault secrets
az keyvault secret list \
  --vault-name your-safety-vault \
  --output json > keyvault-backup.json
```

#### 2. Disaster Recovery

```bash
# Restore from backup
az webapp config appsettings set \
  --resource-group your-resource-group \
  --name your-app-name \
  --settings @appsettings-backup.json

# Deploy to backup region
az webapp deployment source config-zip \
  --resource-group backup-resource-group \
  --name backup-app-name \
  --src deployment-package.zip
```

---

For additional information, see:
- [RESPONSIBLE_AI.md](RESPONSIBLE_AI.md) - Implementation guide
- [SAFETY_API.md](SAFETY_API.md) - API documentation
- [RAI-Architecture.md](RAI-Architecture.md) - Technical architecture