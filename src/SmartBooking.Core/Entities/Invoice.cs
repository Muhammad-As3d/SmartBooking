namespace SmartBooking.Core.Entities;

public class Invoice : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = default!;
    public string Number { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public int Months { get; set; } = 1;
    public InvoiceStatus Status { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public enum InvoiceStatus : byte
{
    Pending = 0,
    Paid = 1,
    Void = 2,
}

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
