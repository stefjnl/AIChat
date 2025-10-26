var builder = DistributedApplication.CreateBuilder(args);

var webApi = builder.AddProject<Projects.AIChat_WebApi>("webapi")
    .WithExternalHttpEndpoints()
    .WithEnvironment("Providers__OpenRouter__ApiKey", builder.Configuration["Providers:OpenRouter:ApiKey"] ?? "")
    .WithEnvironment("Providers__NanoGPT__ApiKey", builder.Configuration["Providers:NanoGPT:ApiKey"] ?? "")
    .WithEnvironment("Providers__LMStudio__ApiKey", builder.Configuration["Providers:LMStudio:ApiKey"] ?? "");

builder.Build().Run();
