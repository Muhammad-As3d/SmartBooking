using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Web.Models;

namespace SmartBooking.Web.Areas.App.Controllers;

[Authorize(Policy = Policies.TenantOwner)]
public class ServicesController(CatalogService catalog) : AppController
{
    [HttpGet("/app/services")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await catalog.ListServicesAsync(ct));

    [HttpGet("/app/services/new")]
    public IActionResult Create() => View("Edit", new ServiceEditModel());

    [HttpGet("/app/services/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var s = await catalog.GetServiceAsync(id, ct);
        if (s is null) return NotFound();
        return View(new ServiceEditModel
        {
            Id = s.Id, Name = s.Name, Prefix = s.Prefix, Description = s.Description, AvgMinutes = s.AvgMinutes,
            DailyLimit = s.DailyLimit, IsActive = s.IsActive, SortOrder = s.SortOrder,
        });
    }

    [HttpPost("/app/services/save")]
    public async Task<IActionResult> Save(ServiceEditModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", model);
        var result = await catalog.SaveServiceAsync(new ServiceForm(model.Id, model.Name, model.Prefix, model.Description,
            model.AvgMinutes, model.DailyLimit, model.IsActive, model.SortOrder), ct);
        if (!result.Ok)
        {
            ModelState.AddModelError("", result.Error!);
            return View("Edit", model);
        }
        TempData["flash"] = "تم حفظ الخدمة.";
        return RedirectToAction(nameof(Index));
    }
}
