using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SmartBooking.Infrastructure.Dashboard;

namespace SmartBooking.Web.Controllers;

public class HomeController(BillingService billing, ILogger<HomeController> logger) : Controller
{
    [HttpGet("/")]
    [OutputCache(PolicyName = "landing")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // صفحة التسويق لازم تفضل شغالة حتى لو الداتابيز واقعة؛ ساعتها بتظهر من غير جدول الأسعار
        try
        {
            return View(await billing.GetPlansAsync(ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not load plans for landing page");
            if (HttpContext.Features.Get<IOutputCacheFeature>() is { } cache) cache.Context.AllowCacheStorage = false;
            return View(new List<PlanDto>());
        }
    }

    [Route("/error/{code:int?}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        var status = code ?? 500;
        Response.StatusCode = status;
        return View(status);
    }
}
