using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Web.Models;

namespace SmartBooking.Web.Controllers;

[Route("account")]
public class AccountController(
    SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, TenantOnboarding onboarding, IConfiguration config) : Controller
{
    [HttpGet("register")]
    public IActionResult Register(string? plan) => View(new RegisterModel());

    [HttpPost("register")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> Register(RegisterModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await onboarding.RegisterAsync(new RegisterTenantInput(
            model.BusinessName, model.Kind, model.Slug, model.City, model.TimeZoneId,
            model.OwnerName, model.Email, model.Phone, model.Password), ct);

        if (!result.Ok)
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }

        await signIn.SignInAsync(result.Value!, isPersistent: true);
        TempData["flash"] = "أهلاً بيك! المنشأة جاهزة، وعندك 14 يوم تجربة كاملة.";
        return RedirectToAction("Index", "Dashboard", new { area = "App" });
    }

    [HttpGet("slug-available")]
    public async Task<IActionResult> SlugAvailable(string slug, CancellationToken ct) =>
        Json(new { available = await onboarding.IsSlugAvailableAsync((slug ?? "").Trim().ToLowerInvariant(), ct) });

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl) => View(new LoginModel { ReturnUrl = returnUrl });

    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError("", "البريد أو كلمة المرور غير صحيحة.");
            return View(model);
        }

        await EnsurePlatformAdminAsync(user);

        var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "الحساب متوقف مؤقتاً بسبب محاولات كتير غلط. جرّب بعد 15 دقيقة.");
            return View(model);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "البريد أو كلمة المرور غير صحيحة.");
            return View(model);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return user.TenantId is null && await users.IsInRoleAsync(user, AppRoles.PlatformAdmin)
            ? RedirectToAction("Index", "Platform", new { area = "Admin" })
            : RedirectToAction("Index", "Dashboard", new { area = "App" });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return Redirect("/");
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();

    /// <summary>
    /// إنشاء أول حساب لمدير المنصة. متاحة بس لو مفيش مدير، وللبريد المكتوب في Platform:AdminEmails.
    /// </summary>
    [HttpGet("setup")]
    public async Task<IActionResult> Setup() =>
        await HasPlatformAdminAsync() ? NotFound() : View(new StaffCreateModel());

    [HttpPost("setup")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> Setup(StaffCreateModel model)
    {
        if (await HasPlatformAdminAsync()) return NotFound();
        var admins = config.GetSection("Platform:AdminEmails").Get<string[]>() ?? [];
        if (!admins.Contains(model.Email.Trim(), StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.Email), "البريد ده مش مضاف في Platform:AdminEmails في الإعدادات.");
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(), UserName = model.Email.Trim(), Email = model.Email.Trim(),
            FullName = model.FullName.Trim(), CreatedAt = DateTime.UtcNow, EmailConfirmed = true,
        };
        var created = await users.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            ModelState.AddModelError("", IdentityErrorsArabic.Describe(created));
            return View(model);
        }
        await users.AddToRoleAsync(user, AppRoles.PlatformAdmin);
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Platform", new { area = "Admin" });
    }

    private async Task<bool> HasPlatformAdminAsync() => (await users.GetUsersInRoleAsync(AppRoles.PlatformAdmin)).Count > 0;

    /// <summary>مدير المنصة بيتحدد من الإعدادات (Platform:AdminEmails) بدل ما يكون فيه صفحة تسجيل مكشوفة.</summary>
    private async Task EnsurePlatformAdminAsync(ApplicationUser user)
    {
        var admins = config.GetSection("Platform:AdminEmails").Get<string[]>() ?? [];
        if (user.TenantId is null
            && admins.Contains(user.Email, StringComparer.OrdinalIgnoreCase)
            && !await users.IsInRoleAsync(user, AppRoles.PlatformAdmin))
        {
            await users.AddToRoleAsync(user, AppRoles.PlatformAdmin);
        }
    }
}
