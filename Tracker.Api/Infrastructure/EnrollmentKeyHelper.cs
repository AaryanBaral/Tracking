using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Data;
using Tracker.Api.Models;

namespace Tracker.Api.Infrastructure;

public static class EnrollmentKeyHelper
{
    public const string HeaderName = "X-Company-Enroll";

    public static string Hash(string enrollmentKey)
    {
        var trimmed = enrollmentKey.Trim();
        var bytes = Encoding.UTF8.GetBytes(trimmed);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static async Task<Company?> ResolveCompanyAsync(
        HttpRequest request,
        TrackerDbContext db,
        CancellationToken ct)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var values))
        {
            return null;
        }

        var enrollmentKey = values.ToString().Trim();
        if (string.IsNullOrWhiteSpace(enrollmentKey))
        {
            return null;
        }

        var hash = Hash(enrollmentKey);
        return await db.Companies
            .FirstOrDefaultAsync(c => c.EnrollmentKeyHash == hash && c.IsActive, ct);
    }
}
