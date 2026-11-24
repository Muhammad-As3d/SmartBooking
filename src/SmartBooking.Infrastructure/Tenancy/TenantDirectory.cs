using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartBooking.Core.Billing;
using SmartBooking.Core.Entities;
using SmartBooking.Infrastructure.Data;

namespace SmartBooking.Infrastructure.Tenancy;

public sealed record TenantSnapshot(
    Guid Id,
    string Name,
    string Slug,
    BusinessKind Kind,
    string TimeZoneId,
    int AlertLeadMinutes,
    bool AcceptingTickets,
    bool IsSuspended,
    SubscriptionStatus SubscriptionStatus,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodEnd,
    int PlanId,
    string PlanName,
    int MaxServices,
    int MaxCounters,
    int MaxStaff,
    int MaxTicketsPerMonth,
    bool HasDisplayScreen,
    bool HasPushAlerts)
{
    public bool IsUsable(DateTime utcNow) =>
        SubscriptionRules.IsUsable(SubscriptionStatus, TrialEndsAt, CurrentPeriodEnd, IsSuspended, utcNow);

    public TimeZoneInfo TimeZone => TenantTime.Zone(TimeZoneId);
    public DateOnly Today(DateTime utcNow) => TenantTime.LocalDate(TimeZoneId, utcNow);
}

/// <summary>
/// كاش قصير لبيانات المنشأة: صفحة الحجز والتذكرة الحية بتتفتح آلاف المرات، فمش لازم نسأل الداتابيز كل مرة.
/// </summary>
public sealed class TenantDirectory(AppDbContext db, IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public Task<TenantSnapshot?> BySlugAsync(string slug, CancellationToken ct = default)
    {
        slug = slug.Trim().ToLowerInvariant();
        return cache.GetOrCreateAsync(SlugKey(slug), e =>
        {
            e.AbsoluteExpirationRelativeToNow = Ttl;
            return Project(db.Tenants.Where(t => t.Slug == slug)).FirstOrDefaultAsync(ct);
        });
    }

    public Task<TenantSnapshot?> ByIdAsync(Guid id, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(IdKey(id), e =>
        {
            e.AbsoluteExpirationRelativeToNow = Ttl;
            return Project(db.Tenants.Where(t => t.Id == id)).FirstOrDefaultAsync(ct);
        });

    public void Invalidate(Guid id, string slug)
    {
        cache.Remove(IdKey(id));
        cache.Remove(SlugKey(slug));
    }

    private static IQueryable<TenantSnapshot> Project(IQueryable<Tenant> q) =>
        q.Select(t => new TenantSnapshot(
            t.Id, t.Name, t.Slug, t.Kind, t.TimeZoneId, t.AlertLeadMinutes, t.AcceptingTickets, t.IsSuspended,
            t.SubscriptionStatus, t.TrialEndsAt, t.CurrentPeriodEnd,
            t.PlanId, t.Plan.Name, t.Plan.MaxServices, t.Plan.MaxCounters, t.Plan.MaxStaff, t.Plan.MaxTicketsPerMonth,
            t.Plan.HasDisplayScreen, t.Plan.HasPushAlerts));

    private static string SlugKey(string slug) => "tenant:slug:" + slug;
    private static string IdKey(Guid id) => "tenant:id:" + id.ToString("N");
}

public static class TenantTime
{
    private static readonly Dictionary<string, TimeZoneInfo> Zones = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    public static readonly (string Id, string Label)[] Supported =
    [
        ("Africa/Cairo", "القاهرة"),
        ("Asia/Riyadh", "الرياض"),
        ("Asia/Dubai", "دبي"),
        ("Asia/Kuwait", "الكويت"),
        ("Asia/Qatar", "الدوحة"),
        ("Asia/Bahrain", "المنامة"),
        ("Asia/Muscat", "مسقط"),
        ("Asia/Amman", "عمّان"),
    ];

    public static TimeZoneInfo Zone(string id)
    {
        lock (Gate)
        {
            if (Zones.TryGetValue(id, out var z)) return z;
            try { z = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { z = TimeZoneInfo.Utc; }
            Zones[id] = z;
            return z;
        }
    }

    public static DateTime ToLocal(string zoneId, DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(zoneId));

    public static DateOnly LocalDate(string zoneId, DateTime utc) => DateOnly.FromDateTime(ToLocal(zoneId, utc));

    /// <summary>بداية الشهر الحالي بتوقيت المنشأة، محوّلة لـ UTC (لحساب حد التذاكر الشهري).</summary>
    public static DateTime MonthStartUtc(string zoneId, DateTime utc)
    {
        var local = ToLocal(zoneId, utc);
        var start = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(start, Zone(zoneId));
    }
}
