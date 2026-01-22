using DotNetEnv;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.EntityFrameworkCore;

if (File.Exists(".env"))
{
    Env.Load();
}
else
{
    Console.WriteLine("Warning: BRAKUJE PLIKU .env");
}


// Pobierz wartości z .env lub użyj domyślnych      moze potem sie to usunie?
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "LabSyncDb";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"];
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? builder.Configuration["Jwt:Issuer"];
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? builder.Configuration["Jwt:Audience"];

builder.Configuration["Jwt:Key"] = jwtKey;
builder.Configuration["Jwt:Issuer"] = jwtIssuer;
builder.Configuration["Jwt:Audience"] = jwtAudience;


builder.Services.AddDbContext<LabSyncDbContext>(options =>
    options.UseNpgsql(connectionString));

var corsOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") 
    ?? "http://localhost:3000,http://localhost:4173";

//Console.WriteLine("=== CORS DEBUG ===");
//Console.WriteLine($"CORS_ALLOWED_ORIGINS: {corsOrigins}");
//Console.WriteLine("==================");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            //policy.AllowAnyOrigin()
            //.AllowAnyHeader()
            //.AllowAnyMethod();            
            if (corsOrigins == "*")
            {
                policy.AllowAnyOrigin();
            }
            else
            {
                policy.WithOrigins(corsOrigins.Split(';'));
            }
            policy.AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LabSyncDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
/// app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();

app.Run();