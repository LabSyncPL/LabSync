using DotNetEnv;
using LabSync.Agent;
using LabSync.Agent.Services;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment())
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
    if (File.Exists(envPath))
        Env.Load(envPath);
}

builder.Configuration.AddEnvironmentVariables();

var serverUrl = builder.Configuration["ServerUrl"]
    ?? throw new InvalidOperationException("ServerUrl is not configured. Please set it in appsettings.json or an environment variable.");

builder.Services.AddSingleton<AgentIdentityService>();
builder.Services.AddSingleton<ServerClient>();
builder.Services.AddSingleton<ModuleLoader>();

builder.Services.AddHttpClient<ServerClient>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();