using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Contracts;
using Tracker.Api.Data;
using Tracker.Api.Infrastructure;
using Tracker.Api.Models;
using Tracker.Api.Services;
using Tracker.Api.Services.Models;

namespace Tracker.Api.Endpoints;

public static partial class IngestEndpoints
{
    public static IEndpointRouteBuilder MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ingest/web-sessions", IngestWebSessionsAsync);
        app.MapPost("/ingest/app-sessions", IngestAppSessionsAsync);
        app.MapPost("/ingest/idle-sessions", IngestIdleSessionsAsync);
        app.MapPost("/ingest/device-sessions", IngestDeviceSessionsAsync);
        app.MapPost("/ingest/monitor-sessions", IngestMonitorSessionsAsync);
        app.MapPost("/ingest/screenshots", IngestScreenshotsAsync);
        app.MapPost("/events/web", IngestWebEventAsync);
        app.MapPost("/events/web/batch", IngestWebEventBatchAsync);
        return app;
    }

    private static async Task<IResult> IngestWebSessionsAsync(
        WebSessionIngestRequest req,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var deviceId = req.DeviceId?.Trim();
        var agentVersion = req.AgentVersion?.Trim();
        if (req.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (req.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }

        if (req.Sessions is null || req.Sessions.Count == 0)
        {
            return Results.BadRequest("sessions are required.");
        }
        if (req.Sessions.Count > IngestLimits.MaxSessionsPerRequest)
        {
            return Results.BadRequest($"sessions exceeds max batch size of {IngestLimits.MaxSessionsPerRequest}.");
        }

        var now = DateTimeOffset.UtcNow;
        if (req.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }

        var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
        if (company is null)
        {
            return Results.Unauthorized();
        }

        logger.LogInformation(
            "Web ingest started for device {deviceId} with {count} sessions. BatchId={batchId} Sequence={sequence}.",
            deviceId,
            req.Sessions.Count,
            req.BatchId,
            req.Sequence);

        try
        {
            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;


            var rows = new List<WebSessionRow>(req.Sessions.Count);
            var skipped = 0;
            var maxFuture = now.AddDays(1);
            foreach (var s in req.Sessions)
            {
                if (s.SessionId == Guid.Empty)
                {
                    skipped++;
                    logger.LogWarning("Skipping web session with empty sessionId for {deviceId}.", deviceId);
                    continue;
                }

                if (s.EndAt <= s.StartAt || s.StartAt > maxFuture || s.EndAt > maxFuture)
                {
                    skipped++;
                    logger.LogWarning("Skipping invalid web session for {deviceId}.", deviceId);
                    continue;
                }

                if (!TryNormalizeRequired(s.Domain, 255, out var domain))
                {
                    skipped++;
                    logger.LogWarning("Skipping web session with empty domain for {deviceId}.", deviceId);
                    continue;
                }
                var title = NormalizeOptional(s.Title, 512);
                var url = NormalizeOptional(s.Url, 2048);

                rows.Add(new WebSessionRow(
                    s.SessionId,
                    deviceId,
                    domain,
                    title,
                    url,
                    s.StartAt,
                    s.EndAt));
            }

            if (rows.Count == 0)
            {
                logger.LogInformation("No valid web sessions to insert for {deviceId}.", deviceId);
                return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, 0, 0));
            }

            var inserted = await ingestService.InsertWebSessionsAsync(rows, ct);
            var duplicates = rows.Count - inserted;
            device.LastSeenAt = DateTimeOffset.UtcNow;



            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Inserted {count} web sessions for {deviceId}. Skipped={skipped}. Duplicates={duplicates}. DurationMs={durationMs}.",
                inserted,
                deviceId,
                skipped,
                duplicates,
                sw.ElapsedMilliseconds);
            return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest web sessions for {deviceId}.", deviceId);
            return Results.Problem("Failed to ingest web sessions.");
        }
    }

    private static async Task<IResult> IngestWebEventAsync(
        WebEventIngestRequest req,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (req.EventId == Guid.Empty)
        {
            return Results.BadRequest("eventId is required.");
        }

        var deviceId = req.DeviceId?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        var agentVersion = req.AgentVersion?.Trim();
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }
        if (req.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (req.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }

        if (!TryNormalizeRequired(req.Domain, 255, out var domain))
        {
            return Results.BadRequest("domain is required.");
        }

        if (req.Timestamp > now.AddDays(2))
        {
            return Results.BadRequest("timestamp is too far in the future.");
        }
        if (req.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }

        if (deviceId.Length > 64)
        {
            return Results.BadRequest("deviceId is too long.");
        }

        var title = NormalizeOptional(req.Title, 512);
        var url = NormalizeOptional(req.Url, 2048);
        var browser = NormalizeOptional(req.Browser, 64);
        var videoUrl = NormalizeOptional(req.VideoUrl, 2048);
        var videoDomain = NormalizeOptional(req.VideoDomain, 255);

        try
        {
            var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
            if (company is null)
            {
                return Results.Unauthorized();
            }

            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;

            var row = new WebEventRow(
                req.EventId,
                deviceId,
                domain,
                title,
                url,
                req.Timestamp,
                browser,
                req.PipActive,
                req.VideoPlaying,
                videoUrl,
                videoDomain,
                req.TabId,
                now);

            var inserted = await ingestService.InsertWebEventsAsync(new[] { row }, ct);
            var duplicates = inserted == 0 ? 1 : 0;
            device.LastSeenAt = now;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Web event ingest for {deviceId}. Inserted={inserted}.",
                deviceId,
                inserted);

            return Results.Ok(new IngestResponse(1, 0, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest web event for {deviceId}.", deviceId);
            return Results.Problem("Failed to ingest web event.");
        }
    }

    private static async Task<IResult> IngestWebEventBatchAsync(
        WebEventBatchRequest request,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (request.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (request.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }

        var deviceId = request.DeviceId?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        if (deviceId.Length > 64)
        {
            return Results.BadRequest("deviceId is too long.");
        }

        var agentVersion = request.AgentVersion?.Trim();
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }

        if (request.Events is null || request.Events.Count == 0)
        {
            return Results.BadRequest("events are required.");
        }

        if (request.Events.Count > IngestLimits.MaxWebEventsPerRequest)
        {
            return Results.BadRequest($"events exceeds max batch size of {IngestLimits.MaxWebEventsPerRequest}.");
        }

        var now = DateTimeOffset.UtcNow;
        if (request.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }
        var maxFuture = now.AddDays(2);
        var rows = new List<WebEventRow>(request.Events.Count);
        var invalid = 0;

        foreach (var evt in request.Events)
        {
            if (evt.EventId == Guid.Empty)
            {
                invalid++;
                continue;
            }

            if (!TryNormalizeRequired(evt.Domain, 255, out var domain))
            {
                invalid++;
                continue;
            }

            if (evt.Timestamp > maxFuture)
            {
                invalid++;
                continue;
            }

            var title = NormalizeOptional(evt.Title, 512);
            var url = NormalizeOptional(evt.Url, 2048);
            var browser = NormalizeOptional(evt.Browser, 64);
            var videoUrl = NormalizeOptional(evt.VideoUrl, 2048);
            var videoDomain = NormalizeOptional(evt.VideoDomain, 255);

            rows.Add(new WebEventRow(
                evt.EventId,
                deviceId,
                domain,
                title,
                url,
                evt.Timestamp,
                browser,
                evt.PipActive,
                evt.VideoPlaying,
                videoUrl,
                videoDomain,
                evt.TabId,
                now));
        }

        if (rows.Count == 0)
        {
            return Results.Ok(new IngestResponse(request.Events.Count, invalid, 0, 0));
        }

        try
        {
            var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
            if (company is null)
            {
                return Results.Unauthorized();
            }

            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;

            var inserted = await ingestService.InsertWebEventsAsync(rows, ct);
            var duplicates = rows.Count - inserted;
            device.LastSeenAt = now;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Web event batch ingest completed. Received={received} Inserted={inserted} Invalid={invalid} Duplicates={duplicates}.",
                request.Events.Count,
                inserted,
                invalid,
                duplicates);

            return Results.Ok(new IngestResponse(request.Events.Count, invalid, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest web event batch.");
            return Results.Problem("Failed to ingest web events.");
        }
    }

    private static async Task<IResult> IngestAppSessionsAsync(
        AppSessionIngestRequest req,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var deviceId = req.DeviceId?.Trim();
        var agentVersion = req.AgentVersion?.Trim();
        if (req.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (req.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }

        if (req.Sessions is null || req.Sessions.Count == 0)
        {
            return Results.BadRequest("sessions are required.");
        }

        var now = DateTimeOffset.UtcNow;
        if (req.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }

        var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
        if (company is null)
        {
            return Results.Unauthorized();
        }

        logger.LogInformation(
            "App ingest started for device {deviceId} with {count} sessions. BatchId={batchId} Sequence={sequence}.",
            deviceId,
            req.Sessions.Count,
            req.BatchId,
            req.Sequence);

        try
        {
            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;


            var rows = new List<AppSessionRow>(req.Sessions.Count);
            var skipped = 0;
            var maxFuture = now.AddDays(1);
            foreach (var s in req.Sessions)
            {
                if (s.SessionId == Guid.Empty)
                {
                    skipped++;
                    logger.LogWarning("Skipping app session with empty sessionId for {deviceId}.", deviceId);
                    continue;
                }

                if (s.EndAt <= s.StartAt || s.StartAt > maxFuture || s.EndAt > maxFuture)
                {
                    skipped++;
                    logger.LogWarning("Skipping invalid app session for {deviceId}.", deviceId);
                    continue;
                }

                if (!TryNormalizeRequired(s.ProcessName, 255, out var processName))
                {
                    skipped++;
                    logger.LogWarning("Skipping app session with empty process for {deviceId}.", deviceId);
                    continue;
                }
                var windowTitle = NormalizeOptional(s.WindowTitle, 512);

                rows.Add(new AppSessionRow(
                    s.SessionId,
                    deviceId,
                    processName,
                    windowTitle,
                    s.StartAt,
                    s.EndAt));
            }

            if (rows.Count == 0)
            {
                logger.LogInformation("No valid app sessions to insert for {deviceId}.", deviceId);
                return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, 0, 0));
            }

            var inserted = await ingestService.InsertAppSessionsAsync(rows, ct);
            var duplicates = rows.Count - inserted;
            device.LastSeenAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Inserted {count} app sessions for {deviceId}. Skipped={skipped}. Duplicates={duplicates}. DurationMs={durationMs}.",
                inserted,
                deviceId,
                skipped,
                duplicates,
                sw.ElapsedMilliseconds);
            return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest app sessions for {deviceId}.", deviceId);
            return Results.Problem("Failed to ingest app sessions.");
        }
    }

    private static async Task<IResult> IngestIdleSessionsAsync(
        IdleSessionIngestRequest req,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var deviceId = req.DeviceId?.Trim();
        var agentVersion = req.AgentVersion?.Trim();
        if (req.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (req.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }

        if (req.Sessions is null || req.Sessions.Count == 0)
        {
            return Results.BadRequest("sessions are required.");
        }

        var now = DateTimeOffset.UtcNow;
        if (req.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }

        var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
        if (company is null)
        {
            return Results.Unauthorized();
        }

        logger.LogInformation(
            "Idle ingest started for device {deviceId} with {count} sessions. BatchId={batchId} Sequence={sequence}.",
            deviceId,
            req.Sessions.Count,
            req.BatchId,
            req.Sequence);

        try
        {
            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;


            var rows = new List<IdleSessionRow>(req.Sessions.Count);
            var skipped = 0;
            var maxFuture = now.AddDays(1);
            foreach (var s in req.Sessions)
            {
                if (s.SessionId == Guid.Empty)
                {
                    skipped++;
                    logger.LogWarning("Skipping idle session with empty sessionId for {deviceId}.", deviceId);
                    continue;
                }

                if (s.EndAt <= s.StartAt || s.StartAt > maxFuture || s.EndAt > maxFuture)
                {
                    skipped++;
                    logger.LogWarning("Skipping invalid idle session for {deviceId}.", deviceId);
                    continue;
                }

                rows.Add(new IdleSessionRow(
                    s.SessionId,
                    deviceId,
                    s.StartAt,
                    s.EndAt));
            }

            if (rows.Count == 0)
            {
                logger.LogInformation("No valid idle sessions to insert for {deviceId}.", deviceId);
                return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, 0, 0));
            }

            var inserted = await ingestService.InsertIdleSessionsAsync(rows, ct);
            var duplicates = rows.Count - inserted;
            device.LastSeenAt = DateTimeOffset.UtcNow;


            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Inserted {count} idle sessions for {deviceId}. Skipped={skipped}. Duplicates={duplicates}. DurationMs={durationMs}.",
                inserted,
                deviceId,
                skipped,
                duplicates,
                sw.ElapsedMilliseconds);
            return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest idle sessions for {deviceId}.", deviceId);
            return Results.Problem("Failed to ingest idle sessions.");
        }
    }

    private static async Task<IResult> IngestDeviceSessionsAsync(
        DeviceSessionIngestRequest req,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var deviceId = req.DeviceId?.Trim();
        var agentVersion = req.AgentVersion?.Trim();
        if (req.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (req.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }

        if (req.Sessions is null || req.Sessions.Count == 0)
        {
            return Results.BadRequest("sessions are required.");
        }

        var now = DateTimeOffset.UtcNow;
        if (req.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }

        var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
        if (company is null)
        {
            return Results.Unauthorized();
        }

        logger.LogInformation(
            "Device ingest started for device {deviceId} with {count} sessions. BatchId={batchId} Sequence={sequence}.",
            deviceId,
            req.Sessions.Count,
            req.BatchId,
            req.Sequence);

        try
        {
            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;


            var rows = new List<DeviceSessionRow>(req.Sessions.Count);
            var skipped = 0;
            var maxFuture = now.AddDays(1);
            foreach (var s in req.Sessions)
            {
                if (s.SessionId == Guid.Empty)
                {
                    skipped++;
                    logger.LogWarning("Skipping device session with empty sessionId for {deviceId}.", deviceId);
                    continue;
                }

                if (s.EndAt <= s.StartAt || s.StartAt > maxFuture || s.EndAt > maxFuture)
                {
                    skipped++;
                    logger.LogWarning("Skipping invalid device session for {deviceId}.", deviceId);
                    continue;
                }

                rows.Add(new DeviceSessionRow(
                    s.SessionId,
                    deviceId,
                    s.StartAt,
                    s.EndAt));
            }

            if (rows.Count == 0)
            {
                logger.LogInformation("No valid device sessions to insert for {deviceId}.", deviceId);
                return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, 0, 0));
            }

            var inserted = await ingestService.InsertDeviceSessionsAsync(rows, ct);
            var duplicates = rows.Count - inserted;
            device.LastSeenAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Inserted {count} device sessions for {deviceId}. Skipped={skipped}. Duplicates={duplicates}. DurationMs={durationMs}.",
                inserted,
                deviceId,
                skipped,
                duplicates,
                sw.ElapsedMilliseconds);
            return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest device sessions for {deviceId}.", deviceId);
            return Results.Problem("Failed to ingest device sessions.");
        }
    }

    private static async Task<IResult> IngestMonitorSessionsAsync(
        MonitorSessionIngestRequest req,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var deviceId = req.DeviceId?.Trim();
        var agentVersion = req.AgentVersion?.Trim();
        if (req.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (req.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }
        if (req.Sessions is null || req.Sessions.Count == 0)
        {
            return Results.BadRequest("sessions are required.");
        }
        if (req.Sessions.Count > IngestLimits.MaxSessionsPerRequest)
        {
            return Results.BadRequest($"sessions exceeds max batch size of {IngestLimits.MaxSessionsPerRequest}.");
        }

        var now = DateTimeOffset.UtcNow;
        if (req.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }

        var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
        if (company is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;

            var rows = new List<MonitorSessionRow>(req.Sessions.Count);
            var skipped = 0;
            foreach (var s in req.Sessions)
            {
                if (s.SessionId == Guid.Empty)
                {
                    skipped++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(s.MonitorId) || string.IsNullOrWhiteSpace(s.ActiveWindowProcess))
                {
                    skipped++;
                    continue;
                }
                if (s.ResolutionWidth <= 0 || s.ResolutionHeight <= 0 || s.WindowWidth < 0 || s.WindowHeight < 0)
                {
                    skipped++;
                    continue;
                }

                if (!TryNormalizeRequired(s.MonitorId, 128, out var monitorId))
                {
                    skipped++;
                    continue;
                }

                if (!TryNormalizeRequired(s.ActiveWindowProcess, 255, out var process))
                {
                    skipped++;
                    continue;
                }

                var title = NormalizeOptional(s.ActiveWindowTitle, 512);

                rows.Add(new MonitorSessionRow(
                    s.SessionId,
                    deviceId,
                    s.Timestamp,
                    monitorId,
                    s.ResolutionWidth,
                    s.ResolutionHeight,
                    process,
                    title,
                    s.WindowX,
                    s.WindowY,
                    s.WindowWidth,
                    s.WindowHeight,
                    s.IsSplitScreen,
                    s.IsPiPActive,
                    Math.Clamp(s.AttentionScore, 0, 100)));
            }

            if (rows.Count == 0)
            {
                return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, 0, 0));
            }

            var inserted = await ingestService.InsertMonitorSessionsAsync(rows, ct);
            var duplicates = rows.Count - inserted;
            device.LastSeenAt = now;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new IngestResponse(req.Sessions.Count, skipped, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest monitor sessions for {deviceId}.", deviceId);
            return Results.Problem("Failed to ingest monitor sessions.");
        }
    }

    private static async Task<IResult> IngestScreenshotsAsync(
        ScreenshotIngestRequest req,
        HttpRequest httpRequest,
        TrackerDbContext db,
        IIngestService ingestService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var deviceId = req.DeviceId?.Trim();
        var agentVersion = req.AgentVersion?.Trim();
        if (req.BatchId == Guid.Empty)
        {
            return Results.BadRequest("batchId is required.");
        }
        if (req.Sequence < 0)
        {
            return Results.BadRequest("sequence must be >= 0.");
        }
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest("deviceId is required.");
        }
        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            return Results.BadRequest("agentVersion is required.");
        }
        if (agentVersion.Length > 64)
        {
            return Results.BadRequest("agentVersion is too long.");
        }
        if (req.Screenshots is null || req.Screenshots.Count == 0)
        {
            return Results.BadRequest("screenshots are required.");
        }
        if (req.Screenshots.Count > IngestLimits.MaxSessionsPerRequest)
        {
            return Results.BadRequest($"screenshots exceeds max batch size of {IngestLimits.MaxSessionsPerRequest}.");
        }

        var now = DateTimeOffset.UtcNow;
        if (req.SentAt > now.AddDays(1))
        {
            return Results.BadRequest("sentAt is too far in the future.");
        }

        var company = await EnrollmentKeyHelper.ResolveCompanyAsync(httpRequest, db, ct);
        if (company is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var deviceResult = await EnsureDeviceAsync(db, deviceId, company.Id, now, ct);
            if (deviceResult.Error is not null)
            {
                return deviceResult.Error;
            }
            var device = deviceResult.Device!;

            var rows = new List<ScreenshotRow>(req.Screenshots.Count);
            var skipped = 0;
            foreach (var s in req.Screenshots)
            {
                if (s.ScreenshotId == Guid.Empty || string.IsNullOrWhiteSpace(s.MonitorId) || string.IsNullOrWhiteSpace(s.FilePath))
                {
                    skipped++;
                    continue;
                }

                if (!TryNormalizeRequired(s.MonitorId, 128, out var monitorId))
                {
                    skipped++;
                    continue;
                }

                if (!TryNormalizeRequired(s.FilePath, 2048, out var filePath))
                {
                    skipped++;
                    continue;
                }

                var triggerReason = NormalizeOptional(s.TriggerReason, 128) ?? "unknown";

                rows.Add(new ScreenshotRow(
                    s.ScreenshotId,
                    deviceId,
                    s.Timestamp,
                    monitorId,
                    filePath,
                    triggerReason));
            }

            if (rows.Count == 0)
            {
                return Results.Ok(new IngestResponse(req.Screenshots.Count, skipped, 0, 0));
            }

            var inserted = await ingestService.InsertScreenshotsAsync(rows, ct);
            var duplicates = rows.Count - inserted;
            device.LastSeenAt = now;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new IngestResponse(req.Screenshots.Count, skipped, inserted, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest screenshots for {deviceId}.", deviceId);
            return Results.Problem("Failed to ingest screenshots.");
        }
    }

    private static async Task<(Device? Device, IResult? Error)> EnsureDeviceAsync(
        TrackerDbContext db,
        string deviceId,
        Guid companyId,
        DateTimeOffset lastSeenAt,
        CancellationToken ct)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null)
        {
            device = new Device
            {
                Id = deviceId,
                CompanyId = companyId,
                LastSeenAt = lastSeenAt
            };
            db.Devices.Add(device);
            await db.SaveChangesAsync(ct);
            return (device, null);
        }

        if (device.CompanyId.HasValue && device.CompanyId != companyId)
        {
            return (null, Results.Forbid());
        }

        device.CompanyId ??= companyId;
        return (device, null);
    }
}
