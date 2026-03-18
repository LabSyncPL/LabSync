using LabSync.Server.Data;
using LabSync.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

public static class ApplicationPipelineExtensions
{
    public static WebApplication UseAppPipeline(this WebApplication app)
    {
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

        if (app.Configuration.GetValue<bool>("SEED_DATA_DELETE") || app.Configuration.GetValue<bool>("SEED_DATA"))
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<LabSyncDbContext>();
                if (app.Configuration.GetValue<bool>("SEED_DATA_DELETE"))
                    DataSeeder.DeleteSeedAsync(dbContext).Wait();

                if (app.Configuration.GetValue<bool>("SEED_DATA"))
                {
                    DataSeeder.SeedAsync(dbContext).Wait();
                    Console.WriteLine("[!!!] Database seeded successfully.");
                }
            }
        }

        app.UseRouting();
        app.UseCors("AllowReactApp");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<AgentHub>("/agentHub");
        app.MapHub<RemoteDesktopHub>("/remoteDesktopHub");
        app.MapHub<SshTerminalHub>("/sshTerminalHub");


        var urls = app.Configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrEmpty(urls))
        {
            app.Urls.Clear();
            foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                app.Urls.Add(url);
            }
        }

        return app;
    }
}