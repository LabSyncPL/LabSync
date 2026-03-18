using LabSync.Core.Interfaces;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

public static class BuilderExtensions
{
    public static WebApplicationBuilder AddDatabaseAndCors(this WebApplicationBuilder builder)
    {
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

        return builder;
    }

    public static WebApplicationBuilder AddAppServices(this WebApplicationBuilder builder)
    {
        // Register application services
        builder.Services.AddSingleton<TokenService>();
        builder.Services.AddSingleton<ICryptoService, CryptoService>();
        builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
        builder.Services.AddSingleton<ISecretProvider, FileSecretProvider>();
        builder.Services.AddScoped<JobDispatchService>();
        builder.Services.AddSingleton<ConnectionTracker>();
        builder.Services.AddSingleton<GridMonitorTracker>();
        builder.Services.AddSingleton<SshSessionManager>();

        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
        })
        .AddMessagePackProtocol();

        builder.Services.AddControllers();

        return builder;
    }
}