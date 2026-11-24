using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using SmartBooking.Core;
using SmartBooking.Infrastructure;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Web.Hubs;
using SmartBooking.Web.Infrastructure;

// أرقام ثابتة (لاتيني، نقطة عشرية) في الـ HTML و SVG مهما كانت لغة السيرفر؛ التواريخ العربية بتتنسق صراحةً
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddSingleton<IQueueNotifier, SignalRQueueNotifier>();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireLowercase = true;
        o.Password.RequireDigit = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<AppClaimsFactory>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "dori.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/account/login";
    o.LogoutPath = "/account/logout";
    o.AccessDeniedPath = "/account/denied";
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
});

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "dori.af";
    o.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.TenantMember, p => p.RequireAuthenticatedUser().RequireClaim(AppClaims.TenantId))
    .AddPolicy(Policies.TenantOwner, p => p.RequireClaim(AppClaims.TenantId).RequireRole(AppRoles.Owner))
    .AddPolicy(Policies.PlatformAdmin, p => p.RequireRole(AppRoles.PlatformAdmin));

builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddSignalR(o =>
{
    o.MaximumReceiveMessageSize = 4 * 1024;
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // حجز التذاكر: 8 طلبات في الدقيقة لكل IP، كفاية لأي عميل حقيقي وبيقفل الباب قدام السبام
    o.AddPolicy(RateLimits.Booking, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(RateLimits.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    o.AddPolicy(RateLimits.Status, ctx => RateLimitPartition.GetTokenBucketLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        // سخي عن قصد: عملاء كتير في نفس الصالة ممكن يكونوا على نفس الـ Wi-Fi (نفس الـ IP)
        _ => new TokenBucketRateLimiterOptions { TokenLimit = 300, TokensPerPeriod = 150, ReplenishmentPeriod = TimeSpan.FromSeconds(10), QueueLimit = 0 }));
});

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.AddOutputCache(o => o.AddPolicy("landing", p => p.Expire(TimeSpan.FromMinutes(5)).Tag("landing")));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();
ViewHelpers.InitSprite(app.Environment);

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // الملفات اللي ليها ?v= (asp-append-version) تتخزن سنة؛ الباقي ساعة
        var versioned = ctx.Context.Request.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = versioned ? "public,max-age=31536000,immutable" : "public,max-age=3600";
    },
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapHub<QueueHub>("/hubs/queue");
app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// ملحوظة: الـ migrations مش بتتنفذ تلقائياً. نفّذها يدوياً: dotnet ef database update
app.Run();

public static class Policies
{
    public const string TenantMember = "TenantMember";
    public const string TenantOwner = "TenantOwner";
    public const string PlatformAdmin = "PlatformAdmin";
}

public static class RateLimits
{
    public const string Booking = "booking";
    public const string Auth = "auth";
    public const string Status = "status";
}
