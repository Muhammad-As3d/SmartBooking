using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartBooking.Core.Entities;
using SmartBooking.Core.Queue;
using SmartBooking.Infrastructure.Data;

namespace SmartBooking.Infrastructure.Notifications;

/// <summary>
/// التنبيه الذكي: كل 20 ثانية بيشوف مين فاضل على دوره أقل من (AlertLeadMinutes) ويبعتله إشعار يتحرك من بيته أو ينزل من عربيته.
/// كل تذكرة بتاخد تنبيه واحد بس، والـ claim بيتم في الداتابيز فينفع يشتغل على أكتر من سيرفر.
/// </summary>
public sealed class SmartAlertWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SmartAlertWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Smart alert cycle failed");
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<TicketAlertSender>();

        var now = clock.GetUtcNow().UtcDateTime;
        var minDate = DateOnly.FromDateTime(now.AddDays(-1));

        // الخدمات اللي فيها منتظرين لسه ما اتنبهوش (فهرس IX_Tickets_PendingAlerts)
        var services = await db.Tickets
            .Where(t => t.Status == TicketStatus.Waiting && t.AlertSentAt == null && t.ServiceDate >= minDate)
            .GroupBy(t => new { t.ServiceId, t.ServiceDate })
            .Select(g => new { g.Key.ServiceId, g.Key.ServiceDate })
            .ToListAsync(ct);

        foreach (var svc in services)
        {
            var info = await db.Services
                .Where(s => s.Id == svc.ServiceId)
                .Select(s => new
                {
                    s.AvgServiceSeconds,
                    LeadMinutes = db.Tenants.Where(t => t.Id == s.TenantId).Select(t => t.AlertLeadMinutes).FirstOrDefault(),
                    Open = s.Counters.Count(cs => cs.Counter.IsOpen && cs.Counter.IsActive),
                    InService = db.Tickets.Any(t => t.ServiceId == s.Id && t.ServiceDate == svc.ServiceDate
                                                    && (t.Status == TicketStatus.Called || t.Status == TicketStatus.InService)),
                })
                .FirstOrDefaultAsync(ct);
            if (info is null || info.Open == 0) continue; // مفيش حد بيستقبل، يبقى التوقع مالوش معنى

            var leadSeconds = info.LeadMinutes * 60;
            var maxAhead = Math.Max(0, (int)Math.Floor((double)leadSeconds * info.Open / Math.Max(30, info.AvgServiceSeconds)));

            var waiting = await db.Tickets
                .Where(t => t.ServiceId == svc.ServiceId && t.ServiceDate == svc.ServiceDate && t.Status == TicketStatus.Waiting)
                .OrderBy(t => t.Id)
                .Take(maxAhead + 1)
                .Select(t => new { t.Id, t.Code, t.AlertSentAt })
                .ToListAsync(ct);

            for (var ahead = 0; ahead < waiting.Count; ahead++)
            {
                var t = waiting[ahead];
                if (t.AlertSentAt is not null) continue;

                var eta = EtaCalculator.EstimateSeconds(ahead, info.AvgServiceSeconds, info.Open, info.InService);
                if (eta > leadSeconds) break;

                var claimed = await db.Tickets
                    .Where(x => x.Id == t.Id && x.AlertSentAt == null && x.Status == TicketStatus.Waiting)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.AlertSentAt, now), ct);
                if (claimed == 0) continue;

                var body = ahead == 0
                    ? $"تذكرتك {t.Code}: إنت التالي، خليك قريب."
                    : $"تذكرتك {t.Code}: باقي حوالي {EtaCalculator.FormatMinutes(eta)} على دورك. ابدأ اتحرك.";
                await sender.SendAsync(t.Id, "دورك قرّب", body, ct);
            }
        }
    }
}
