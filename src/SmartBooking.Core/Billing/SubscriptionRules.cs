using SmartBooking.Core.Entities;

namespace SmartBooking.Core.Billing;

public static class SubscriptionRules
{
    public const int TrialDays = 14;
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(3);

    public static bool IsUsable(SubscriptionStatus status, DateTime? trialEndsAt, DateTime? periodEnd, bool suspended, DateTime utcNow)
    {
        if (suspended) return false;
        return status switch
        {
            SubscriptionStatus.Trial => trialEndsAt > utcNow,
            SubscriptionStatus.Active or SubscriptionStatus.PastDue => periodEnd + GracePeriod > utcNow,
            _ => false,
        };
    }

    public static DateTime ExtendPeriod(DateTime? currentEnd, int months, DateTime utcNow)
    {
        var from = currentEnd is { } end && end > utcNow ? end : utcNow;
        return from.AddMonths(months);
    }

    public static string StatusLabel(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Trial => "فترة تجريبية",
        SubscriptionStatus.Active => "فعّال",
        SubscriptionStatus.PastDue => "متأخر السداد",
        SubscriptionStatus.Cancelled => "ملغي",
        _ => "-",
    };
}
