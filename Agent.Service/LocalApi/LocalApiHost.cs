using Agent.Service.Infrastructure;
using Agent.Service.Infrastructure.Outbox;
using Agent.Service.Tracking;
using Agent.Shared.LocalApi;
using Agent.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Agent.Service.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Agent.Service.LocalApi;

public sealed class LocalApiHost
{
    public const int DefaultPort = 43121;

    private readonly WebEventQueue _queue;
    private readonly string _token;
    private readonly OutboxRepository? _outboxRepo;
    private readonly OutboxSenderState? _outboxState;
    private readonly string? _deviceId;
    private readonly string? _agentVersion;
    private readonly AppSessionizer _appSessionizer;
    private readonly IdleSessionizer _idleSessionizer;
    private readonly WebSessionizer _webSessionizer;
    private readonly string[] _allowedOrigins;
    private readonly object _pipStateSync = new();
    private LocalPipStateResponse _pipState = new(false, false, null, null, DateTimeOffset.UtcNow);
    private IHost? _host;

    public int Port { get; set; } = DefaultPort;

    public LocalApiHost(
        WebEventQueue queue,
        string token,
        AppSessionizer appSessionizer,
        IdleSessionizer idleSessionizer,
        WebSessionizer webSessionizer,
        OutboxRepository? outboxRepo = null,
        OutboxSenderState? outboxState = null,
        string? deviceId = null,
        string? agentVersion = null,
        IEnumerable<string>? allowedOrigins = null)
    {
        _queue = queue;
        _token = token;
        _outboxRepo = outboxRepo;
        _outboxState = outboxState;
        _deviceId = deviceId;
        _agentVersion = agentVersion;
        _appSessionizer = appSessionizer;
        _idleSessionizer = idleSessionizer;
        _webSessionizer = webSessionizer;
        _allowedOrigins = (allowedOrigins ?? Array.Empty<string>())
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        
        // Bind to Configured Port
        builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");

        builder.Services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        if (_outboxRepo != null)
        {
            builder.Services.AddSingleton(_outboxRepo);
        }

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var requestOrigin = context.Request.Headers.Origin.ToString();
            if (string.IsNullOrWhiteSpace(requestOrigin) || IsOriginAllowed(requestOrigin))
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = string.IsNullOrWhiteSpace(requestOrigin)
                    ? "null"
                    : requestOrigin;
                context.Response.Headers["Vary"] = "Origin";
            }

            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Agent-Token";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";

            if (HttpMethods.Options.Equals(context.Request.Method, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await next();
        });
        app.UseMiddleware<AgentTokenAuthMiddleware>(_token);

        app.MapGet("/health", () => Results.Ok(new
        {
            ok = true,
            lastFlushAtUtc = _outboxState?.LastFlushAtUtc,
            deviceId = _deviceId,
            agentVersion = _agentVersion
        }));

        app.MapGet("/local/outbox/stats", async (OutboxRepository? repo) =>
        {
             if (repo == null) return Results.Problem("Outbox not available");
             var stats = await repo.GetStatsAsync();
             return Results.Ok(stats);
        });

        app.MapGet("/diag", async (OutboxRepository? repo) =>
        {
            object? stats = null;
            if (repo != null)
            {
                stats = await repo.GetStatsAsync();
            }
            return Results.Ok(new
            {
                contract = LocalApiConstants.Contract,
                deviceId = _deviceId,
                agentVersion = _agentVersion,
                port = Port,
                serverTimeUtc = DateTimeOffset.UtcNow,
                outbox = new
                {
                    pendingByType = stats,
                    lastFlushAtUtc = _outboxState?.LastFlushAtUtc,
                    available = repo != null
                }
            });
        });

        app.MapGet("/version", () => Results.Ok(new
        {
            contract = LocalApiConstants.Contract,
            deviceId = _deviceId,
            agentVersion = _agentVersion,
            port = Port,
            routes = new[]
            {
                "GET /health",
                "GET /diag",
                "GET /version",
                "GET /local/outbox/stats",
                "POST /events/app-focus",
                "POST /events/idle",
                "POST /events/web",
                "POST /events/web-session (alias)",
                "POST /events/activity-sample",
                "POST /events/screenshot",
                "GET /state/pip"
            },
            auth = new { header = AgentTokenAuthMiddleware.HeaderName },
            serverTimeUtc = DateTimeOffset.UtcNow
        }));
        
        app.MapGet("/state/pip", () =>
        {
            lock (_pipStateSync)
            {
                return Results.Ok(_pipState);
            }
        });

        app.MapPost("/events/web", (WebEvent evt, CancellationToken ct) =>
            HandleWebEventAsync(evt, app.Logger, ct));
        app.MapPost("/events/web-session", (WebEvent evt, CancellationToken ct) =>
            HandleWebEventAsync(evt, app.Logger, ct));

        app.MapPost("/events/app-focus", async (AppFocusRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.AppName))
            {
                return Results.BadRequest("appName is required.");
            }

            var timestamp = req.TimestampUtc == default ? DateTimeOffset.UtcNow : req.TimestampUtc;
            await _appSessionizer.HandleAppFocusAsync(req.AppName, timestamp, req.WindowTitle);
            await _webSessionizer.HandleAppFocusAsync(req.AppName, timestamp);
            return Results.Ok(new { received = true });
        });

        app.MapPost("/events/idle", async (IdleSampleRequest req) =>
        {
            if (req.IdleSeconds < 0)
            {
                return Results.BadRequest("idleSeconds must be >= 0.");
            }

            var timestamp = req.TimestampUtc == default ? DateTimeOffset.UtcNow : req.TimestampUtc;
            var idleDuration = TimeSpan.FromSeconds(req.IdleSeconds);
            await _idleSessionizer.HandleIdleStateAsync(idleDuration, timestamp);
            await _webSessionizer.HandleIdleStateAsync(idleDuration, timestamp);
            return Results.Ok(new { received = true });
        });

        app.MapPost("/events/activity-sample", async (ActivitySampleRequest req, CancellationToken ct) =>
        {
            if (_outboxRepo is null)
            {
                return Results.Problem("Outbox unavailable.");
            }

            if (string.IsNullOrWhiteSpace(req.ProcessName) || string.IsNullOrWhiteSpace(req.MonitorId))
            {
                return Results.BadRequest("processName and monitorId are required.");
            }

            var timestamp = req.TimestampUtc == default ? DateTimeOffset.UtcNow : req.TimestampUtc;
            var dto = new MonitorSessionDto(
                Guid.NewGuid(),
                timestamp,
                req.MonitorId,
                Math.Max(1, req.MonitorWidth),
                Math.Max(1, req.MonitorHeight),
                req.ProcessName,
                req.WindowTitle,
                req.WindowBounds.X,
                req.WindowBounds.Y,
                req.WindowBounds.Width,
                req.WindowBounds.Height,
                req.IsSplitScreen,
                req.IsPiPActive,
                req.AttentionScore);

            await _outboxRepo.EnqueueAsync("monitor_session", dto);
            return Results.Ok(new { received = true });
        });

        app.MapPost("/events/screenshot", async (ScreenshotEventRequest req) =>
        {
            if (_outboxRepo is null)
            {
                return Results.Problem("Outbox unavailable.");
            }

            if (string.IsNullOrWhiteSpace(req.MonitorId)
                || string.IsNullOrWhiteSpace(req.FilePath)
                || string.IsNullOrWhiteSpace(req.TriggerReason))
            {
                return Results.BadRequest("monitorId, filePath and triggerReason are required.");
            }

            var timestamp = req.TimestampUtc == default ? DateTimeOffset.UtcNow : req.TimestampUtc;
            var dto = new ScreenshotDto(Guid.NewGuid(), timestamp, req.MonitorId, req.FilePath, req.TriggerReason);
            await _outboxRepo.EnqueueAsync("screenshot", dto);
            return Results.Ok(new { received = true });
        });

        _host = app;
        await app.StartAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_host is null) return;
        await _host.StopAsync(ct);
        _host.Dispose();
        _host = null;
    }

    private async Task<IResult> HandleWebEventAsync(WebEvent evt, ILogger logger, CancellationToken ct)
    {
        if (evt.EventId == Guid.Empty || string.IsNullOrWhiteSpace(evt.Domain))
        {
            logger.LogWarning(
                "Web event rejected. DeviceId={deviceId} EventId={eventId} Domain={domain}",
                _deviceId,
                evt.EventId,
                evt.Domain);
            return Results.BadRequest("eventId and domain are required.");
        }

        logger.LogDebug(
            "Web event received. DeviceId={deviceId} Domain={domain} Browser={browser} Timestamp={timestamp}",
            _deviceId,
            evt.Domain,
            evt.Browser,
            evt.Timestamp);

        lock (_pipStateSync)
        {
            _pipState = new LocalPipStateResponse(
                evt.PipActive == true,
                evt.VideoPlaying == true,
                evt.VideoUrl,
                evt.VideoDomain,
                evt.Timestamp);
        }

        await _queue.Channel.Writer.WriteAsync(evt, ct);
        return Results.Ok(new { received = true });
    }

    private bool IsOriginAllowed(string origin)
    {
        if (_allowedOrigins.Length == 0)
        {
            return false;
        }

        foreach (var allowed in _allowedOrigins)
        {
            if (allowed.EndsWith('*'))
            {
                var prefix = allowed[..^1];
                if (origin.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record AppFocusRequest(string AppName, string? WindowTitle, DateTimeOffset TimestampUtc);
    private sealed record IdleSampleRequest(double IdleSeconds, DateTimeOffset TimestampUtc);
}
