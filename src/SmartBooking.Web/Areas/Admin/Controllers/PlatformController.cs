using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBooking.Core;
using SmartBooking.Infrastructure.Dashboard;

namespace SmartBooking.Web.Areas.Admin.Controllers;

/// <summary>لوحة صاحب المنصة: المشتركين، الإيراد الشهري، تأكيد المدفوعات.</summary>
[Area("Admin")]
[Authorize(Policy = Policies.PlatformAdmin)]
public class PlatformController(BillingService billing) : Controller
{
    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await billing.GetAdminOverviewAsync(ct));

    [HttpPost("/admin/invoices/{id:guid}/paid")]
    public async Task<IActionResult> MarkPaid(Guid id, string? reference, CancellationToken ct) =>
        Back(await billing.MarkInvoicePaidAsync(id, reference, ct), "تم تأكيد الدفع وتمديد الاشتراك.");

    [HttpPost("/admin/tenants/{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, bool suspended, CancellationToken ct) =>
        Back(await billing.SetSuspendedAsync(id, suspended, ct), suspended ? "تم إيقاف المنشأة." : "تم تفعيل المنشأة.");

    [HttpPost("/admin/tenants/{id:guid}/trial")]
    public async Task<IActionResult> ExtendTrial(Guid id, int days, CancellationToken ct) =>
        Back(await billing.ExtendTrialAsync(id, Math.Clamp(days, 1, 60), ct), "تم تمديد الفترة التجريبية.");

    private IActionResult Back(OpResult result, string success)
    {
        if (result.Ok) TempData["flash"] = success;
        else TempData["error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }
}
