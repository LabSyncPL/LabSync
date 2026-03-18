using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.IO;

/// Enviroment configuration
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
    if (File.Exists(envPath))
        Env.Load(envPath);
}

builder.Configuration.AddEnvironmentVariables();

builder.AddDatabaseAndCors();
builder.AddAppServices();
builder.AddAppAuthentication();


var app = builder.Build();

// Configure HTTP Pipeline via Extensions
app.UseAppPipeline();

app.Run();