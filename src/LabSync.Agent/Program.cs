using LabSync.Agent;
using LabSync.Agent.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<SystemInfoService>();
builder.Services.AddSingleton<ServerClient>();

builder.Services.AddHttpClient<ServerClient>(client =>
{
    var url = builder.Configuration["ServerUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();