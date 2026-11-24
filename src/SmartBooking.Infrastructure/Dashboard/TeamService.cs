using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Dashboard;

public sealed record TeamMemberDto(Guid Id, string FullName, string Email, bool IsOwner, bool IsLocked, DateTime CreatedAt);

public sealed class TeamService(AppDbContext db, UserManager<ApplicationUser> users, ITenantContext tenantContext, TenantDirectory tenants, TimeProvider clock)
{
    private Guid TenantId => tenantContext.TenantId ?? throw new InvalidOperationException("No tenant in context.");

    public async Task<List<TeamMemberDto>> ListAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var now = clock.GetUtcNow();
        return await db.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new TeamMemberDto(
                u.Id, u.FullName, u.Email!,
                db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == AppRoles.OwnerId),
                u.LockoutEnd != null && u.LockoutEnd > now,
                u.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<OpResult> AddStaffAsync(string fullName, string email, string password, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var tenant = await tenants.ByIdAsync(tenantId, ct);
        var count = await db.Users.CountAsync(u => u.TenantId == tenantId, ct);
        if (tenant is not null && count >= tenant.MaxStaff)
            return OpResult.Fail($"باقتك تسمح بـ {tenant.MaxStaff} مستخدمين.");

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(), UserName = email.Trim(), Email = email.Trim(), FullName = fullName.Trim(),
            TenantId = tenantId, CreatedAt = clock.GetUtcNow().UtcDateTime, EmailConfirmed = true,
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) return OpResult.Fail(IdentityErrorsArabic.Describe(result));
        await users.AddToRoleAsync(user, AppRoles.Staff);
        return OpResult.Success;
    }

    public async Task<OpResult> SetLockedAsync(Guid userId, bool locked, Guid currentUserId, CancellationToken ct = default)
    {
        if (userId == currentUserId) return OpResult.Fail("مش هينفع توقف حسابك.");
        var user = await users.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == TenantId, ct);
        if (user is null) return OpResult.Fail("المستخدم غير موجود.");
        if (await users.IsInRoleAsync(user, AppRoles.Owner)) return OpResult.Fail("مش هينفع توقف حساب المالك.");

        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null);
        await users.UpdateSecurityStampAsync(user); // يطرده من أي جلسة مفتوحة
        return OpResult.Success;
    }
}
