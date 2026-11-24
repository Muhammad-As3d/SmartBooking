using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Entities;
using SmartBooking.Core.Queue;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Dashboard;

public sealed record ServiceTodayDto(Guid Id, string Name, string Prefix, int Waiting, int Active, int Completed, int NoShow, int AvgWaitSeconds, int AvgServiceSeconds, int EtaForNewSeconds);
public sealed record DayCountDto(DateOnly Date, int Total, int Completed);
public sealed record OverviewDto(
    TenantSnapshot Tenant, int Waiting, int Active, int Completed, int NoShow, int AvgWaitSeconds,
    int OpenCounters, int TicketsThisMonth, IReadOnlyList<ServiceTodayDto> Services, IReadOnlyList<DayCountDto> Last7Days,
    IReadOnlyList<RecentTicketDto> Recent);
public sealed record RecentTicketDto(string Code, string ServiceName, TicketStatus Status, TicketSource Source, string? CounterName, DateTime CreatedAt);

public sealed class StatsService(AppDbContext db, ITenantContext tenantContext, TenantDirectory tenants, TimeProvider clock)
{
    public async Task<OverviewDto?> GetOverviewAsync(CancellationToken ct = default)
    {
        if (tenantContext.TenantId is not { } tenantId) return null;
        var tenant = await tenants.ByIdAsync(tenantId, ct);
        if (tenant is null) return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var today = tenant.Today(now);
        var weekStart = today.AddDays(-6);
        var monthStart = TenantTime.MonthStartUtc(tenant.TimeZoneId, now);

        // تجميع كامل في SQL؛ بيرجع صف واحد لكل خدمة
        var perService = await db.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new
            {
                s.Id, s.Name, s.Prefix, s.AvgServiceSeconds,
                Waiting = db.Tickets.Count(t => t.ServiceId == s.Id && t.ServiceDate == today && t.Status == TicketStatus.Waiting),
                Active = db.Tickets.Count(t => t.ServiceId == s.Id && t.ServiceDate == today && (t.Status == TicketStatus.Called || t.Status == TicketStatus.InService)),
                Completed = db.Tickets.Count(t => t.ServiceId == s.Id && t.ServiceDate == today && t.Status == TicketStatus.Completed),
                NoShow = db.Tickets.Count(t => t.ServiceId == s.Id && t.ServiceDate == today && t.Status == TicketStatus.NoShow),
                AvgWait = db.Tickets.Where(t => t.ServiceId == s.Id && t.ServiceDate == today && t.CalledAt != null)
                    .Average(t => (double?)EF.Functions.DateDiffSecond(t.CreatedAt, t.CalledAt!.Value)),
                Open = s.Counters.Count(cs => cs.Counter.IsOpen && cs.Counter.IsActive),
            })
            .ToListAsync(ct);

        var services = perService.Select(s => new ServiceTodayDto(
            s.Id, s.Name, s.Prefix, s.Waiting, s.Active, s.Completed, s.NoShow, (int)(s.AvgWait ?? 0), s.AvgServiceSeconds,
            EtaCalculator.EstimateSeconds(s.Waiting, s.AvgServiceSeconds, s.Open, s.Active > 0))).ToList();

        var days = await db.Tickets
            .Where(t => t.ServiceDate >= weekStart && t.ServiceDate <= today)
            .GroupBy(t => t.ServiceDate)
            .Select(g => new DayCountDto(g.Key, g.Count(), g.Count(t => t.Status == TicketStatus.Completed)))
            .ToListAsync(ct);
        var last7 = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i))
            .Select(d => days.FirstOrDefault(x => x.Date == d) ?? new DayCountDto(d, 0, 0)).ToList();

        var openCounters = await db.Counters.CountAsync(c => c.IsOpen && c.IsActive, ct);
        var month = await db.Tickets.CountAsync(t => t.CreatedAt >= monthStart, ct);

        var recent = await db.Tickets.Where(t => t.ServiceDate == today)
            .OrderByDescending(t => t.Id).Take(8)
            .Select(t => new RecentTicketDto(t.Code, t.Service.Name, t.Status, t.Source, t.Counter != null ? t.Counter.Name : null, t.CreatedAt))
            .ToListAsync(ct);

        var waitSamples = perService.Where(s => s.AvgWait is not null).ToList();
        var avgWait = waitSamples.Count == 0 ? 0 : (int)waitSamples.Average(s => s.AvgWait!.Value);

        return new OverviewDto(tenant,
            services.Sum(s => s.Waiting), services.Sum(s => s.Active), services.Sum(s => s.Completed), services.Sum(s => s.NoShow),
            avgWait, openCounters, month, services, last7, recent);
    }
}
