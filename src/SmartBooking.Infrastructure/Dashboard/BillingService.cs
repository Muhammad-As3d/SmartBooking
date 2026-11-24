using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Billing;
using SmartBooking.Core.Entities;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Dashboard;

public sealed record PlanDto(int Id, string Code, string Name, decimal MonthlyPrice, string Currency, int MaxServices, int MaxCounters, int MaxStaff, int MaxTicketsPerMonth, bool HasDisplayScreen, bool HasPushAlerts);
public sealed record InvoiceDto(Guid Id, string Number, string PlanName, decimal Amount, string Currency, int Months, InvoiceStatus Status, DateTime CreatedAt, DateTime? PaidAt);
public sealed record BillingDto(TenantSnapshot Tenant, bool Usable, int TicketsThisMonth, int Services, int Counters, int Staff, IReadOnlyList<PlanDto> Plans, IReadOnlyList<InvoiceDto> Invoices);

public sealed record AdminTenantRow(Guid Id, string Name, string Slug, BusinessKind Kind, string PlanName, SubscriptionStatus Status, DateTime? TrialEndsAt, DateTime? PeriodEnd, bool Suspended, DateTime CreatedAt, int TicketsThisMonth);
public sealed record AdminInvoiceRow(Guid Id, string Number, string TenantName, string PlanName, decimal Amount, string Currency, int Months, DateTime CreatedAt);
public sealed record AdminOverview(int Tenants, int Active, int Trials, decimal Mrr, IReadOnlyList<AdminTenantRow> Rows, IReadOnlyList<AdminInvoiceRow> PendingInvoices);

/// <summary>
/// الاشتراكات الشهرية. الفاتورة بتتعمل هنا، والتحصيل يتم عبر بوابة دفع (Paymob / Moyasar / Stripe)
/// أو تحويل بنكي يأكده مدير المنصة؛ لما تتدفع الفترة بتتمد تلقائياً.
/// </summary>
public sealed class BillingService(AppDbContext db, ITenantContext tenantContext, TenantDirectory tenants, TimeProvider clock)
{
    public static readonly Func<AppDbContext, IAsyncEnumerable<PlanDto>> ActivePlans =
        EF.CompileAsyncQuery((AppDbContext db) => db.Plans.Where(p => p.IsActive).OrderBy(p => p.SortOrder)
            .Select(p => new PlanDto(p.Id, p.Code, p.Name, p.MonthlyPrice, p.Currency, p.MaxServices, p.MaxCounters, p.MaxStaff, p.MaxTicketsPerMonth, p.HasDisplayScreen, p.HasPushAlerts)));

    public async Task<List<PlanDto>> GetPlansAsync(CancellationToken ct = default)
    {
        var list = new List<PlanDto>();
        await foreach (var p in ActivePlans(db).WithCancellation(ct)) list.Add(p);
        return list;
    }

    public async Task<BillingDto?> GetAsync(CancellationToken ct = default)
    {
        if (tenantContext.TenantId is not { } tenantId) return null;
        var tenant = await tenants.ByIdAsync(tenantId, ct);
        if (tenant is null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var monthStart = TenantTime.MonthStartUtc(tenant.TimeZoneId, now);

        var usage = await db.Tenants.Where(t => t.Id == tenantId)
            .Select(t => new
            {
                Tickets = db.Tickets.Count(k => k.TenantId == tenantId && k.CreatedAt >= monthStart),
                Services = db.Services.Count(s => s.IsActive),
                Counters = db.Counters.Count(c => c.IsActive),
                Staff = db.Users.Count(u => u.TenantId == tenantId),
            }).FirstAsync(ct);

        var invoices = await db.Invoices.OrderByDescending(i => i.CreatedAt).Take(24)
            .Select(i => new InvoiceDto(i.Id, i.Number, i.Plan.Name, i.Amount, i.Currency, i.Months, i.Status, i.CreatedAt, i.PaidAt))
            .ToListAsync(ct);

        return new BillingDto(tenant, tenant.IsUsable(now), usage.Tickets, usage.Services, usage.Counters, usage.Staff,
            await GetPlansAsync(ct), invoices);
    }

    public async Task<OpResult<Guid>> CreateInvoiceAsync(int planId, int months, CancellationToken ct = default)
    {
        if (tenantContext.TenantId is not { } tenantId) return OpResult<Guid>.Fail("غير مصرح.");
        if (months is not (1 or 3 or 6 or 12)) return OpResult<Guid>.Fail("مدة غير صالحة.");

        var plan = await db.Plans.Where(p => p.Id == planId && p.IsActive)
            .Select(p => new { p.Id, p.MonthlyPrice, p.Currency }).FirstOrDefaultAsync(ct);
        if (plan is null) return OpResult<Guid>.Fail("الباقة غير موجودة.");

        // إلغاء أي فاتورة معلقة قديمة عشان ما يبقاش فيه أكتر من طلب مفتوح
        await db.Invoices.Where(i => i.Status == InvoiceStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvoiceStatus.Void), ct);

        // خصم 2 شهر على السنوي، شهر على 6 شهور
        var discountMonths = months switch { 12 => 2, 6 => 1, _ => 0 };
        var now = clock.GetUtcNow().UtcDateTime;
        var invoice = new Invoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Number = $"INV-{now:yyMMdd}-{Random.Shared.Next(100000, 999999)}",
            Amount = plan.MonthlyPrice * (months - discountMonths),
            Currency = plan.Currency,
            Months = months,
            Status = InvoiceStatus.Pending,
            CreatedAt = now,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return OpResult<Guid>.Success(invoice.Id);
    }

    // ===== مدير المنصة =====

    public async Task<AdminOverview> GetAdminOverviewAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rows = await db.Tenants.IgnoreQueryFilters()
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .Select(t => new AdminTenantRow(t.Id, t.Name, t.Slug, t.Kind, t.Plan.Name, t.SubscriptionStatus, t.TrialEndsAt, t.CurrentPeriodEnd,
                t.IsSuspended, t.CreatedAt,
                db.Tickets.IgnoreQueryFilters().Count(k => k.TenantId == t.Id && k.CreatedAt >= monthStart)))
            .ToListAsync(ct);

        var paying = db.Tenants.Where(t => t.SubscriptionStatus == SubscriptionStatus.Active && t.CurrentPeriodEnd > now && !t.IsSuspended);
        var total = await db.Tenants.CountAsync(ct);
        var active = await paying.CountAsync(ct);
        var trials = await db.Tenants.CountAsync(t => t.SubscriptionStatus == SubscriptionStatus.Trial && t.TrialEndsAt > now, ct);
        var mrr = await paying.SumAsync(t => t.Plan.MonthlyPrice, ct);

        var pending = await db.Invoices.IgnoreQueryFilters()
            .Where(i => i.Status == InvoiceStatus.Pending)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new AdminInvoiceRow(i.Id, i.Number, i.Tenant.Name, i.Plan.Name, i.Amount, i.Currency, i.Months, i.CreatedAt))
            .ToListAsync(ct);

        return new AdminOverview(total, active, trials, mrr, rows, pending);
    }

    public async Task<OpResult> MarkInvoicePaidAsync(Guid invoiceId, string? reference, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var invoice = await db.Invoices.IgnoreQueryFilters().AsTracking()
                .Include(i => i.Tenant)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.Status == InvoiceStatus.Pending, ct);
            if (invoice is null) return OpResult.Fail("الفاتورة غير موجودة أو مدفوعة بالفعل.");

            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = now;
            invoice.PaymentReference = reference?.Trim();

            var tenant = invoice.Tenant;
            var startFrom = tenant.SubscriptionStatus == SubscriptionStatus.Active ? tenant.CurrentPeriodEnd : null;
            tenant.CurrentPeriodEnd = SubscriptionRules.ExtendPeriod(startFrom, invoice.Months, now);
            tenant.SubscriptionStatus = SubscriptionStatus.Active;
            tenant.PlanId = invoice.PlanId;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            tenants.Invalidate(tenant.Id, tenant.Slug);
            return OpResult.Success;
        });
    }

    public async Task<OpResult> SetSuspendedAsync(Guid tenantId, bool suspended, CancellationToken ct = default)
    {
        var slug = await db.Tenants.IgnoreQueryFilters().Where(t => t.Id == tenantId).Select(t => t.Slug).FirstOrDefaultAsync(ct);
        if (slug is null) return OpResult.Fail("المنشأة غير موجودة.");
        await db.Tenants.Where(t => t.Id == tenantId).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsSuspended, suspended), ct);
        tenants.Invalidate(tenantId, slug);
        return OpResult.Success;
    }

    public async Task<OpResult> ExtendTrialAsync(Guid tenantId, int days, CancellationToken ct = default)
    {
        var t = await db.Tenants.Where(x => x.Id == tenantId).Select(x => new { x.Slug, x.SubscriptionStatus, x.TrialEndsAt }).FirstOrDefaultAsync(ct);
        if (t is null) return OpResult.Fail("المنشأة غير موجودة.");
        if (t.SubscriptionStatus != SubscriptionStatus.Trial) return OpResult.Fail("المنشأة مش في فترة تجريبية.");

        var now = clock.GetUtcNow().UtcDateTime;
        var from = t.TrialEndsAt > now ? t.TrialEndsAt.Value : now;
        await db.Tenants.Where(x => x.Id == tenantId).ExecuteUpdateAsync(s => s.SetProperty(x => x.TrialEndsAt, from.AddDays(days)), ct);
        tenants.Invalidate(tenantId, t.Slug);
        return OpResult.Success;
    }
}
