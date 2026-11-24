namespace SmartBooking.Core.Entities;

/// <summary>نقطة خدمة: عيادة دكتور، شباك، أو موظف استقبال.</summary>
public class Counter : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>مفتوح الآن ويستقبل؟ يدخل في حساب وقت الانتظار.</summary>
    public bool IsOpen { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<CounterService> Services { get; set; } = [];
}

public class CounterService
{
    public Guid CounterId { get; set; }
    public Counter Counter { get; set; } = default!;
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = default!;
}
