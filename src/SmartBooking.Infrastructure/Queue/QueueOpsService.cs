using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Entities;
using SmartBooking.Core.Queue;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Notifications;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Queue;

public sealed record CounterListItem(Guid Id, string Name, bool IsOpen, int ServicesCount, string? CurrentCode);

public sealed record ConsoleTicketDto(long Id, string Code, TicketStatus Status, string ServiceName, string? CustomerName,
    string? CustomerPhone, DateTime CreatedAt, DateTime? CalledAt, DateTime? ServiceStartedAt, int RecallCount);

public sealed record WaitingItem(long Id, string Code, string ServiceName, string? CustomerName, TicketSource Source, DateTime CreatedAt);

public sealed record CounterConsoleDto(
    Guid Id, string Name, bool IsOpen, IReadOnlyList<string> ServiceNames, IReadOnlyList<(Guid Id, string Name)> Services,
    ConsoleTicketDto? Current, int WaitingCount, IReadOnlyList<WaitingItem> Next, int ServedToday);

/// <summary>عمليات موظف الشباك: نداء التالي، بدء، إنهاء، لم يحضر، إعادة نداء.</summary>
public sealed class QueueOpsService(
    AppDbContext db, ITenantContext tenantContext, TenantDirectory tenants, TicketIssuer issuer,
    IQueueNotifier notifier, TicketAlertSender alerts, TimeProvider clock)
{
    private Guid TenantId => tenantContext.TenantId ?? throw new InvalidOperationException("No tenant in context.");

    public Task<List<CounterListItem>> GetCountersAsync(CancellationToken ct = default) =>
        db.Counters.Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CounterListItem(
                c.Id, c.Name, c.IsOpen, c.Services.Count,
                db.Tickets.Where(t => t.CounterId == c.Id && (t.Status == TicketStatus.Called || t.Status == TicketStatus.InService))
                    .Select(t => t.Code).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<CounterConsoleDto?> GetConsoleAsync(Guid counterId, CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        var counter = await db.Counters
            .Where(c => c.Id == counterId && c.IsActive)
            .Select(c => new
            {
                c.Id, c.Name, c.IsOpen,
                Services = c.Services.OrderBy(s => s.Service.SortOrder).Select(s => new { s.ServiceId, s.Service.Name }).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (counter is null) return null;

        var serviceIds = counter.Services.Select(s => s.ServiceId).ToList();

        var current = await db.Tickets
            .Where(t => t.CounterId == counterId && (t.Status == TicketStatus.Called || t.Status == TicketStatus.InService))
            .OrderByDescending(t => t.CalledAt)
            .Select(t => new ConsoleTicketDto(t.Id, t.Code, t.Status, t.Service.Name, t.CustomerName, t.CustomerPhone,
                t.CreatedAt, t.CalledAt, t.ServiceStartedAt, t.RecallCount))
            .FirstOrDefaultAsync(ct);

        var waitingQuery = db.Tickets.Where(t => serviceIds.Contains(t.ServiceId) && t.ServiceDate == today && t.Status == TicketStatus.Waiting);
        var waitingCount = await waitingQuery.CountAsync(ct);
        var next = await waitingQuery.OrderBy(t => t.Id).Take(6)
            .Select(t => new WaitingItem(t.Id, t.Code, t.Service.Name, t.CustomerName, t.Source, t.CreatedAt))
            .ToListAsync(ct);

        var served = await db.Tickets.CountAsync(t => t.CounterId == counterId && t.ServiceDate == today && t.Status == TicketStatus.Completed, ct);

        return new CounterConsoleDto(counter.Id, counter.Name, counter.IsOpen,
            counter.Services.Select(s => s.Name).ToList(),
            counter.Services.Select(s => (s.ServiceId, s.Name)).ToList(),
            current, waitingCount, next, served);
    }

    /// <summary>
    /// ينهي العميل الحالي وينادي اللي بعده. الاختيار بيتم بـ UPDLOCK + READPAST:
    /// لو شباكين ضغطوا "التالي" في نفس اللحظة، كل واحد بياخد عميل مختلف ومحدش بيستنى التاني.
    /// </summary>
    public async Task<OpResult<string?>> CallNextAsync(Guid counterId, CancellationToken ct = default)
    {
        var counter = await db.Counters.Where(c => c.Id == counterId && c.IsActive)
            .Select(c => new { c.Id, c.Name, c.IsOpen }).FirstOrDefaultAsync(ct);
        if (counter is null) return OpResult<string?>.Fail("الشباك غير موجود.");

        if (!counter.IsOpen)
            await db.Counters.Where(c => c.Id == counterId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsOpen, true), ct);

        await FinishCurrentAsync(counterId, TicketStatus.Completed, ct);

        var today = await TodayAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var tenantId = TenantId;

        var ids = await db.Database.SqlQuery<long>($"""
            WITH nextTicket AS (
                SELECT TOP (1) t.*
                FROM dbo.Tickets t WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE t.TenantId = {tenantId} AND t.ServiceDate = {today} AND t.Status = 0
                  AND t.ServiceId IN (SELECT cs.ServiceId FROM dbo.CounterServices cs WHERE cs.CounterId = {counterId})
                ORDER BY t.Id
            )
            UPDATE nextTicket SET Status = 1, CalledAt = {now}, CounterId = {counterId}
            OUTPUT inserted.Id AS Value;
            """).ToListAsync(ct);

        if (ids.Count == 0) return OpResult<string?>.Success(null);

        var called = await db.Tickets.Where(t => t.Id == ids[0])
            .Select(t => new { t.Id, t.Code, t.PublicToken, t.ServiceId }).FirstAsync(ct);

        await notifier.TicketCalledAsync(tenantId, called.ServiceId, called.PublicToken, called.Code, counter.Name, ct);
        await notifier.QueueChangedAsync(tenantId, called.ServiceId, ct);
        await alerts.SendAsync(called.Id, "حان دورك الآن", $"تذكرتك {called.Code}، اتفضل على {counter.Name}", ct);
        return OpResult<string?>.Success(called.Code);
    }

    public async Task<OpResult> StartAsync(long ticketId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var n = await db.Tickets.Where(t => t.Id == ticketId && t.Status == TicketStatus.Called)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.InService).SetProperty(t => t.ServiceStartedAt, now), ct);
        if (n == 0) return OpResult.Fail("التذكرة مش في حالة نداء.");
        await NotifyTicketAsync(ticketId, ct);
        return OpResult.Success;
    }

    public async Task<OpResult> CompleteAsync(long ticketId, CancellationToken ct = default) =>
        await FinishAsync(ticketId, TicketStatus.Completed, ct) ? OpResult.Success : OpResult.Fail("التذكرة مش نشطة.");

    public async Task<OpResult> NoShowAsync(long ticketId, CancellationToken ct = default) =>
        await FinishAsync(ticketId, TicketStatus.NoShow, ct) ? OpResult.Success : OpResult.Fail("التذكرة مش نشطة.");

    public async Task<OpResult> RecallAsync(long ticketId, CancellationToken ct = default)
    {
        var t = await db.Tickets.Where(x => x.Id == ticketId && x.Status == TicketStatus.Called && x.CounterId != null)
            .Select(x => new { x.Id, x.Code, x.PublicToken, x.ServiceId, x.TenantId, Counter = x.Counter!.Name })
            .FirstOrDefaultAsync(ct);
        if (t is null) return OpResult.Fail("لا يمكن إعادة النداء.");

        await db.Tickets.Where(x => x.Id == ticketId).ExecuteUpdateAsync(s => s.SetProperty(x => x.RecallCount, x => x.RecallCount + 1), ct);
        await notifier.TicketCalledAsync(t.TenantId, t.ServiceId, t.PublicToken, t.Code, t.Counter, ct);
        await alerts.SendAsync(t.Id, "نداء مرة أخرى", $"رقمك {t.Code} بيتنادى على {t.Counter}", ct);
        return OpResult.Success;
    }

    public async Task<OpResult> SetOpenAsync(Guid counterId, bool open, CancellationToken ct = default)
    {
        var n = await db.Counters.Where(c => c.Id == counterId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsOpen, open), ct);
        if (n == 0) return OpResult.Fail("الشباك غير موجود.");
        if (!open) await FinishCurrentAsync(counterId, TicketStatus.Completed, ct);

        var serviceIds = await db.CounterServices.Where(cs => cs.CounterId == counterId).Select(cs => cs.ServiceId).ToListAsync(ct);
        foreach (var sid in serviceIds) await notifier.QueueChangedAsync(TenantId, sid, ct);
        return OpResult.Success;
    }

    public async Task<OpResult<IssuedTicket>> IssueAtReceptionAsync(Guid serviceId, string? name, string? phone, CancellationToken ct = default)
    {
        var tenant = await tenants.ByIdAsync(TenantId, ct);
        if (tenant is null) return OpResult<IssuedTicket>.Fail("المنشأة غير موجودة.");
        return await issuer.IssueAsync(tenant, serviceId, name, phone, TicketSource.Reception, ct);
    }

    private async Task FinishCurrentAsync(Guid counterId, TicketStatus final, CancellationToken ct)
    {
        var currentIds = await db.Tickets
            .Where(t => t.CounterId == counterId && (t.Status == TicketStatus.Called || t.Status == TicketStatus.InService))
            .Select(t => t.Id).ToListAsync(ct);
        foreach (var id in currentIds) await FinishAsync(id, final, ct);
    }

    private async Task<bool> FinishAsync(long ticketId, TicketStatus final, CancellationToken ct)
    {
        var t = await db.Tickets
            .Where(x => x.Id == ticketId && (x.Status == TicketStatus.Called || x.Status == TicketStatus.InService))
            .Select(x => new { x.Id, x.ServiceId, x.TenantId, x.CalledAt, x.ServiceStartedAt })
            .FirstOrDefaultAsync(ct);
        if (t is null) return false;

        var now = clock.GetUtcNow().UtcDateTime;
        var n = await db.Tickets
            .Where(x => x.Id == ticketId && (x.Status == TicketStatus.Called || x.Status == TicketStatus.InService))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, final).SetProperty(x => x.FinishedAt, now), ct);
        if (n == 0) return false;

        if (final == TicketStatus.Completed && (t.ServiceStartedAt ?? t.CalledAt) is { } started)
        {
            // المتوسط بيتعلم من الواقع: كل تذكرة بتخلص بتعدّل متوسط وقت الخدمة
            var observed = (int)(now - started).TotalSeconds;
            var avg = await db.Services.Where(s => s.Id == t.ServiceId).Select(s => s.AvgServiceSeconds).FirstAsync(ct);
            var next = EtaCalculator.NextAverage(avg, observed);
            await db.Services.Where(s => s.Id == t.ServiceId).ExecuteUpdateAsync(s => s.SetProperty(x => x.AvgServiceSeconds, next), ct);
        }

        await notifier.QueueChangedAsync(t.TenantId, t.ServiceId, ct);
        return true;
    }

    private async Task NotifyTicketAsync(long ticketId, CancellationToken ct)
    {
        var t = await db.Tickets.Where(x => x.Id == ticketId).Select(x => new { x.TenantId, x.ServiceId }).FirstOrDefaultAsync(ct);
        if (t is not null) await notifier.QueueChangedAsync(t.TenantId, t.ServiceId, ct);
    }

    private async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        var tenant = await tenants.ByIdAsync(TenantId, ct) ?? throw new InvalidOperationException("Tenant not found.");
        return tenant.Today(clock.GetUtcNow().UtcDateTime);
    }
}
