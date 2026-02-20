using DotNetEnv;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
    if (File.Exists(envPath))
        Env.Load(envPath);
}

builder.Configuration.AddEnvironmentVariables();

var dbHost = builder.Configuration["DB_HOST"];
var dbPort = builder.Configuration["DB_PORT"];
var dbName = builder.Configuration["DB_NAME"];
var dbUser = builder.Configuration["DB_USER"];
var dbPass = builder.Configuration["DB_PASSWORD"];
var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass}";

builder.Services.AddDbContext<LabSyncDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure CORS
var corsOrigins = builder.Configuration.GetValue<string>("CORS_ALLOWED_ORIGINS");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        if (string.IsNullOrEmpty(corsOrigins) || corsOrigins == "*")
            policy.AllowAnyOrigin();
        else
            policy.WithOrigins(corsOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries));

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddControllers();

var app = builder.Build();

// Apply database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LabSyncDbContext>();
    dbContext.Database.Migrate();
}

app.UseRouting();
app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();

app.Run();