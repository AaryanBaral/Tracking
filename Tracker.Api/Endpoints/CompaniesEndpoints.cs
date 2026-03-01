using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Contracts;
using Tracker.Api.Data;
using Tracker.Api.Infrastructure;
using Tracker.Api.Models;
using Tracker.Api.Security;

namespace Tracker.Api.Endpoints;

public static class CompaniesEndpoints
{
    public static IEndpointRouteBuilder MapCompaniesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/companies", GetCompaniesAsync).RequireAuthorization();
        app.MapGet("/companies/{companyId}/devices", GetCompanyDevicesAsync).RequireAuthorization();
        app.MapPost("/companies", CreateCompanyAsync).RequireAuthorization();
        app.MapPut("/companies/{companyId}", UpdateCompanyAsync).RequireAuthorization();
        app.MapDelete("/companies/{companyId}", DeleteCompanyAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetCompaniesAsync(
        ClaimsPrincipal user,
        TrackerDbContext db,
        CancellationToken ct)
    {
        if (!user.IsSystemAdmin())
        {
            return Results.Forbid();
        }

        var companies = await db.Companies
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CompanyListItem(
                c.Id,
                c.Name,
                c.IsActive,
                db.Devices.Count(d => d.CompanyId == c.Id),
                c.EnrollmentKey))
            .ToListAsync(ct);

        return Results.Ok(companies);
    }

    private static async Task<IResult> GetCompanyDevicesAsync(
        Guid companyId,
        ClaimsPrincipal user,
        TrackerDbContext db,
        int page = 1,
        int pageSize = 50,
        string? query = null,
        string? search = null,
        bool? seen = null,
        DateTimeOffset? activeSince = null,
        CancellationToken ct = default)
    {
        if (!user.IsSystemAdmin())
        {
            return Results.Forbid();
        }

        var companyExists = await db.Companies
            .AsNoTracking()
            .AnyAsync(c => c.Id == companyId, ct);
        if (!companyExists)
        {
            return Results.NotFound(new { message = "Company not found" });
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var deviceQuery = db.Devices.AsNoTracking().Where(d => d.CompanyId == companyId);
        var term = !string.IsNullOrWhiteSpace(query) ? query.Trim() : search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            deviceQuery = deviceQuery.Where(d =>
                EF.Functions.ILike(d.Id, pattern) ||
                (d.Hostname != null && EF.Functions.ILike(d.Hostname, pattern)) ||
                (d.DisplayName != null && EF.Functions.ILike(d.DisplayName, pattern)));
        }

        if (activeSince.HasValue)
        {
            deviceQuery = deviceQuery.Where(d => d.LastSeenAt >= activeSince.Value);
        }

        if (seen.HasValue)
        {
            deviceQuery = seen.Value
                ? deviceQuery.Where(d => d.LastReviewedAt != null)
                : deviceQuery.Where(d => d.LastReviewedAt == null);
        }

        var devices = await deviceQuery
            .OrderByDescending(d => d.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DeviceListItem(
                d.Id,
                d.Hostname,
                d.DisplayName,
                d.LastSeenAt,
                d.LastReviewedAt))
            .ToListAsync(ct);

        return Results.Ok(devices);
    }

    private static async Task<IResult> CreateCompanyAsync(
        CreateCompanyRequest request,
        ClaimsPrincipal user,
        TrackerDbContext db,
        CancellationToken ct)
    {
        if (!user.IsSystemAdmin())
        {
            return Results.Forbid();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest("name is required.");
        }
        if (name.Length > 200)
        {
            return Results.BadRequest("name is too long.");
        }

        var enrollmentKey = request.EnrollmentKey?.Trim();
        if (string.IsNullOrWhiteSpace(enrollmentKey))
        {
            return Results.BadRequest("enrollmentKey is required.");
        }
        if (enrollmentKey.Length > 256)
        {
            return Results.BadRequest("enrollmentKey is too long.");
        }

        var adminEmail = request.AdminEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return Results.BadRequest("adminEmail is required.");
        }
        if (adminEmail.Length > 320)
        {
            return Results.BadRequest("adminEmail is too long.");
        }

        var adminPassword = request.AdminPassword?.Trim();
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            return Results.BadRequest("adminPassword is required.");
        }
        if (adminPassword.Length < 8)
        {
            return Results.BadRequest("adminPassword must be at least 8 characters.");
        }

        var enrollmentHash = EnrollmentKeyHelper.Hash(enrollmentKey!);
        var exists = await db.Companies.AsNoTracking()
            .AnyAsync(c => c.EnrollmentKeyHash == enrollmentHash, ct);
        if (exists)
        {
            return Results.Conflict("enrollmentKey already exists.");
        }

        var adminExists = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Email == adminEmail, ct);
        if (adminExists)
        {
            return Results.Conflict("adminEmail already exists.");
        }

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = name,
            EnrollmentKey = enrollmentKey,
            EnrollmentKeyHash = enrollmentHash,
            IsActive = request.IsActive
        };

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Email = adminEmail,
            PasswordHash = PasswordHasher.Hash(adminPassword),
            Role = "CompanyAdmin",
            IsActive = true
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Companies.Add(company);
        db.Users.Add(adminUser);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Results.Ok(new CreateCompanyResponse(
            company.Id,
            company.Name,
            company.IsActive,
            enrollmentKey!));
    }

    private static async Task<IResult> UpdateCompanyAsync(
        Guid companyId,
        UpdateCompanyRequest request,
        ClaimsPrincipal user,
        TrackerDbContext db,
        CancellationToken ct)
    {
        if (!user.IsSystemAdmin())
        {
            return Results.Forbid();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest("name is required.");
        }
        if (name.Length > 200)
        {
            return Results.BadRequest("name is too long.");
        }

        var enrollmentKey = request.EnrollmentKey?.Trim();
        if (string.IsNullOrWhiteSpace(enrollmentKey))
        {
            return Results.BadRequest("enrollmentKey is required.");
        }
        if (enrollmentKey.Length > 256)
        {
            return Results.BadRequest("enrollmentKey is too long.");
        }

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null)
        {
            return Results.NotFound(new { message = "Company not found" });
        }

        var enrollmentHash = EnrollmentKeyHelper.Hash(enrollmentKey);
        var exists = await db.Companies.AsNoTracking()
            .AnyAsync(c => c.Id != companyId && c.EnrollmentKeyHash == enrollmentHash, ct);
        if (exists)
        {
            return Results.Conflict("enrollmentKey already exists.");
        }

        company.Name = name;
        company.EnrollmentKey = enrollmentKey;
        company.EnrollmentKeyHash = enrollmentHash;
        company.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new CompanyListItem(
            company.Id,
            company.Name,
            company.IsActive,
            await db.Devices.CountAsync(d => d.CompanyId == company.Id, ct),
            company.EnrollmentKey));
    }

    private static async Task<IResult> DeleteCompanyAsync(
        Guid companyId,
        ClaimsPrincipal user,
        TrackerDbContext db,
        CancellationToken ct)
    {
        if (!user.IsSystemAdmin())
        {
            return Results.Forbid();
        }

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null)
        {
            return Results.NotFound(new { message = "Company not found" });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var deviceIds = await db.Devices
            .AsNoTracking()
            .Where(d => d.CompanyId == companyId)
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (deviceIds.Count > 0)
        {
            await db.WebEvents.Where(e => deviceIds.Contains(e.DeviceId)).ExecuteDeleteAsync(ct);
            await db.WebSessions.Where(s => deviceIds.Contains(s.DeviceId)).ExecuteDeleteAsync(ct);
            await db.AppSessions.Where(s => deviceIds.Contains(s.DeviceId)).ExecuteDeleteAsync(ct);
            await db.IdleSessions.Where(s => deviceIds.Contains(s.DeviceId)).ExecuteDeleteAsync(ct);
            await db.DeviceSessions.Where(s => deviceIds.Contains(s.DeviceId)).ExecuteDeleteAsync(ct);
            await db.MonitorSessions.Where(s => deviceIds.Contains(s.DeviceId)).ExecuteDeleteAsync(ct);
            await db.Screenshots.Where(s => deviceIds.Contains(s.DeviceId)).ExecuteDeleteAsync(ct);
            await db.IngestCursors.Where(c => deviceIds.Contains(c.DeviceId)).ExecuteDeleteAsync(ct);
            await db.Devices.Where(d => deviceIds.Contains(d.Id)).ExecuteDeleteAsync(ct);
        }

        await db.Users.Where(u => u.CompanyId == companyId).ExecuteDeleteAsync(ct);
        await db.Companies.Where(c => c.Id == companyId).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
        return Results.Ok(new { deleted = true });
    }
}
