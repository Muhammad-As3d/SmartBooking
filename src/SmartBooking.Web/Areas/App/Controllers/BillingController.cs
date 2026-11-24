using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Web.Infrastructure;

namespace SmartBooking.Web.Areas.App.Controllers;

[AllowExpiredSubscription]
public class BillingController(BillingService billing) : AppController
{
    [HttpGet("/app/billing")]
    public async Task<IActionResult> Index(bool expired, CancellationToken ct)
    {
        var model = await billing.GetAsync(ct);
        if (model is null) return NotFound();
        ViewBag.Expired = expired || !model.Usable;
        return View(model);
    }

    [HttpPost("/app/billing/subscribe")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<IActionResult> Subscribe(int planId, int months, CancellationToken ct)
    {
        var result = await billing.CreateInvoiceAsync(planId, months, ct);
        if (result.Ok) TempData["flash"] = "تم إصدار الفاتورة. هيتفعّل الاشتراك أول ما الدفع يتأكد.";
        else TempData["error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }
}
