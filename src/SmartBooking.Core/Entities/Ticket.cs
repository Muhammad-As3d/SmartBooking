namespace SmartBooking.Core.Entities;

/// <summary>تذكرة رقمية حية في جوال العميل.</summary>
public class Ticket : ITenantOwned
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = default!;

    /// <summary>يوم الخدمة بتوقيت المنشأة. الترقيم يبدأ من 1 كل يوم لكل خدمة.</summary>
    public DateOnly ServiceDate { get; set; }

    public int Number { get; set; }
    public string Code { get; set; } = default!;

    /// <summary>رمز عشوائي غير قابل للتخمين يُستخدم في رابط التذكرة.</summary>
    public string PublicToken { get; set; } = default!;

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public TicketSource Source { get; set; }
    public TicketStatus Status { get; set; }

    public Guid? CounterId { get; set; }
    public Counter? Counter { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? ServiceStartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? AlertSentAt { get; set; }
    public int RecallCount { get; set; }

    public ICollection<TicketPushSubscription> PushSubscriptions { get; set; } = [];
}

public enum TicketStatus : byte
{
    Waiting = 0,
    Called = 1,
    InService = 2,
    Completed = 3,
    NoShow = 4,
    Cancelled = 5,
}

public enum TicketSource : byte
{
    QrCode = 0,
    Link = 1,
    Reception = 2,
}

public class TicketPushSubscription
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string Endpoint { get; set; } = default!;
    public string P256dh { get; set; } = default!;
    public string Auth { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>عداد يومي لكل خدمة، يتزود ذرياً (atomic) عشان ما يطلع رقمين متشابهين في نفس الثانية.</summary>
public class DailySequence
{
    public Guid ServiceId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public int LastNumber { get; set; }
}
