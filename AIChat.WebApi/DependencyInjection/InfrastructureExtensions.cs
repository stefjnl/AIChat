using AIChat.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using AIChat.Agents.Providers;
using AIChat.WebApi.Services;

namespace AIChat.WebApi.DependencyInjection;

internal static class InfrastructureExtensions
{
 public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
 {
 services.AddSingleton<IThreadStorage, FileThreadStorage>();
 services.AddSingleton<IChatHistoryStorage, FileChatHistoryStorage>();
 services.AddSingleton<ProviderClientFactory>();
 services.AddSingleton<AgentService>();

 return services;
 }
}
