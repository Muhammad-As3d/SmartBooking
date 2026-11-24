using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Web.Models;

namespace SmartBooking.Web.Areas.App.Controllers;

[Authorize(Policy = Policies.TenantOwner)]
public class CountersController(CatalogService catalog) : AppController
{
    [HttpGet("/app/counters")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await catalog.ListCountersAsync(ct));

    [HttpGet("/app/counters/new")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Services = await catalog.ServiceOptionsAsync(ct);
        return View("Edit", new CounterEditModel());
    }

    [HttpGet("/app/counters/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var c = await catalog.GetCounterAsync(id, ct);
        if (c is null) return NotFound();
        ViewBag.Services = await catalog.ServiceOptionsAsync(ct);
        return View(new CounterEditModel { Id = c.Id, Name = c.Name, IsActive = c.IsActive, SortOrder = c.SortOrder, ServiceIds = [.. c.ServiceIds] });
    }

    [HttpPost("/app/counters/save")]
    public async Task<IActionResult> Save(CounterEditModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await catalog.SaveCounterAsync(new CounterForm(model.Id, model.Name, model.IsActive, model.SortOrder, model.ServiceIds), ct);
            if (result.Ok)
            {
                TempData["flash"] = "تم الحفظ.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", result.Error!);
        }
        ViewBag.Services = await catalog.ServiceOptionsAsync(ct);
        return View("Edit", model);
    }
}
