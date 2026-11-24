namespace SmartBooking.Core.Entities;

/// <summary>خدمة يحجز لها العميل دور (كشف عام، سحب عينة، تجديد إقامة...).</summary>
public class Service : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>حرف يسبق رقم التذكرة، مثل A في A-014.</summary>
    public string Prefix { get; set; } = "A";

    /// <summary>
    /// متوسط مدة خدمة العميل الواحد بالثواني. يتحدث تلقائياً (متوسط متحرك) مع كل تذكرة تنتهي.
    /// </summary>
    public int AvgServiceSeconds { get; set; } = 600;

    public int? DailyLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<CounterService> Counters { get; set; } = [];
}
