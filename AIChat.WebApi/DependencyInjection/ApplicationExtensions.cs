using AIChat.Agents.Providers;
using AIChat.Infrastructure.Configuration;
using AIChat.Safety.DependencyInjection;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AIChat.WebApi.Services;
using Microsoft.Extensions.Options;

namespace AIChat.WebApi.DependencyInjection;

internal static class ApplicationExtensions
{
 public static void AddApplicationServices(this WebApplicationBuilder builder)
 {
 // Bind configuration POCOs
 builder.BindAndRegisterConfigurations();

 // Require providers configuration to be present
 var providersSection = builder.Configuration.GetSection("Providers");
 var providersConfig = providersSection.Get<ProvidersConfiguration>();
 if (providersConfig == null || providersConfig.Providers == null || providersConfig.Providers.Count ==0)
 {
 throw new InvalidOperationException("Providers configuration is missing or empty. Please configure the 'Providers' section.");
 }

 // SAFETY
 builder.Services.AddAISafetyServices(builder.Configuration);

 // INFRASTRUCTURE
 builder.Services.AddInfrastructureServices();

 // Agent registry used to resolve agents by name at runtime
 builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();

 // Register startup filter to create and register agents at application startup
 builder.Services.AddSingleton<IStartupFilter>(_ => new AgentRegistrationStartupFilter());

 // WEB services
 builder.Services.ConfigureHttpJsonOptions(options =>
 {
 options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
 });

 builder.Services.AddSingleton<ITitleGenerator, TitleGenerationService>();
 builder.Services.AddSingleton<TextAnalysisService>();

 builder.Services.AddControllers();
 builder.Services.AddSignalR();
 builder.Services.AddCors(options =>
 {
 options.AddDefaultPolicy(policy =>
 {
 policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
 });
 });

 // Register AgentService which resolves agents via IAgentRegistry
 builder.Services.AddSingleton<AgentService>();
 }
}
