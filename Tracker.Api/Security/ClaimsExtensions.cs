using System.Security.Claims;

namespace Tracker.Api.Security;

public static class ClaimsExtensions
{
    public const string CompanyIdClaim = "companyId";
    public const string RoleSystemAdmin = "SystemAdmin";

    public static Guid GetCompanyId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(CompanyIdClaim);
        return Guid.TryParse(raw, out var companyId) ? companyId : Guid.Empty;
    }

    public static bool IsSystemAdmin(this ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role);
        return string.Equals(role, RoleSystemAdmin, StringComparison.OrdinalIgnoreCase);
    }
}
