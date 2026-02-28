using System.Diagnostics;
namespace Tracker.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var request = context.Request;
        var route = request.Path.Value ?? string.Empty;
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;

        _logger.LogInformation(
            "Request started. CorrelationId={correlationId} Method={method} Route={route} Query={query} ContentType={contentType} ContentLength={contentLength}",
            correlationId,
            request.Method,
            route,
            request.QueryString.Value ?? string.Empty,
            request.ContentType ?? "(none)",
            request.ContentLength?.ToString() ?? "(none)");

        await _next(context);

        sw.Stop();
        correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;
        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            _logger.LogWarning(
                "Request failed. CorrelationId={correlationId} Method={method} Route={route} Status={status} DurationMs={durationMs}",
                correlationId,
                request.Method,
                route,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogInformation(
                "Request completed. CorrelationId={correlationId} Method={method} Route={route} Status={status} DurationMs={durationMs}",
                correlationId,
                request.Method,
                route,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
    }
}
