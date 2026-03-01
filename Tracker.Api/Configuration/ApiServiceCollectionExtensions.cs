using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Tracker.Api.Data;
using Tracker.Api.Infrastructure;
using Tracker.Api.Security;
using Tracker.Api.Services;

namespace Tracker.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static void AddTrackerApiServices(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
        });

        builder.Services.AddDbContext<TrackerDbContext>(o =>
        {
            o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
            if (builder.Environment.IsDevelopment())
            {
                o.EnableSensitiveDataLogging();
            }
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("ui", policy =>
            {
                var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
                var origins = configuredOrigins
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Select(origin => origin.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (origins.Length == 0)
                {
                    throw new InvalidOperationException("Cors:AllowedOrigins must include at least one trusted origin.");
                }

                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
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
    }
}
