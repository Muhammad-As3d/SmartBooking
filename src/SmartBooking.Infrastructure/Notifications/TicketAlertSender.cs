using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartBooking.Core;
using SmartBooking.Infrastructure.Data;
using WebPush;

namespace SmartBooking.Infrastructure.Notifications;

/// <summary>يبعت تنبيه للتذكرة: لحظياً على الصفحة المفتوحة (SignalR) + إشعار للجوال حتى لو الصفحة مقفولة (Web Push).</summary>
public sealed class TicketAlertSender(
    AppDbContext db, IQueueNotifier notifier, WebPushKeys keys, IHttpClientFactory httpFactory, ILogger<TicketAlertSender> logger)
{
    public async Task SendAsync(long ticketId, string title, string body, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.IgnoreQueryFilters()
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                t.PublicToken,
                PushAllowed = db.Tenants.Where(x => x.Id == t.TenantId).Select(x => x.Plan.HasPushAlerts).FirstOrDefault(),
                Subs = t.PushSubscriptions.Select(p => new { p.Id, p.Endpoint, p.P256dh, p.Auth }).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (ticket is null) return;

        await notifier.TicketAlertAsync(ticket.PublicToken, title, body, ct);
        if (!ticket.PushAllowed || ticket.Subs.Count == 0) return;

        var payload = JsonSerializer.Serialize(new { title, body, url = "/t/" + ticket.PublicToken, tag = ticket.PublicToken });
        using var client = new WebPushClient(httpFactory.CreateClient("webpush"));
        var dead = new List<long>();

        foreach (var s in ticket.Subs)
        {
            try
            {
                await client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, keys.Details, ct);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                dead.Add(s.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Web push failed for ticket {TicketId}", ticketId);
            }
        }

        if (dead.Count > 0)
            await db.TicketPushSubscriptions.Where(p => dead.Contains(p.Id)).ExecuteDeleteAsync(ct);
    }
}
