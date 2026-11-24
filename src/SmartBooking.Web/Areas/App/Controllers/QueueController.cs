using Microsoft.AspNetCore.Mvc;
using SmartBooking.Core;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Infrastructure.Queue;
using SmartBooking.Web.Models;

namespace SmartBooking.Web.Areas.App.Controllers;

/// <summary>شاشة موظف الشباك / الدكتور.</summary>
public class QueueController(QueueOpsService ops, CatalogService catalog) : AppController
{
    [HttpGet("/app/queue")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Services = await catalog.ServiceOptionsAsync(ct);
        return View(await ops.GetCountersAsync(ct));
    }

    [HttpGet("/app/queue/{id:guid}")]
    public async Task<IActionResult> Counter(Guid id, CancellationToken ct)
    {
        var model = await ops.GetConsoleAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("/app/queue/{id:guid}/panel")]
    public async Task<IActionResult> Panel(Guid id, CancellationToken ct)
    {
        var model = await ops.GetConsoleAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        return model is null ? NotFound() : PartialView("_CounterPanel", model);
    }

    [HttpPost("/app/queue/{id:guid}/next")]
    public async Task<IActionResult> Next(Guid id, CancellationToken ct)
    {
        var result = await ops.CallNextAsync(id, ct);
        if (!result.Ok) TempData["error"] = result.Error;
        else if (result.Value is null) TempData["flash"] = "مفيش حد منتظر دلوقتي.";
        return RedirectToAction(nameof(Counter), new { id });
    }

    [HttpPost("/app/queue/{id:guid}/start/{ticketId:long}")]
    public async Task<IActionResult> Start(Guid id, long ticketId, CancellationToken ct) =>
        Back(await ops.StartAsync(ticketId, ct), id);

    [HttpPost("/app/queue/{id:guid}/complete/{ticketId:long}")]
    public async Task<IActionResult> Complete(Guid id, long ticketId, CancellationToken ct) =>
        Back(await ops.CompleteAsync(ticketId, ct), id);

    [HttpPost("/app/queue/{id:guid}/noshow/{ticketId:long}")]
    public async Task<IActionResult> NoShow(Guid id, long ticketId, CancellationToken ct) =>
        Back(await ops.NoShowAsync(ticketId, ct), id);

    [HttpPost("/app/queue/{id:guid}/recall/{ticketId:long}")]
    public async Task<IActionResult> Recall(Guid id, long ticketId, CancellationToken ct) =>
        Back(await ops.RecallAsync(ticketId, ct), id);

    [HttpPost("/app/queue/{id:guid}/open")]
    public async Task<IActionResult> SetOpen(Guid id, bool open, CancellationToken ct) =>
        Back(await ops.SetOpenAsync(id, open, ct), id);

    /// <summary>تذكرة من الاستقبال لعميل معندوش موبايل.</summary>
    [HttpPost("/app/queue/issue")]
    public async Task<IActionResult> Issue(ReceptionTicketModel model, CancellationToken ct)
    {
        var result = await ops.IssueAtReceptionAsync(model.ServiceId, model.Name, model.Phone, ct);
        if (result.Ok) TempData["flash"] = $"تم إصدار التذكرة {result.Value!.Code}";
        else TempData["error"] = result.Error;

        return model.ReturnCounterId is { } cid
            ? RedirectToAction(nameof(Counter), new { id = cid })
            : RedirectToAction(nameof(Index));
    }

    private IActionResult Back(OpResult result, Guid counterId)
    {
        if (!result.Ok) TempData["error"] = result.Error;
        return RedirectToAction(nameof(Counter), new { id = counterId });
    }
}
