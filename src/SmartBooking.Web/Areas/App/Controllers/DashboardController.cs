using Microsoft.AspNetCore.Mvc;
using SmartBooking.Infrastructure.Dashboard;

namespace SmartBooking.Web.Areas.App.Controllers;

public class DashboardController(StatsService stats) : AppController
{
    [HttpGet("/app")]
    [HttpGet("/app/dashboard")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await stats.GetOverviewAsync(ct);
        return model is null ? Forbid() : View(model);
    }

    /// <summary>جزء الأرقام الحية، بيتحدث لوحده لما يوصل حدث من SignalR.</summary>
    [HttpGet]
    public async Task<IActionResult> Live(CancellationToken ct)
    {
        var model = await stats.GetOverviewAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return model is null ? Forbid() : PartialView("_Live", model);
    }
}
