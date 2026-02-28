using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tracker.Api.Data;
using Tracker.Api.Infrastructure;
using Tracker.Api.Models;
using Tracker.Api.Services.Models;

namespace Tracker.Api.Services;

public sealed class IngestService : IIngestService
{
    private readonly TrackerDbContext _db;
    private readonly ILogger<IngestService> _logger;

    public IngestService(TrackerDbContext db, ILogger<IngestService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> InsertWebEventsAsync(IReadOnlyList<WebEventRow> rows, CancellationToken ct)
    {
        _logger.LogInformation("InsertWebEventsAsync called with {count} rows", rows.Count);
        
        if (rows.Count == 0)
        {
            _logger.LogWarning("No WebEvents to insert");
            return 0;
        }

        // Log first event for debugging
        var firstRow = rows[0];
        _logger.LogInformation(
            "First WebEvent: EventId={eventId}, DeviceId={deviceId}, Domain={domain}, Timestamp={timestamp}",
            firstRow.EventId,
            firstRow.DeviceId,
            firstRow.Domain,
            firstRow.Timestamp);

        var values = rows.Select(r => new object?[]
        {
            Guid.NewGuid(),
            r.EventId,
            r.DeviceId,
            r.Domain,
            r.Title,
            r.Url,
            r.Timestamp,
            r.Browser,
            r.ReceivedAt
        }).ToList();

        var inserted = await InsertRowsAsync(
            "WebEvents",
            new[]
            {
                "Id",
                "EventId",
                "DeviceId",
                "Domain",
                "Title",
                "Url",
                "Timestamp",
                "Browser",
                "ReceivedAt"
            },
            values,
            "EventId",
            ct);

        _logger.LogInformation(
            "WebEvents insert complete: Attempted={attempted}, Inserted={inserted}, Skipped={skipped}",
            rows.Count,
            inserted,
            rows.Count - inserted);

        return inserted;
    }

    public async Task<int> InsertWebSessionsAsync(IReadOnlyList<WebSessionRow> rows, CancellationToken ct)
    {
        _logger.LogInformation("InsertWebSessionsAsync called with {count} rows", rows.Count);
        
        if (rows.Count == 0)
        {
            _logger.LogWarning("No WebSessions to insert");
            return 0;
        }

        // Log first session for debugging
        var firstRow = rows[0];
        _logger.LogInformation(
            "First WebSession: SessionId={sessionId}, DeviceId={deviceId}, Domain={domain}",
            firstRow.SessionId,
            firstRow.DeviceId,
            firstRow.Domain);

        var values = rows.Select(r => new object?[]
        {
            Guid.NewGuid(),
            r.SessionId,
            r.DeviceId,
            r.Domain,
            r.Title,
            r.Url,
            r.StartAt,
            r.EndAt
        }).ToList();

        var inserted = await InsertRowsAsync(
            "WebSessions",
            new[] { "Id", "SessionId", "DeviceId", "Domain", "Title", "Url", "StartAt", "EndAt" },
            values,
            "SessionId",
            ct);

        _logger.LogInformation(
            "WebSessions insert complete: Attempted={attempted}, Inserted={inserted}, Skipped={skipped}",
            rows.Count,
            inserted,
            rows.Count - inserted);

        return inserted;
    }

    public async Task<int> InsertAppSessionsAsync(IReadOnlyList<AppSessionRow> rows, CancellationToken ct)
    {
        _logger.LogInformation("InsertAppSessionsAsync called with {count} rows", rows.Count);
        
        if (rows.Count == 0)
        {
            _logger.LogWarning("No AppSessions to insert");
            return 0;
        }

        // Log first session for debugging
        var firstRow = rows[0];
        _logger.LogInformation(
            "First AppSession: SessionId={sessionId}, DeviceId={deviceId}, ProcessName={processName}",
            firstRow.SessionId,
            firstRow.DeviceId,
            firstRow.ProcessName);

        var values = rows.Select(r => new object?[]
        {
            Guid.NewGuid(),
            r.SessionId,
            r.DeviceId,
            r.ProcessName,
            r.WindowTitle,
            r.StartAt,
            r.EndAt
        }).ToList();

        var inserted = await InsertRowsAsync(
            "AppSessions",
            new[] { "Id", "SessionId", "DeviceId", "ProcessName", "WindowTitle", "StartAt", "EndAt" },
            values,
            "SessionId",
            ct);

        _logger.LogInformation(
            "AppSessions insert complete: Attempted={attempted}, Inserted={inserted}, Skipped={skipped}",
            rows.Count,
            inserted,
            rows.Count - inserted);

        return inserted;
    }

    public async Task<int> InsertIdleSessionsAsync(IReadOnlyList<IdleSessionRow> rows, CancellationToken ct)
    {
        _logger.LogInformation("InsertIdleSessionsAsync called with {count} rows", rows.Count);
        
        if (rows.Count == 0)
        {
            _logger.LogWarning("No IdleSessions to insert");
            return 0;
        }

        var values = rows.Select(r => new object?[]
        {
            Guid.NewGuid(),
            r.SessionId,
            r.DeviceId,
            r.StartAt,
            r.EndAt
        }).ToList();

        var inserted = await InsertRowsAsync(
            "IdleSessions",
            new[] { "Id", "SessionId", "DeviceId", "StartAt", "EndAt" },
            values,
            "SessionId",
            ct);

        _logger.LogInformation(
            "IdleSessions insert complete: Attempted={attempted}, Inserted={inserted}, Skipped={skipped}",
            rows.Count,
            inserted,
            rows.Count - inserted);

        return inserted;
    }

    public async Task<int> InsertDeviceSessionsAsync(IReadOnlyList<DeviceSessionRow> rows, CancellationToken ct)
    {
        _logger.LogInformation("InsertDeviceSessionsAsync called with {count} rows", rows.Count);
        
        if (rows.Count == 0)
        {
            _logger.LogWarning("No DeviceSessions to insert");
            return 0;
        }

        var values = rows.Select(r => new object?[]
        {
            Guid.NewGuid(),
            r.SessionId,
            r.DeviceId,
            r.StartAt,
            r.EndAt
        }).ToList();

        var inserted = await InsertRowsAsync(
            "DeviceSessions",
            new[] { "Id", "SessionId", "DeviceId", "StartAt", "EndAt" },
            values,
            "SessionId",
            ct);

        _logger.LogInformation(
            "DeviceSessions insert complete: Attempted={attempted}, Inserted={inserted}, Skipped={skipped}",
            rows.Count,
            inserted,
            rows.Count - inserted);

        return inserted;
    }

    public async Task EnsureDevicesAsync(
        IReadOnlyList<string> deviceIds,
        DateTimeOffset lastSeenAt,
        CancellationToken ct)
    {
        if (deviceIds.Count == 0)
        {
            return;
        }

        _logger.LogInformation("EnsureDevicesAsync called with {count} devices", deviceIds.Count);

        var existing = await _db.Devices
            .Where(d => deviceIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(ct);

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = deviceIds.Where(id => !existingSet.Contains(id)).Distinct().ToList();

        if (missing.Count > 0)
        {
            _logger.LogInformation("Adding {count} new devices", missing.Count);
            foreach (var id in missing)
            {
                _db.Devices.Add(new Device { Id = id, LastSeenAt = lastSeenAt });
            }
        }

        if (existingSet.Count > 0)
        {
            _logger.LogInformation("Updating LastSeenAt for {count} existing devices", existingSet.Count);
            foreach (var id in existingSet)
            {
                var device = new Device { Id = id, LastSeenAt = lastSeenAt };
                _db.Devices.Attach(device);
                _db.Entry(device).Property(d => d.LastSeenAt).IsModified = true;
            }
        }

        var changes = await _db.SaveChangesAsync(ct);
        _logger.LogInformation("EnsureDevicesAsync complete: SaveChanges returned {changes}", changes);
    }

    private async Task<int> InsertRowsAsync(
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<object?[]> rows,
        string conflictColumn,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug(
            "InsertRowsAsync: Table={table}, Rows={rows}, ConflictColumn={conflictColumn}",
            tableName,
            rows.Count,
            conflictColumn);

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            _logger.LogDebug("Opening database connection");
            await _db.Database.OpenConnectionAsync(ct);
        }

        try
        {
            var totalInserted = 0;
            var totalAttempted = 0;

            for (var offset = 0; offset < rows.Count; offset += IngestLimits.InsertChunkSize)
            {
                var chunk = rows.Skip(offset).Take(IngestLimits.InsertChunkSize).ToList();
                totalAttempted += chunk.Count;

                var sql = new StringBuilder();
                sql.Append("INSERT INTO \"").Append(tableName).Append("\" (");
                sql.Append(string.Join(", ", columns.Select(c => $"\"{c}\"")));
                sql.Append(") VALUES ");

                var parameters = new List<NpgsqlParameter>();
                var paramIndex = 0;
                
                for (var i = 0; i < chunk.Count; i++)
                {
                    if (i > 0)
                    {
                        sql.Append(", ");
                    }

                    sql.Append("(");
                    for (var j = 0; j < columns.Count; j++)
                    {
                        if (j > 0)
                        {
                            sql.Append(", ");
                        }

                        var paramName = "@p" + paramIndex++;
                        sql.Append(paramName);
                        parameters.Add(new NpgsqlParameter(paramName, chunk[i][j] ?? DBNull.Value));
                    }
                    sql.Append(")");
                }

                // Changed from DO NOTHING to DO UPDATE to actually save data
                sql.Append(" ON CONFLICT (\"").Append(conflictColumn).Append("\") DO UPDATE SET ");
                
                // Update all columns except Id and the conflict column
                var updateColumns = columns.Where(c => c != "Id" && c != conflictColumn).ToList();
                for (var i = 0; i < updateColumns.Count; i++)
                {
                    if (i > 0) sql.Append(", ");
                    var col = updateColumns[i];
                    sql.Append("\"").Append(col).Append("\" = EXCLUDED.\"").Append(col).Append("\"");
                }
                sql.Append(";");

                await using var command = connection.CreateCommand();
                command.CommandText = sql.ToString();
                command.Parameters.AddRange(parameters.ToArray());

                // Log the SQL for the first chunk only (for debugging)
                if (offset == 0)
                {
                    _logger.LogDebug("SQL (first chunk): {sql}", sql.ToString().Substring(0, Math.Min(500, sql.Length)));
                }

                var affected = await command.ExecuteNonQueryAsync(ct);
                totalInserted += affected;

                _logger.LogDebug(
                    "Chunk {chunkNum}: Attempted={attempted}, Affected={affected}",
                    (offset / IngestLimits.InsertChunkSize) + 1,
                    chunk.Count,
                    affected);
            }

            if (totalInserted == 0 && totalAttempted > 0)
            {
                _logger.LogWarning(
                    "⚠️ WARNING: {table} - NO ROWS INSERTED! Attempted={attempted}, Inserted={inserted}. " +
                    "This likely means all rows had duplicate {conflictColumn} values.",
                    tableName,
                    totalAttempted,
                    totalInserted,
                    conflictColumn);
            }
            else if (totalInserted < totalAttempted)
            {
                _logger.LogInformation(
                    "{table} - Partial insert: Attempted={attempted}, Inserted/Updated={inserted}, Duplicates={duplicates}",
                    tableName,
                    totalAttempted,
                    totalInserted,
                    totalAttempted - totalInserted);
            }
            else
            {
                _logger.LogInformation(
                    "{table} - Full insert: Inserted/Updated={inserted} rows",
                    tableName,
                    totalInserted);
            }

            return totalInserted;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to insert rows into {table}. Rows={rows}",
                tableName,
                rows.Count);
            throw;
        }
        finally
        {
            if (shouldClose)
            {
                _logger.LogDebug("Closing database connection");
                await _db.Database.CloseConnectionAsync();
            }
        }
    }
}