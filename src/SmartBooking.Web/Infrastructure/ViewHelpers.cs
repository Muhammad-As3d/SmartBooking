using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartBooking.Core.Entities;

namespace SmartBooking.Web.Infrastructure;

public static class ViewHelpers
{
    /// <summary>رابط الأيقونات بـ hash المحتوى، عشان المتصفح ياخد النسخة الجديدة أول ما الملف يتغير (والقديمة متكاشة سنة).</summary>
    public static string SpriteUrl { get; private set; } = "/img/icons.svg";

    public static void InitSprite(IWebHostEnvironment env)
    {
        var file = env.WebRootFileProvider.GetFileInfo("img/icons.svg");
        if (!file.Exists) return;
        using var stream = file.CreateReadStream();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))[..10].ToLowerInvariant();
        SpriteUrl = "/img/icons.svg?v=" + hash;
    }

    public static IHtmlContent Icon(this IHtmlHelper _, string name, string? css = null) =>
        new HtmlString($"<svg class=\"icon {css}\" aria-hidden=\"true\"><use href=\"{SpriteUrl}#{name}\"></use></svg>");

    public static string KindIcon(BusinessKind kind) => kind switch
    {
        BusinessKind.Clinic => "clinic",
        BusinessKind.MedicalLab => "lab",
        BusinessKind.ServiceOffice => "desk",
        _ => "ticket",
    };

    public static string Money(decimal amount, string currency) =>
        amount.ToString("#,0.##", CultureInfo.InvariantCulture) + " " + Currency(currency);

    public static string Currency(string code) => code switch
    {
        "EGP" => "ج.م",
        "SAR" => "ر.س",
        "AED" => "د.إ",
        _ => code,
    };

    public static string Minutes(int seconds) => ((int)Math.Ceiling(seconds / 60.0)).ToString(CultureInfo.InvariantCulture);

    public static string StatusTagClass(TicketStatus status) => status switch
    {
        TicketStatus.Waiting => "",
        TicketStatus.Called or TicketStatus.InService => "amber",
        TicketStatus.Completed => "ok",
        _ => "danger",
    };

    public static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("ar-EG");

    public static string Date(DateTime value) => value.ToString("d MMM yyyy", Ar);

    public static string Time(DateTime utc, string timeZoneId) =>
        SmartBooking.Infrastructure.Tenancy.TenantTime.ToLocal(timeZoneId, utc).ToString("h:mm tt", Ar);

    public static string LocalDate(DateTime utc, string timeZoneId) =>
        Date(SmartBooking.Infrastructure.Tenancy.TenantTime.ToLocal(timeZoneId, utc));
}
