namespace SmartBooking.Core;

/// <summary>المنشأة الحالية لطلب لوحة التحكم (من الـ claim). null للزائر أو مدير المنصة.</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}

/// <summary>بيبلغ الشاشات والتذاكر المفتوحة بأي تغيير لحظياً (التنفيذ في طبقة الويب عبر SignalR).</summary>
public interface IQueueNotifier
{
    Task QueueChangedAsync(Guid tenantId, Guid serviceId, CancellationToken ct = default);
    Task TicketCalledAsync(Guid tenantId, Guid serviceId, string ticketToken, string code, string counterName, CancellationToken ct = default);
    Task TicketAlertAsync(string ticketToken, string title, string body, CancellationToken ct = default);
}

public readonly record struct OpResult(bool Ok, string? Error = null)
{
    public static OpResult Success => new(true);
    public static OpResult Fail(string error) => new(false, error);
}

public readonly record struct OpResult<T>(bool Ok, T? Value, string? Error = null)
{
    public static OpResult<T> Success(T value) => new(true, value);
    public static OpResult<T> Fail(string error) => new(false, default, error);
}
