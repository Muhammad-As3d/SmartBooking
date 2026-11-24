namespace SmartBooking.Core.Entities;

/// <summary>
/// منشأة مشتركة في المنصة (عيادة، مختبر، مكتب خدمات). كل بيانات النظام معزولة بـ TenantId.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public BusinessKind Kind { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }

    /// <summary>IANA time zone, e.g. Africa/Cairo or Asia/Riyadh.</summary>
    public string TimeZoneId { get; set; } = "Africa/Cairo";

    /// <summary>كم دقيقة قبل الدور يوصل التنبيه الذكي.</summary>
    public int AlertLeadMinutes { get; set; } = 15;

    public bool AcceptingTickets { get; set; } = true;
    public bool IsSuspended { get; set; }
    public DateTime CreatedAt { get; set; }

    public int PlanId { get; set; }
    public Plan Plan { get; set; } = default!;
    public SubscriptionStatus SubscriptionStatus { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    public ICollection<Service> Services { get; set; } = [];
    public ICollection<Counter> Counters { get; set; } = [];
}

public enum BusinessKind
{
    Clinic = 0,
    MedicalLab = 1,
    ServiceOffice = 2,
    Other = 9,
}

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Cancelled = 3,
}
