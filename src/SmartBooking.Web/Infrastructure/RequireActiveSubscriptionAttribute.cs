using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartBooking.Core;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Web.Infrastructure;

/// <summary>لو الاشتراك منتهي، أي صفحة في لوحة التحكم بتحوّل على صفحة الاشتراك (ماعدا الصفحات المعلّمة بـ AllowExpiredSubscription).</summary>
public sealed class RequireActiveSubscriptionAttribute : TypeFilterAttribute
{
    public RequireActiveSubscriptionAttribute() : base(typeof(Filter)) { }

    private sealed class Filter(ITenantContext tenantContext, TenantDirectory tenants, TimeProvider clock) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowExpiredSubscriptionAttribute>().Any()
                || tenantContext.TenantId is not { } tenantId)
            {
                await next();
                return;
            }

            var tenant = await tenants.ByIdAsync(tenantId, context.HttpContext.RequestAborted);
            if (tenant is null || !tenant.IsUsable(clock.GetUtcNow().UtcDateTime))
            {
                context.Result = new RedirectToActionResult("Index", "Billing", new { area = "App", expired = true });
                return;
            }
            await next();
        }
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowExpiredSubscriptionAttribute : Attribute;
