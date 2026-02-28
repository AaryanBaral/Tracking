using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Tracker.Api.Data;
using Tracker.Api.Endpoints;
using Tracker.Api.Middleware;
using Tracker.Api.Security;
using Tracker.Api.Infrastructure;
using Tracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});
builder.Services.AddDbContext<TrackerDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")).EnableSensitiveDataLogging());
builder.Services.AddCors(options =>
{
    options.AddPolicy("ui", policy =>
    {
        policy.WithOrigins("http://localhost:8080", "http://192.168.0.6:8080", "http://localhost:5000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddScoped<IIngestService, IngestService>();
builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection("AdminSeed"));
builder.Services.AddScoped<AdminSeeder>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authConfig = builder.Configuration.GetSection("Auth").Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(authConfig.SigningKey) || Encoding.UTF8.GetByteCount(authConfig.SigningKey) < 32)
        {
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes.");
        }
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = authConfig.Issuer,
            ValidAudience = authConfig.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authConfig.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("ui");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/auth"))
    {
        await next();
        return;
    }

    if (context.Request.ContentLength is > 0)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        if (!string.IsNullOrWhiteSpace(body))
        {
            app.Logger.LogInformation(
                "Request body {method} {path}: {body}",
                context.Request.Method,
                context.Request.Path,
                body);
        }
    }

    await next();
});

app.MapGet("/", () => "Tracker API");

app.MapIngestEndpoints();
app.MapAuthEndpoints();
app.MapCompaniesEndpoints();
app.MapDevicesEndpoints();
app.MapHealthEndpoints();

await app.RunAsync();
