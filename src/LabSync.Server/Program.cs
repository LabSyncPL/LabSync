using DotNetEnv;
using LabSync.Core.Interfaces;
using LabSync.Server.Authentication;
using LabSync.Server.Data;
using LabSync.Server.Hubs;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Important for SignalR
    });
});

// Register application services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ICryptoService, CryptoService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<JobDispatchService>();
builder.Services.AddSingleton<ConnectionTracker>();


// Add Authentication and Authorization
builder.Services.AddAuthentication(options =>
    {
        // Default to JWT for standard API controllers
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, DeviceKeyAuthenticationHandler>(DeviceKeyAuthenticationHandler.SchemeName, null)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key not configured.")))
        };
        // SignalR sends the token via query string (access_token), not Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/agentHub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    // The "Agent" role is implicitly handled by the [Authorize] attribute on the hub
});

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
}).AddMessagePackProtocol();
builder.Services.AddControllers();

var app = builder.Build();

// Global Exception Handler
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (contextFeature != null)
        {
            // In a real app, you would log this exception.
            // For now, we just return a generic error message.
            await context.Response.WriteAsJsonAsync(new { Message = "An unexpected server error has occurred." });
        }
    });
});


// Apply database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LabSyncDbContext>();
    dbContext.Database.Migrate();
}

app.UseRouting();
app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AgentHub>("/agentHub");
app.Run();