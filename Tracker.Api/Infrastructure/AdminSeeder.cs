using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tracker.Api.Data;
using Tracker.Api.Models;
using Tracker.Api.Security;

namespace Tracker.Api.Infrastructure;

public sealed class AdminSeeder
{
    private readonly TrackerDbContext _db;
    private readonly AdminSeedOptions _options;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        TrackerDbContext db,
        IOptions<AdminSeedOptions> options,
        ILogger<AdminSeeder> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var email = _options.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning("Admin seed enabled but Email or Password missing.");
            return;
        }

        var existing = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Admin user already exists for {email}.", email);
            return;
        }

        var enrollmentKey = _options.CompanyEnrollmentKey?.Trim();
        if (string.IsNullOrWhiteSpace(enrollmentKey))
        {
            enrollmentKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            _logger.LogInformation("Generated enrollment key for seeded company.");
        }

        var enrollmentHash = EnrollmentKeyHelper.Hash(enrollmentKey);
        var companyName = string.IsNullOrWhiteSpace(_options.CompanyName) ? "System" : _options.CompanyName.Trim();
        var company = await _db.Companies.FirstOrDefaultAsync(
            c => c.EnrollmentKeyHash == enrollmentHash || c.Name == companyName,
            ct);

        if (company is null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                Name = companyName,
                EnrollmentKey = enrollmentKey,
                EnrollmentKeyHash = enrollmentHash,
                IsActive = true
            };
            _db.Companies.Add(company);
        }
        else
        {
            if (company.EnrollmentKeyHash != enrollmentHash)
            {
                company.EnrollmentKey = enrollmentKey;
                company.EnrollmentKeyHash = enrollmentHash;
            }
            else if (string.IsNullOrWhiteSpace(company.EnrollmentKey))
            {
                company.EnrollmentKey = enrollmentKey;
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Email = email,
            PasswordHash = PasswordHasher.Hash(_options.Password),
            Role = string.IsNullOrWhiteSpace(_options.Role) ? "SystemAdmin" : _options.Role.Trim(),
            IsActive = true
        };
        _db.Users.Add(user);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded admin user {email} with role {role}.", email, user.Role);
        if (!string.IsNullOrWhiteSpace(enrollmentKey))
        {
            _logger.LogInformation("Seeded company enrollment key: {key}", enrollmentKey);
        }
    }
}
