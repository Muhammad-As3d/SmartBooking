using System.Security.Claims;
using SmartBooking.Core;
using SmartBooking.Infrastructure.Data;

namespace SmartBooking.Web.Infrastructure;

/// <summary>
/// المنشأة الحالية بتيجي من الـ cookie الموقّعة بس، مش من الرابط أو الفورم، فمستحيل مستخدم يشوف بيانات منشأة تانية.
/// القيمة بتتقرأ في كل مرة ومش بتتخزن: الـ DbContext ممكن يتعمل أثناء الـ authentication نفسه
/// (SecurityStampValidator) قبل ما HttpContext.User يتملى، ولو خزّناها ساعتها هتفضل فاضية طول الطلب.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? TenantId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(AppClaims.TenantId), out var id) ? id : null;
}

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException());

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(AppClaims.FullName) ?? user.Identity?.Name ?? "";
}
