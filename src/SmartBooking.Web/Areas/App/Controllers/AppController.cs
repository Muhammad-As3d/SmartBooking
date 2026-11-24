using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBooking.Core;
using SmartBooking.Web.Infrastructure;

namespace SmartBooking.Web.Areas.App.Controllers;

[Area("App")]
[Route("app/[controller]/[action]/{id?}")]
[Authorize(Policy = Policies.TenantMember)]
[RequireActiveSubscription]
public abstract class AppController : Controller
{
    protected IActionResult Done(OpResult result, string success, string action, object? routeValues = null)
    {
        if (result.Ok) TempData["flash"] = success;
        else TempData["error"] = result.Error;
        return RedirectToAction(action, routeValues);
    }
}
