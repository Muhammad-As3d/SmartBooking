using SmartBooking.Core.Entities;

namespace SmartBooking.Core;

public static class Labels
{
    public static string Of(BusinessKind kind) => kind switch
    {
        BusinessKind.Clinic => "عيادة خاصة",
        BusinessKind.MedicalLab => "مختبر طبي",
        BusinessKind.ServiceOffice => "مكتب خدمات",
        _ => "منشأة أخرى",
    };

    public static string Of(TicketStatus status) => status switch
    {
        TicketStatus.Waiting => "في الانتظار",
        TicketStatus.Called => "تم النداء",
        TicketStatus.InService => "داخل الخدمة",
        TicketStatus.Completed => "انتهت",
        TicketStatus.NoShow => "لم يحضر",
        TicketStatus.Cancelled => "ملغاة",
        _ => "-",
    };

    public static string Of(TicketSource source) => source switch
    {
        TicketSource.QrCode => "QR",
        TicketSource.Link => "رابط",
        TicketSource.Reception => "الاستقبال",
        _ => "-",
    };

    public static string Of(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Pending => "بانتظار الدفع",
        InvoiceStatus.Paid => "مدفوعة",
        InvoiceStatus.Void => "ملغاة",
        _ => "-",
    };
}
