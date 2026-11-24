using Microsoft.AspNetCore.SignalR;
using SmartBooking.Core;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Queue;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Web.Hubs;

/// <summary>
/// قناة WebSockets واحدة لكل الشاشات. كل عميل بينضم لمجموعة بحسب دوره:
/// تذكرة (ticket:token + svc:serviceId)، شاشة عرض (display:tenant)، لوحة تحكم (tenant:id).
/// </summary>
public sealed class QueueHub(PublicQueueService queue, TenantDirectory tenants) : Hub
{
    public async Task<bool> WatchTicket(string token)
    {
        var ticket = await queue.GetTicketAsync(token);
        if (ticket is null) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroups.Ticket(token));
        await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroups.Service(ticket.ServiceId));
        return true;
    }

    public async Task<bool> WatchDisplay(string slug)
    {
        var tenant = await tenants.BySlugAsync(slug);
        if (tenant is null) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroups.Display(tenant.Id));
        return true;
    }

    public async Task<bool> WatchBooking(string slug)
    {
        var tenant = await tenants.BySlugAsync(slug);
        if (tenant is null) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroups.Display(tenant.Id));
        return true;
    }

    public async Task<bool> WatchDashboard()
    {
        var claim = Context.User?.FindFirst(AppClaims.TenantId)?.Value;
        if (!Guid.TryParse(claim, out var tenantId)) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroups.Tenant(tenantId));
        return true;
    }
}

public static class QueueGroups
{
    public static string Ticket(string token) => "ticket:" + token;
    public static string Service(Guid id) => "svc:" + id.ToString("N");
    public static string Display(Guid tenantId) => "display:" + tenantId.ToString("N");
    public static string Tenant(Guid tenantId) => "tenant:" + tenantId.ToString("N");
}

public sealed class SignalRQueueNotifier(IHubContext<QueueHub> hub) : IQueueNotifier
{
    public Task QueueChangedAsync(Guid tenantId, Guid serviceId, CancellationToken ct = default) =>
        hub.Clients.Groups(QueueGroups.Service(serviceId), QueueGroups.Tenant(tenantId), QueueGroups.Display(tenantId))
            .SendAsync("queueChanged", serviceId.ToString(), ct);

    public Task TicketCalledAsync(Guid tenantId, Guid serviceId, string ticketToken, string code, string counterName, CancellationToken ct = default) =>
        hub.Clients.Groups(QueueGroups.Display(tenantId), QueueGroups.Tenant(tenantId), QueueGroups.Ticket(ticketToken))
            .SendAsync("ticketCalled", new { code, counter = counterName }, ct);

    public Task TicketAlertAsync(string ticketToken, string title, string body, CancellationToken ct = default) =>
        hub.Clients.Group(QueueGroups.Ticket(ticketToken)).SendAsync("alert", new { title, body }, ct);
}
