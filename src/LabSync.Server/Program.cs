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

/// Enviroment configuration
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
    if (File.Exists(envPath))
        Env.Load(envPath);
}

builder.Configuration.AddEnvironmentVariables();

/// Database configuration
string GetRequired(string key) => builder.Configuration[key] ?? throw new InvalidOperationException($"{key} is not configured.");
var connectionString =
    $"Host={GetRequired("DB_HOST")};" +
    $"Port={GetRequired("DB_PORT")};" +
    $"Database={GetRequired("DB_NAME")};" +
    $"Username={GetRequired("DB_USER")};" +
    $"Password={GetRequired("DB_PASSWORD")}";

builder.Services.AddDbContext<LabSyncDbContext>(options =>
    options.UseNpgsql(connectionString));

/// CORS configuration
var corsOrigins = builder.Configuration.GetValue<string>("CORS_ALLOWED_ORIGINS");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        if (string.IsNullOrEmpty(corsOrigins) || corsOrigins == "*")
        {
            policy.SetIsOriginAllowed(_ => true);
        }
        else
        {
            policy.WithOrigins(corsOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register application services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ICryptoService, CryptoService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<JobDispatchService>();
builder.Services.AddSingleton<ConnectionTracker>();

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;})
    .AddScheme<AuthenticationSchemeOptions, DeviceKeyAuthenticationHandler>(DeviceKeyAuthenticationHandler.SchemeName, null)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key not configured.")))
        };
        /// SignalR sends the token via query string (access_token), not Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/agentHub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    }
);


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    /// The "Agent" role is implicitly handled by the [Authorize] attribute on the hub
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
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(contextFeature.Error, "Unhandled exception");
            await context.Response.WriteAsJsonAsync(new { Message = "An unexpected server error has occurred." });
        }
    });
});


// Apply database migrations on startup
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<LabSyncDbContext>();
        dbContext.Database.Migrate();
    }
}

app.UseRouting();
app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AgentHub>("/agentHub");

var urls = builder.Configuration["ASPNETCORE_URLS"];
if (!string.IsNullOrEmpty(urls))
{
    app.Urls.Clear();
    foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        app.Urls.Add(url);
    }
}

app.Run();