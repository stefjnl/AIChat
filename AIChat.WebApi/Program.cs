using AIChat.WebApi.DependencyInjection;
using AIChat.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Minimal configuration in Program.cs
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Register all application services (infrastructure, agents, telemetry, safety)
builder.AddApplicationServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors();
app.UseRouting();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");
app.MapHub<TelemetryHub>("/telemetryhub");

app.MapGet("/", (IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.ContentRootPath, "wwwroot", "index.html");
    return Results.File(filePath, "text/html");
});

app.Run();