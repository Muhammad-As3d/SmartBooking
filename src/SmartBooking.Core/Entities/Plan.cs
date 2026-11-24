namespace SmartBooking.Core.Entities;

public class Plan
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "EGP";
    public int MaxServices { get; set; }
    public int MaxCounters { get; set; }
    public int MaxStaff { get; set; }
    public int MaxTicketsPerMonth { get; set; }
    public bool HasDisplayScreen { get; set; }
    public bool HasPushAlerts { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
