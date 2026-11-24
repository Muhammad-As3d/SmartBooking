using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Entities;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Queue;

public sealed record IssuedTicket(string Token, string Code, bool Existing);

/// <summary>
/// إصدار التذاكر. ده قلب منع التضارب: الرقم بيتحجز ذرياً من DailySequences جوه نفس الـ transaction،
/// ولو اتنين ضغطوا في نفس الملّي ثانية كل واحد بياخد رقم مختلف.
/// </summary>
public sealed class TicketIssuer(AppDbContext db, IQueueNotifier notifier, TimeProvider clock)
{
    public async Task<OpResult<IssuedTicket>> IssueAsync(
        TenantSnapshot tenant, Guid serviceId, string? name, string? phone, TicketSource source, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (!tenant.IsUsable(now))
            return OpResult<IssuedTicket>.Fail("الحجز متوقف مؤقتاً لهذه المنشأة.");
        if (!tenant.AcceptingTickets && source != TicketSource.Reception)
            return OpResult<IssuedTicket>.Fail("الحجز مغلق حالياً، جرّب بعد شوية.");

        var today = tenant.Today(now);
        name = Clean(name, 80);
        phone = NormalizePhone(phone);

        var service = await db.Services
            .Where(s => s.Id == serviceId && s.TenantId == tenant.Id && s.IsActive)
            .Select(s => new { s.Prefix, s.DailyLimit })
            .FirstOrDefaultAsync(ct);
        if (service is null)
            return OpResult<IssuedTicket>.Fail("الخدمة غير متاحة.");

        if (phone is not null)
        {
            var existing = await FindActiveByPhoneAsync(serviceId, today, phone, ct);
            if (existing is not null) return OpResult<IssuedTicket>.Success(existing);
        }

        var monthStart = TenantTime.MonthStartUtc(tenant.TimeZoneId, now);
        var usedThisMonth = await db.Tickets.CountAsync(t => t.TenantId == tenant.Id && t.CreatedAt >= monthStart, ct);
        if (usedThisMonth >= tenant.MaxTicketsPerMonth)
            return OpResult<IssuedTicket>.Fail("وصلت المنشأة للحد الشهري للتذاكر في باقتها الحالية.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var number = await NextNumberAsync(serviceId, today, ct);
            if (service.DailyLimit is { } limit && number > limit)
            {
                await tx.RollbackAsync(ct);
                return OpResult<IssuedTicket>.Fail("اكتمل عدد أدوار اليوم لهذه الخدمة.");
            }

            var ticket = new Ticket
            {
                TenantId = tenant.Id,
                ServiceId = serviceId,
                ServiceDate = today,
                Number = number,
                Code = $"{service.Prefix}-{number:000}",
                PublicToken = NewToken(),
                CustomerName = name,
                CustomerPhone = phone,
                Source = source,
                Status = TicketStatus.Waiting,
                CreatedAt = now,
            };
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await notifier.QueueChangedAsync(tenant.Id, serviceId, ct);
            return OpResult<IssuedTicket>.Success(new IssuedTicket(ticket.PublicToken, ticket.Code, false));
        }
        catch (DbUpdateException ex) when (phone is not null && IsActivePhoneConflict(ex))
        {
            // نفس الموبايل حجز في نفس اللحظة من تابين مختلفين: نرجّع التذكرة اللي سبقت
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var existing = await FindActiveByPhoneAsync(serviceId, today, phone, ct);
            return existing is not null
                ? OpResult<IssuedTicket>.Success(existing)
                : OpResult<IssuedTicket>.Fail("حصل تعارض، حاول مرة أخرى.");
        }
    }

    private async Task<int> NextNumberAsync(Guid serviceId, DateOnly date, CancellationToken ct)
    {
        // MERGE + HOLDLOCK = upsert ذري؛ الصف بيتقفل لحد نهاية الـ transaction
        var rows = await db.Database.SqlQuery<int>($"""
            MERGE dbo.DailySequences WITH (HOLDLOCK) AS target
            USING (SELECT {serviceId} AS ServiceId, {date} AS ServiceDate) AS src
                ON target.ServiceId = src.ServiceId AND target.ServiceDate = src.ServiceDate
            WHEN MATCHED THEN UPDATE SET LastNumber = target.LastNumber + 1
            WHEN NOT MATCHED THEN INSERT (ServiceId, ServiceDate, LastNumber) VALUES (src.ServiceId, src.ServiceDate, 1)
            OUTPUT inserted.LastNumber AS Value;
            """).ToListAsync(ct);
        return rows[0];
    }

    private Task<IssuedTicket?> FindActiveByPhoneAsync(Guid serviceId, DateOnly date, string phone, CancellationToken ct) =>
        db.Tickets
            .Where(t => t.ServiceId == serviceId && t.ServiceDate == date && t.CustomerPhone == phone
                        && (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Called || t.Status == TicketStatus.InService))
            .Select(t => new IssuedTicket(t.PublicToken, t.Code, true))
            .FirstOrDefaultAsync(ct);

    private static bool IsActivePhoneConflict(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 } sql && sql.Message.Contains("UX_Tickets_ActivePhone");

    public static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        Span<char> buf = stackalloc char[20];
        var n = 0;
        foreach (var raw in phone)
        {
            // أرقام عربية/هندية -> لاتينية
            var c = raw is >= '٠' and <= '٩' ? (char)('0' + (raw - '٠'))
                  : raw is >= '۰' and <= '۹' ? (char)('0' + (raw - '۰'))
                  : raw;
            if (char.IsAsciiDigit(c) || (c == '+' && n == 0))
            {
                if (n == buf.Length) return null;
                buf[n++] = c;
            }
        }
        return n >= 8 ? new string(buf[..n]) : null;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
