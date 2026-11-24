using System.ComponentModel.DataAnnotations;
using SmartBooking.Core.Entities;

namespace SmartBooking.Web.Models;

public sealed class RegisterModel
{
    [Required(ErrorMessage = "اكتب اسم المنشأة"), StringLength(120, MinimumLength = 2)]
    public string BusinessName { get; set; } = "";

    public BusinessKind Kind { get; set; } = BusinessKind.Clinic;

    [Required(ErrorMessage = "اختار رابط للمنشأة"), StringLength(40, MinimumLength = 3, ErrorMessage = "الرابط من 3 لـ 40 حرف")]
    [RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "حروف إنجليزي وأرقام وشرطة فقط")]
    public string Slug { get; set; } = "";

    [StringLength(60)]
    public string? City { get; set; }

    public string TimeZoneId { get; set; } = "Africa/Cairo";

    [Required(ErrorMessage = "اكتب اسمك"), StringLength(120)]
    public string OwnerName { get; set; } = "";

    [Required(ErrorMessage = "اكتب البريد"), EmailAddress(ErrorMessage = "البريد غير صحيح"), StringLength(200)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "اكتب رقم الجوال"), StringLength(20, MinimumLength = 8, ErrorMessage = "رقم الجوال غير صحيح")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "اكتب كلمة المرور"), StringLength(100, MinimumLength = 8, ErrorMessage = "8 حروف على الأقل"), DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public sealed class LoginModel
{
    [Required(ErrorMessage = "اكتب البريد"), EmailAddress(ErrorMessage = "البريد غير صحيح")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "اكتب كلمة المرور"), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
}

public sealed class TakeTicketModel
{
    [Required(ErrorMessage = "اختار الخدمة")]
    public Guid ServiceId { get; set; }

    [StringLength(80)]
    public string? Name { get; set; }

    [StringLength(20)]
    [RegularExpression(@"^[+0-9٠-٩۰-۹\s-]{8,20}$", ErrorMessage = "رقم الجوال غير صحيح")]
    public string? Phone { get; set; }

    public string? Src { get; set; }
}

public sealed class ServiceEditModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "اكتب اسم الخدمة"), StringLength(80)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "اكتب الحرف المميز"), StringLength(2, MinimumLength = 1)]
    public string Prefix { get; set; } = "A";

    [StringLength(250)]
    public string? Description { get; set; }

    [Range(1, 240, ErrorMessage = "من 1 لـ 240 دقيقة")]
    public int AvgMinutes { get; set; } = 10;

    [Range(1, 2000, ErrorMessage = "من 1 لـ 2000")]
    public int? DailyLimit { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 999)]
    public int SortOrder { get; set; }
}

public sealed class CounterEditModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "اكتب الاسم"), StringLength(60)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    [Range(0, 999)]
    public int SortOrder { get; set; }

    public List<Guid> ServiceIds { get; set; } = [];
}

public sealed class StaffCreateModel
{
    [Required(ErrorMessage = "اكتب الاسم"), StringLength(120)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "اكتب البريد"), EmailAddress(ErrorMessage = "البريد غير صحيح")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "اكتب كلمة المرور"), StringLength(100, MinimumLength = 8, ErrorMessage = "8 حروف على الأقل"), DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public sealed class SettingsModel
{
    [Required(ErrorMessage = "اكتب اسم المنشأة"), StringLength(120)]
    public string Name { get; set; } = "";

    public BusinessKind Kind { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(60)]
    public string? City { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    public string TimeZoneId { get; set; } = "Africa/Cairo";

    [Range(5, 60, ErrorMessage = "من 5 لـ 60 دقيقة")]
    public int AlertLeadMinutes { get; set; } = 15;

    public bool AcceptingTickets { get; set; } = true;
}

public sealed class ReceptionTicketModel
{
    [Required]
    public Guid ServiceId { get; set; }

    [StringLength(80)]
    public string? Name { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    public Guid? ReturnCounterId { get; set; }
}

public sealed class PushSubscriptionModel
{
    [Required, StringLength(600)]
    public string Endpoint { get; set; } = "";

    [Required, StringLength(120)]
    public string P256dh { get; set; } = "";

    [Required, StringLength(60)]
    public string Auth { get; set; } = "";
}
