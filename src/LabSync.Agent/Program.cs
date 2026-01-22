using DotNetEnv;
using LabSync.Agent;
using LabSync.Agent.Services;

if (File.Exists(".env"))
{
    Env.Load();
}
else
{
    Console.WriteLine("Agent: Plik .env nie znaleziony, u¿ywam konfiguracji z appsettings.json");
}

var builder = Host.CreateApplicationBuilder(args);


var serverUrl = Environment.GetEnvironmentVariable("AGENT_SERVER_URL")
    ?? builder.Configuration["ServerUrl"]
    ?? "http://localhost:5000";


builder.Services.AddSingleton<AgentIdentityService>();
builder.Services.AddSingleton<ServerClient>();
builder.Services.AddSingleton<ModuleLoader>();

builder.Services.AddHttpClient<ServerClient>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
});

builder.Services.AddHostedService<Worker>();
///////////
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var host = builder.Build();
host.Run();