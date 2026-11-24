using Microsoft.AspNetCore.Identity;
using SmartBooking.Core.Entities;

namespace SmartBooking.Infrastructure.Data;

internal static class SeedData
{
    public const int DefaultTrialPlanId = 2;

    public static readonly Plan[] Plans =
    [
        new()
        {
            Id = 1, Code = "starter", Name = "الأساسية", MonthlyPrice = 499m, Currency = "EGP",
            MaxServices = 3, MaxCounters = 2, MaxStaff = 3, MaxTicketsPerMonth = 3_000,
            HasDisplayScreen = false, HasPushAlerts = true, SortOrder = 1, IsActive = true,
        },
        new()
        {
            Id = 2, Code = "pro", Name = "الاحترافية", MonthlyPrice = 999m, Currency = "EGP",
            MaxServices = 10, MaxCounters = 6, MaxStaff = 10, MaxTicketsPerMonth = 15_000,
            HasDisplayScreen = true, HasPushAlerts = true, SortOrder = 2, IsActive = true,
        },
        new()
        {
            Id = 3, Code = "business", Name = "المؤسسات", MonthlyPrice = 2499m, Currency = "EGP",
            MaxServices = 50, MaxCounters = 30, MaxStaff = 50, MaxTicketsPerMonth = 100_000,
            HasDisplayScreen = true, HasPushAlerts = true, SortOrder = 3, IsActive = true,
        },
    ];

    public static readonly IdentityRole<Guid>[] Roles =
    [
        new() { Id = AppRoles.PlatformAdminId, Name = AppRoles.PlatformAdmin, NormalizedName = "PLATFORMADMIN", ConcurrencyStamp = "role-platform-admin" },
        new() { Id = AppRoles.OwnerId, Name = AppRoles.Owner, NormalizedName = "OWNER", ConcurrencyStamp = "role-owner" },
        new() { Id = AppRoles.StaffId, Name = AppRoles.Staff, NormalizedName = "STAFF", ConcurrencyStamp = "role-staff" },
    ];
}
