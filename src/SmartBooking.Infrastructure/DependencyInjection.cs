using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartBooking.Infrastructure.Dashboard;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Notifications;
using SmartBooking.Infrastructure.Queue;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");

        services.AddDbContext<AppDbContext>(o => o
            .UseSqlServer(cs, sql => sql.CommandTimeout(15))
            // القراءة هي الغالبة، فالـ tracking مقفول افتراضياً، ومفتوح بس لما نحتاج نعدّل كيانات
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddMemoryCache();
        services.AddHttpClient("webpush", c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WebPushKeys>();

        services.AddScoped<TenantDirectory>();
        services.AddScoped<TicketIssuer>();
        services.AddScoped<PublicQueueService>();
        services.AddScoped<QueueOpsService>();
        services.AddScoped<TicketAlertSender>();
        services.AddScoped<CatalogService>();
        services.AddScoped<StatsService>();
        services.AddScoped<TenantOnboarding>();
        services.AddScoped<TeamService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<BillingService>();

        services.AddHostedService<SmartAlertWorker>();
        return services;
    }
}
