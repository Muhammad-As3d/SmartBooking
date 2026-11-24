using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Infrastructure.Tenancy;
using SmartBooking.Web.Models;

namespace SmartBooking.Web.Areas.App.Controllers;

public class SettingsController(SettingsService settings, TenantDirectory tenants, SmartBooking.Core.ITenantContext tenantContext) : AppController
{
    [HttpGet("/app/settings")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        if (s is null) return NotFound();
        return View(new SettingsModel
        {
            Name = s.Name, Kind = s.Kind, Phone = s.Phone, City = s.City, Address = s.Address,
            TimeZoneId = s.TimeZoneId, AlertLeadMinutes = s.AlertLeadMinutes, AcceptingTickets = s.AcceptingTickets,
        });
    }

    [HttpPost("/app/settings")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<IActionResult> Index(SettingsModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await settings.SaveAsync(new SettingsForm(model.Name, model.Kind, model.Phone, model.City, model.Address,
            model.TimeZoneId, model.AlertLeadMinutes, model.AcceptingTickets), ct);
        return Done(result, "تم حفظ الإعدادات.", nameof(Index));
    }

    /// <summary>بوستر الـ QR للطباعة وتعليقه على باب العيادة.</summary>
    [HttpGet("/app/settings/qr")]
    public async Task<IActionResult> Qr(CancellationToken ct)
    {
        var tenant = await tenants.ByIdAsync(tenantContext.TenantId!.Value, ct);
        return tenant is null ? NotFound() : View(tenant);
    }

    [HttpPost("/app/settings/accepting")]
    public async Task<IActionResult> Accepting(bool accepting, string? returnUrl, CancellationToken ct)
    {
        await settings.SetAcceptingAsync(accepting, ct);
        TempData["flash"] = accepting ? "الحجز مفتوح للعملاء." : "تم إيقاف استقبال حجوزات جديدة.";
        return returnUrl is not null && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Dashboard");
    }
}
