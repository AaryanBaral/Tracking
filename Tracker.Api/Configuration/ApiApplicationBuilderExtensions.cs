using Microsoft.EntityFrameworkCore;
using Tracker.Api.Middleware;

namespace Tracker.Api.Configuration;

public static class ApiApplicationBuilderExtensions
{
    public static async Task EnsureDatabaseConnectivityAsync(this WebApplication app)
    {
        await using var startupScope = app.Services.CreateAsyncScope();
        var db = startupScope.ServiceProvider.GetRequiredService<Tracker.Api.Data.TrackerDbContext>();
        try
        {
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

    public static void UseTrackerApiPipeline(this WebApplication app)
    {
        app.UseCors("ui");
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

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
    }
}
