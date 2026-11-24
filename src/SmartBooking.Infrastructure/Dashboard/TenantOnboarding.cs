using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Billing;
using SmartBooking.Core.Entities;
using SmartBooking.Infrastructure.Data;

namespace SmartBooking.Infrastructure.Dashboard;

public sealed record RegisterTenantInput(
    string BusinessName, BusinessKind Kind, string Slug, string? City, string TimeZoneId,
    string OwnerName, string Email, string Phone, string Password);

/// <summary>تسجيل منشأة جديدة: المنشأة + حساب المالك + خدمة ونقطة خدمة جاهزين + 14 يوم تجربة.</summary>
public sealed partial class TenantOnboarding(AppDbContext db, UserManager<ApplicationUser> users, TimeProvider clock)
{
    private static readonly HashSet<string> ReservedSlugs =
        ["admin", "app", "api", "account", "q", "t", "d", "hub", "hubs", "www", "support", "pricing", "login", "register", "static", "lib", "css", "js", "img"];

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$")]
    private static partial Regex SlugPattern();

    public static bool IsValidSlug(string slug) => SlugPattern().IsMatch(slug) && !ReservedSlugs.Contains(slug) && !slug.Contains("--");

    public async Task<bool> IsSlugAvailableAsync(string slug, CancellationToken ct = default) =>
        IsValidSlug(slug) && !await db.Tenants.AnyAsync(t => t.Slug == slug, ct);

    public async Task<OpResult<ApplicationUser>> RegisterAsync(RegisterTenantInput input, CancellationToken ct = default)
    {
        var slug = input.Slug.Trim().ToLowerInvariant();
        if (!IsValidSlug(slug))
            return OpResult<ApplicationUser>.Fail("رابط المنشأة لازم يكون حروف إنجليزي صغيرة وأرقام وشرطة فقط (3-40 حرف).");
        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            return OpResult<ApplicationUser>.Fail("الرابط ده محجوز لمنشأة تانية، جرّب رابط مختلف.");

        var now = clock.GetUtcNow().UtcDateTime;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var tenant = new Tenant
            {
                Id = Guid.CreateVersion7(),
                Name = input.BusinessName.Trim(),
                Slug = slug,
                Kind = input.Kind,
                City = input.City?.Trim(),
                Phone = input.Phone.Trim(),
                TimeZoneId = input.TimeZoneId,
                CreatedAt = now,
                PlanId = SeedData.DefaultTrialPlanId,
                SubscriptionStatus = SubscriptionStatus.Trial,
                TrialEndsAt = now.AddDays(SubscriptionRules.TrialDays),
            };
            db.Tenants.Add(tenant);

            var (serviceName, prefix, minutes, counterName) = input.Kind switch
            {
                BusinessKind.Clinic => ("كشف عام", "A", 12, "عيادة 1"),
                BusinessKind.MedicalLab => ("سحب عينات", "L", 6, "شباك 1"),
                BusinessKind.ServiceOffice => ("خدمة العملاء", "S", 8, "شباك 1"),
                _ => ("خدمة عامة", "A", 10, "شباك 1"),
            };
            var service = new Service
            {
                Id = Guid.CreateVersion7(), TenantId = tenant.Id, Name = serviceName, Prefix = prefix,
                AvgServiceSeconds = minutes * 60, CreatedAt = now, SortOrder = 1,
            };
            var counter = new Counter { Id = Guid.CreateVersion7(), TenantId = tenant.Id, Name = counterName, CreatedAt = now, SortOrder = 1 };
            counter.Services.Add(new CounterService { CounterId = counter.Id, ServiceId = service.Id });
            db.Services.Add(service);
            db.Counters.Add(counter);
            await db.SaveChangesAsync(ct);

            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = input.Email.Trim(),
                Email = input.Email.Trim(),
                PhoneNumber = input.Phone.Trim(),
                FullName = input.OwnerName.Trim(),
                TenantId = tenant.Id,
                CreatedAt = now,
            };
            var created = await users.CreateAsync(user, input.Password);
            if (!created.Succeeded)
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return OpResult<ApplicationUser>.Fail(IdentityErrorsArabic.Describe(created));
            }
            await users.AddToRoleAsync(user, AppRoles.Owner);

            await tx.CommitAsync(ct);
            return OpResult<ApplicationUser>.Success(user);
        });
    }
}

public static class IdentityErrorsArabic
{
    public static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Code switch
        {
            "DuplicateUserName" or "DuplicateEmail" => "البريد ده مسجّل قبل كده.",
            "InvalidEmail" => "البريد الإلكتروني غير صحيح.",
            "PasswordTooShort" => "كلمة المرور لازم تكون 8 حروف على الأقل.",
            "PasswordRequiresDigit" => "كلمة المرور لازم فيها رقم.",
            "PasswordRequiresLower" => "كلمة المرور لازم فيها حرف إنجليزي صغير.",
            "PasswordRequiresUpper" => "كلمة المرور لازم فيها حرف إنجليزي كبير.",
            "PasswordRequiresNonAlphanumeric" => "كلمة المرور لازم فيها رمز.",
            _ => e.Description,
        }).Distinct());
}
