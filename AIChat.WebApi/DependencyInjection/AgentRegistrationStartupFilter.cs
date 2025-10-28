using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AIChat.Agents.Providers;
using Microsoft.Extensions.Options;
using AIChat.Infrastructure.Configuration;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI;

namespace AIChat.WebApi.DependencyInjection;

internal class AgentRegistrationStartupFilter : IStartupFilter
{
 public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
 {
 return app =>
 {
 var sp = app.ApplicationServices;
 var options = sp.GetRequiredService<IOptions<ProvidersConfiguration>>();
 var cfg = options.Value;
 var factory = sp.GetRequiredService<ProviderClientFactory>();
 var registry = sp.GetRequiredService<IAgentRegistry>();

 if (cfg?.Providers != null)
 {
 foreach (var providerName in cfg.Providers.Keys)
 {
 var chatClient = factory.CreateChatClient(providerName);
 var agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
 {
 Name = providerName,
 Instructions = "You are a helpful AI assistant that provides accurate and concise responses."
 });

 registry.Register(providerName, agent);
 }
 }

 next(app);
 };
 }
}
