using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Entities;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Dashboard;

public sealed record SettingsForm(
    string Name, BusinessKind Kind, string? Phone, string? City, string? Address, string TimeZoneId, int AlertLeadMinutes, bool AcceptingTickets);

public sealed class SettingsService(AppDbContext db, ITenantContext tenantContext, TenantDirectory tenants)
{
    private Guid TenantId => tenantContext.TenantId ?? throw new InvalidOperationException("No tenant in context.");

    public Task<SettingsForm?> GetAsync(CancellationToken ct = default) =>
        db.Tenants.Where(t => t.Id == TenantId)
            .Select(t => new SettingsForm(t.Name, t.Kind, t.Phone, t.City, t.Address, t.TimeZoneId, t.AlertLeadMinutes, t.AcceptingTickets))
            .FirstOrDefaultAsync(ct);

    public async Task<OpResult> SaveAsync(SettingsForm form, CancellationToken ct = default)
    {
        if (TenantTime.Supported.All(z => z.Id != form.TimeZoneId)) return OpResult.Fail("المنطقة الزمنية غير مدعومة.");
        var lead = Math.Clamp(form.AlertLeadMinutes, 5, 60);

        var tenantId = TenantId;
        await db.Tenants.Where(t => t.Id == tenantId).ExecuteUpdateAsync(s => s
            .SetProperty(t => t.Name, form.Name.Trim())
            .SetProperty(t => t.Kind, form.Kind)
            .SetProperty(t => t.Phone, form.Phone)
            .SetProperty(t => t.City, form.City)
            .SetProperty(t => t.Address, form.Address)
            .SetProperty(t => t.TimeZoneId, form.TimeZoneId)
            .SetProperty(t => t.AlertLeadMinutes, lead)
            .SetProperty(t => t.AcceptingTickets, form.AcceptingTickets), ct);

        await InvalidateAsync(tenantId, ct);
        return OpResult.Success;
    }

    public async Task SetAcceptingAsync(bool accepting, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        await db.Tenants.Where(t => t.Id == tenantId).ExecuteUpdateAsync(s => s.SetProperty(t => t.AcceptingTickets, accepting), ct);
        await InvalidateAsync(tenantId, ct);
    }

    private async Task InvalidateAsync(Guid tenantId, CancellationToken ct)
    {
        var slug = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Slug).FirstAsync(ct);
        tenants.Invalidate(tenantId, slug);
    }
}
