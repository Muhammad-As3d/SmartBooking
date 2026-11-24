using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Web.Infrastructure;
using SmartBooking.Web.Models;

namespace SmartBooking.Web.Areas.App.Controllers;

[Authorize(Policy = Policies.TenantOwner)]
public class TeamController(TeamService team) : AppController
{
    [HttpGet("/app/team")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Members = await team.ListAsync(ct);
        return View(new StaffCreateModel());
    }

    [HttpPost("/app/team/add")]
    public async Task<IActionResult> Add(StaffCreateModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Members = await team.ListAsync(ct);
            return View(nameof(Index), model);
        }
        return Done(await team.AddStaffAsync(model.FullName, model.Email, model.Password, ct), "تمت إضافة الموظف.", nameof(Index));
    }

    [HttpPost("/app/team/{id:guid}/lock")]
    public async Task<IActionResult> Lock(Guid id, bool locked, CancellationToken ct) =>
        Done(await team.SetLockedAsync(id, locked, User.UserId(), ct), locked ? "تم إيقاف الحساب." : "تم تفعيل الحساب.", nameof(Index));
}
