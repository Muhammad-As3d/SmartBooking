using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Entities;
using SmartBooking.Core.Queue;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Queue;

public sealed record ServiceQueueDto(
    Guid Id, string Name, string? Description, string Prefix, int Waiting, int OpenCounters, int EtaSeconds);

public sealed record BookingPageDto(TenantSnapshot Tenant, bool IsOpen, IReadOnlyList<ServiceQueueDto> Services);

public sealed record TicketStatusDto(
    string Token,
    string Code,
    TicketStatus Status,
    string StatusLabel,
    string ServiceName,
    Guid ServiceId,
    string TenantName,
    string TenantSlug,
    int Ahead,
    int EtaSeconds,
    string EtaLabel,
    string? CounterName,
    DateTime CreatedAt,
    int AlertLeadMinutes,
    bool PushEnabled,
    bool HasPush,
    string TimeZoneId);

public sealed record DisplayCallDto(string Code, string CounterName, string ServiceName, DateTime CalledAt);
public sealed record DisplayServiceDto(Guid Id, string Name, int Waiting, string? NextCode);
public sealed record DisplayDto(TenantSnapshot Tenant, IReadOnlyList<DisplayCallDto> NowServing, IReadOnlyList<DisplayServiceDto> Services);

public sealed class PublicQueueService(AppDbContext db, TenantDirectory tenants, TicketIssuer issuer, IQueueNotifier notifier, TimeProvider clock)
{
    private sealed record TicketRow(
        long Id, Guid TenantId, Guid ServiceId, string Code, TicketStatus Status, DateTime CreatedAt,
        string ServiceName, int AvgSeconds, string? CounterName, int Ahead, bool InService, int OpenCounters, bool HasPush);

    // أكتر استعلام بيتنفذ في النظام (كل تذكرة مفتوحة بتسأله مع كل تحديث) فمتجمّع مسبقاً
    private static readonly Func<AppDbContext, string, Task<TicketRow?>> TicketByToken =
        EF.CompileAsyncQuery((AppDbContext db, string token) =>
            db.Tickets
                .Where(t => t.PublicToken == token)
                .Select(t => new TicketRow(
                    t.Id, t.TenantId, t.ServiceId, t.Code, t.Status, t.CreatedAt,
                    t.Service.Name, t.Service.AvgServiceSeconds,
                    t.Counter != null ? t.Counter.Name : null,
                    db.Tickets.Count(o => o.ServiceId == t.ServiceId && o.ServiceDate == t.ServiceDate
                                          && o.Status == TicketStatus.Waiting && o.Id < t.Id),
                    db.Tickets.Any(o => o.ServiceId == t.ServiceId && o.ServiceDate == t.ServiceDate
                                        && (o.Status == TicketStatus.Called || o.Status == TicketStatus.InService)),
                    db.CounterServices.Count(cs => cs.ServiceId == t.ServiceId && cs.Counter.IsOpen && cs.Counter.IsActive),
                    t.PushSubscriptions.Any()))
                .FirstOrDefault());

    public async Task<BookingPageDto?> GetBookingPageAsync(string slug, CancellationToken ct = default)
    {
        var tenant = await tenants.BySlugAsync(slug, ct);
        if (tenant is null) return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var today = tenant.Today(now);
        var rows = await db.Services
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Id, s.Name, s.Description, s.Prefix, s.AvgServiceSeconds,
                Waiting = db.Tickets.Count(t => t.ServiceId == s.Id && t.ServiceDate == today && t.Status == TicketStatus.Waiting),
                InService = db.Tickets.Any(t => t.ServiceId == s.Id && t.ServiceDate == today
                                                && (t.Status == TicketStatus.Called || t.Status == TicketStatus.InService)),
                Open = s.Counters.Count(cs => cs.Counter.IsOpen && cs.Counter.IsActive),
            })
            .ToListAsync(ct);

        var services = rows
            .Select(r => new ServiceQueueDto(r.Id, r.Name, r.Description, r.Prefix, r.Waiting, r.Open,
                EtaCalculator.EstimateSeconds(r.Waiting, r.AvgServiceSeconds, r.Open, r.InService)))
            .ToList();

        return new BookingPageDto(tenant, tenant.AcceptingTickets && tenant.IsUsable(now), services);
    }

    public async Task<OpResult<IssuedTicket>> TakeTicketAsync(string slug, Guid serviceId, string? name, string? phone, TicketSource source, CancellationToken ct = default)
    {
        var tenant = await tenants.BySlugAsync(slug, ct);
        if (tenant is null) return OpResult<IssuedTicket>.Fail("المنشأة غير موجودة.");
        return await issuer.IssueAsync(tenant, serviceId, name, phone, source, ct);
    }

    public async Task<TicketStatusDto?> GetTicketAsync(string token, CancellationToken ct = default)
    {
        if (!IsTokenShape(token)) return null;
        var row = await TicketByToken(db, token);
        if (row is null) return null;

        var tenant = await tenants.ByIdAsync(row.TenantId, ct);
        if (tenant is null) return null;

        var eta = row.Status == TicketStatus.Waiting
            ? EtaCalculator.EstimateSeconds(row.Ahead, row.AvgSeconds, row.OpenCounters, row.InService)
            : 0;

        return new TicketStatusDto(
            token, row.Code, row.Status, Labels.Of(row.Status), row.ServiceName, row.ServiceId,
            tenant.Name, tenant.Slug,
            row.Status == TicketStatus.Waiting ? row.Ahead : 0,
            eta, EtaCalculator.FormatMinutes(eta),
            row.CounterName, row.CreatedAt, tenant.AlertLeadMinutes, tenant.HasPushAlerts, row.HasPush, tenant.TimeZoneId);
    }

    public async Task<bool> CancelAsync(string token, CancellationToken ct = default)
    {
        if (!IsTokenShape(token)) return false;
        var info = await db.Tickets.Where(t => t.PublicToken == token && t.Status == TicketStatus.Waiting)
            .Select(t => new { t.TenantId, t.ServiceId }).FirstOrDefaultAsync(ct);
        if (info is null) return false;

        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await db.Tickets
            .Where(t => t.PublicToken == token && t.Status == TicketStatus.Waiting)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Cancelled).SetProperty(t => t.FinishedAt, now), ct);

        if (changed > 0) await notifier.QueueChangedAsync(info.TenantId, info.ServiceId, ct);
        return changed > 0;
    }

    public async Task<bool> SavePushSubscriptionAsync(string token, string endpoint, string p256dh, string auth, CancellationToken ct = default)
    {
        if (!IsTokenShape(token)) return false;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || endpoint.Length > 600)
            return false;
        if (p256dh.Length is 0 or > 120 || auth.Length is 0 or > 60) return false;

        var ticket = await db.Tickets
            .Where(t => t.PublicToken == token && t.Status <= TicketStatus.InService)
            .Select(t => new { t.Id, Count = t.PushSubscriptions.Count, Exists = t.PushSubscriptions.Any(p => p.Endpoint == endpoint) })
            .FirstOrDefaultAsync(ct);
        if (ticket is null || ticket.Count >= 3) return false;
        if (ticket.Exists) return true;

        db.TicketPushSubscriptions.Add(new TicketPushSubscription
        {
            TicketId = ticket.Id, Endpoint = endpoint, P256dh = p256dh, Auth = auth,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DisplayDto?> GetDisplayAsync(string slug, CancellationToken ct = default)
    {
        var tenant = await tenants.BySlugAsync(slug, ct);
        if (tenant is null) return null;
        var today = tenant.Today(clock.GetUtcNow().UtcDateTime);

        var serving = await db.Tickets
            .Where(t => t.TenantId == tenant.Id && t.ServiceDate == today
                        && (t.Status == TicketStatus.Called || t.Status == TicketStatus.InService) && t.CounterId != null)
            .OrderByDescending(t => t.CalledAt)
            .Take(8)
            .Select(t => new DisplayCallDto(t.Code, t.Counter!.Name, t.Service.Name, t.CalledAt ?? t.CreatedAt))
            .ToListAsync(ct);

        var services = await db.Services
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new DisplayServiceDto(
                s.Id, s.Name,
                db.Tickets.Count(t => t.ServiceId == s.Id && t.ServiceDate == today && t.Status == TicketStatus.Waiting),
                db.Tickets.Where(t => t.ServiceId == s.Id && t.ServiceDate == today && t.Status == TicketStatus.Waiting)
                    .OrderBy(t => t.Id).Select(t => t.Code).FirstOrDefault()))
            .ToListAsync(ct);

        return new DisplayDto(tenant, serving, services);
    }

    public static bool IsTokenShape(string? token) =>
        token is { Length: 22 } && token.AsSpan().IndexOfAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_") < 0;
}
