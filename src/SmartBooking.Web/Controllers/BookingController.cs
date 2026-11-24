using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;
using SmartBooking.Core.Entities;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Infrastructure.Notifications;
using SmartBooking.Infrastructure.Queue;
using SmartBooking.Web.Models;

namespace SmartBooking.Web.Controllers;

/// <summary>
/// الجزء اللي العميل بيشوفه من غير تطبيق ولا تسجيل:
/// /q/{slug} صفحة الحجز (اللي الـ QR بيفتحها)، /t/{token} التذكرة الحية، /d/{slug} شاشة العرض في الصالة.
/// </summary>
public class BookingController(PublicQueueService queue, WebPushKeys pushKeys) : Controller
{
    [HttpGet("/q/{slug}")]
    public async Task<IActionResult> Book(string slug, string? src, CancellationToken ct)
    {
        var page = await queue.GetBookingPageAsync(slug, ct);
        if (page is null) return NotFound();
        ViewBag.Source = src == "qr" ? "qr" : "link";
        return View(page);
    }

    [HttpPost("/q/{slug}")]
    [EnableRateLimiting(RateLimits.Booking)]
    public async Task<IActionResult> Book(string slug, TakeTicketModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["error"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "بيانات غير صحيحة.";
            return RedirectToAction(nameof(Book), new { slug });
        }

        var source = model.Src == "qr" ? TicketSource.QrCode : TicketSource.Link;
        var result = await queue.TakeTicketAsync(slug, model.ServiceId, model.Name, model.Phone, source, ct);
        if (!result.Ok)
        {
            TempData["error"] = result.Error;
            return RedirectToAction(nameof(Book), new { slug });
        }

        if (result.Value!.Existing) TempData["flash"] = "عندك تذكرة نشطة بالفعل لنفس الخدمة، دي هي.";
        return RedirectToAction(nameof(Ticket), new { token = result.Value.Token });
    }

    [HttpGet("/t/{token}")]
    public async Task<IActionResult> Ticket(string token, CancellationToken ct)
    {
        var ticket = await queue.GetTicketAsync(token, ct);
        if (ticket is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        ViewBag.VapidKey = pushKeys.PublicKey;
        return View(ticket);
    }

    [HttpGet("/t/{token}/status")]
    [EnableRateLimiting(RateLimits.Status)]
    public async Task<IActionResult> TicketStatus(string token, CancellationToken ct)
    {
        var ticket = await queue.GetTicketAsync(token, ct);
        if (ticket is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Json(new
        {
            ticket.Code,
            status = ticket.Status.ToString(),
            ticket.StatusLabel,
            ticket.Ahead,
            ticket.EtaSeconds,
            ticket.EtaLabel,
            ticket.CounterName,
        });
    }

    [HttpPost("/t/{token}/cancel")]
    [EnableRateLimiting(RateLimits.Booking)]
    public async Task<IActionResult> Cancel(string token, CancellationToken ct)
    {
        await queue.CancelAsync(token, ct);
        return RedirectToAction(nameof(Ticket), new { token });
    }

    [HttpPost("/t/{token}/push")]
    [EnableRateLimiting(RateLimits.Booking)]
    public async Task<IActionResult> SubscribePush(string token, [FromBody] PushSubscriptionModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var ok = await queue.SavePushSubscriptionAsync(token, model.Endpoint, model.P256dh, model.Auth, ct);
        return ok ? NoContent() : BadRequest();
    }

    [HttpGet("/d/{slug}")]
    public async Task<IActionResult> Display(string slug, CancellationToken ct)
    {
        var display = await queue.GetDisplayAsync(slug, ct);
        if (display is null) return NotFound();
        if (!display.Tenant.HasDisplayScreen) return View("DisplayLocked", display.Tenant);
        return View(display);
    }

    [HttpGet("/d/{slug}/state")]
    [EnableRateLimiting(RateLimits.Status)]
    public async Task<IActionResult> DisplayState(string slug, CancellationToken ct)
    {
        var display = await queue.GetDisplayAsync(slug, ct);
        if (display is null || !display.Tenant.HasDisplayScreen) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PartialView("_DisplayBoard", display);
    }

    /// <summary>كود QR بصيغة SVG (خفيف ويتطبع بأي مقاس).</summary>
    [HttpGet("/q/{slug}/qr.svg")]
    [ResponseCache(Duration = 86400)]
    public IActionResult Qr(string slug, string? fg)
    {
        if (!TenantOnboarding.IsValidSlug(slug)) return NotFound();
        var url = Url.Action(nameof(Book), "Booking", new { slug, src = "qr" }, Request.Scheme)!;
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(data).GetGraphic(10, fg == "dark" ? "#0B0C0F" : "#E8B53E", fg == "dark" ? "#FFFFFF" : "#0B0C0F", drawQuietZones: true);
        return Content(svg, "image/svg+xml");
    }
}
