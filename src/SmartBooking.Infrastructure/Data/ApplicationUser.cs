using Microsoft.AspNetCore.Identity;
using SmartBooking.Core.Entities;

namespace SmartBooking.Infrastructure.Data;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = default!;
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class AppRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string Owner = "Owner";
    public const string Staff = "Staff";

    internal static readonly Guid PlatformAdminId = Guid.Parse("8c1a3f0e-4b2d-4f6a-9a51-0d7e2b1c9f01");
    internal static readonly Guid OwnerId = Guid.Parse("8c1a3f0e-4b2d-4f6a-9a51-0d7e2b1c9f02");
    internal static readonly Guid StaffId = Guid.Parse("8c1a3f0e-4b2d-4f6a-9a51-0d7e2b1c9f03");
}

public static class AppClaims
{
    public const string TenantId = "tenant_id";
    public const string FullName = "full_name";
}
