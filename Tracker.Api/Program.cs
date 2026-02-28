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

// Fail fast at boot if the primary database is unreachable.
await using (var startupScope = app.Services.CreateAsyncScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<TrackerDbContext>();
    try
    {
        // Force an actual open to get a concrete provider/network/auth failure reason.
        await db.Database.OpenConnectionAsync();

        var conn = db.Database.GetDbConnection();
        app.Logger.LogInformation(
            "Database connectivity check passed. DataSource={dataSource} Database={database}",
            conn.DataSource,
            conn.Database);

        await db.Database.CloseConnectionAsync();
    }
    catch (Exception ex)
    {
        var reason = ex.GetBaseException().Message;
        app.Logger.LogCritical(
            ex,
            "Database connectivity check failed at startup. Reason={reason}. Aborting boot.",
            reason);
        throw new InvalidOperationException($"Database connectivity check failed: {reason}", ex);
    }
}

app.UseCors("ui");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var ex = feature?.Error;
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;

        logger.LogError(
            ex,
            "Unhandled exception. CorrelationId={correlationId} Route={route} Method={method}",
            correlationId,
            feature?.Path ?? context.Request.Path.Value ?? "(unknown)",
            context.Request.Method);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "internal_server_error",
            correlationId
        });
    });
});
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Tracker API");

app.MapIngestEndpoints();
app.MapAuthEndpoints();
app.MapCompaniesEndpoints();
app.MapDevicesEndpoints();
app.MapHealthEndpoints();

try
{
    app.Logger.LogInformation("Tracker API starting.");
    await app.RunAsync();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Tracker API terminated unexpectedly.");
    throw;
}
