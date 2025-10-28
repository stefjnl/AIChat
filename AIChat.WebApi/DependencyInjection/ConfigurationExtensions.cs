using AIChat.Infrastructure.Configuration;
using AIChat.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIChat.WebApi.DependencyInjection;

internal static class ConfigurationExtensions
{
 /// <summary>
 /// Binds and registers configuration POCOs used by the Web API.
 /// </summary>
 public static void BindAndRegisterConfigurations(this WebApplicationBuilder builder)
 {
 // Register ProvidersConfiguration and StorageOptions via IOptions
 var providersSection = builder.Configuration.GetSection("Providers");
 builder.Services.Configure<ProvidersConfiguration>(providersSection);

 builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
 }
}
