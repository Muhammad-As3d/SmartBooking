using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Entities;
using SmartBooking.Infrastructure.Data;
using SmartBooking.Infrastructure.Tenancy;

namespace SmartBooking.Infrastructure.Dashboard;

public sealed record ServiceRow(Guid Id, string Name, string Prefix, string? Description, int AvgServiceSeconds, int? DailyLimit, bool IsActive, int SortOrder, int CountersCount);
public sealed record ServiceForm(Guid? Id, string Name, string Prefix, string? Description, int AvgMinutes, int? DailyLimit, bool IsActive, int SortOrder);

public sealed record CounterRow(Guid Id, string Name, bool IsOpen, bool IsActive, int SortOrder, IReadOnlyList<string> ServiceNames);
public sealed record CounterForm(Guid? Id, string Name, bool IsActive, int SortOrder, IReadOnlyList<Guid> ServiceIds);
public sealed record Option(Guid Id, string Name);

/// <summary>إدارة الخدمات ونقاط الخدمة مع حدود الباقة.</summary>
public sealed class CatalogService(AppDbContext db, ITenantContext tenantContext, TenantDirectory tenants, TimeProvider clock)
{
    private Guid TenantId => tenantContext.TenantId ?? throw new InvalidOperationException("No tenant in context.");

    public Task<List<ServiceRow>> ListServicesAsync(CancellationToken ct = default) =>
        db.Services.OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new ServiceRow(s.Id, s.Name, s.Prefix, s.Description, s.AvgServiceSeconds, s.DailyLimit, s.IsActive, s.SortOrder, s.Counters.Count))
            .ToListAsync(ct);

    public Task<List<Option>> ServiceOptionsAsync(CancellationToken ct = default) =>
        db.Services.Where(s => s.IsActive).OrderBy(s => s.SortOrder).Select(s => new Option(s.Id, s.Name)).ToListAsync(ct);

    public Task<ServiceForm?> GetServiceAsync(Guid id, CancellationToken ct = default) =>
        db.Services.Where(s => s.Id == id)
            .Select(s => new ServiceForm(s.Id, s.Name, s.Prefix, s.Description, (s.AvgServiceSeconds + 59) / 60, s.DailyLimit, s.IsActive, s.SortOrder))
            .FirstOrDefaultAsync(ct);

    public async Task<OpResult> SaveServiceAsync(ServiceForm form, CancellationToken ct = default)
    {
        var prefix = form.Prefix.Trim().ToUpperInvariant();
        if (prefix.Length is < 1 or > 2 || !prefix.All(char.IsAsciiLetterUpper))
            return OpResult.Fail("الحرف المميز لازم يكون حرف أو حرفين إنجليزي (مثل A أو LB).");

        var duplicatePrefix = await db.Services.AnyAsync(s => s.Prefix == prefix && s.IsActive && s.Id != form.Id, ct);
        if (duplicatePrefix) return OpResult.Fail("الحرف ده مستخدم لخدمة تانية، اختار حرف مختلف عشان الأرقام ما تتلخبطش.");

        var avgSeconds = Math.Clamp(form.AvgMinutes, 1, 240) * 60;

        if (form.Id is null)
        {
            var tenant = await tenants.ByIdAsync(TenantId, ct);
            var count = await db.Services.CountAsync(s => s.IsActive, ct);
            if (tenant is not null && count >= tenant.MaxServices)
                return OpResult.Fail($"باقتك تسمح بـ {tenant.MaxServices} خدمات. رقّي الباقة لإضافة المزيد.");

            db.Services.Add(new Service
            {
                Id = Guid.CreateVersion7(), TenantId = TenantId, Name = form.Name.Trim(), Prefix = prefix,
                Description = form.Description?.Trim(), AvgServiceSeconds = avgSeconds, DailyLimit = form.DailyLimit,
                IsActive = form.IsActive, SortOrder = form.SortOrder, CreatedAt = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(ct);
            return OpResult.Success;
        }

        var n = await db.Services.Where(s => s.Id == form.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Name, form.Name.Trim())
                .SetProperty(x => x.Prefix, prefix)
                .SetProperty(x => x.Description, form.Description == null ? null : form.Description.Trim())
                .SetProperty(x => x.AvgServiceSeconds, avgSeconds)
                .SetProperty(x => x.DailyLimit, form.DailyLimit)
                .SetProperty(x => x.IsActive, form.IsActive)
                .SetProperty(x => x.SortOrder, form.SortOrder), ct);
        return n == 0 ? OpResult.Fail("الخدمة غير موجودة.") : OpResult.Success;
    }

    public async Task<List<CounterRow>> ListCountersAsync(CancellationToken ct = default) =>
        await db.Counters.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CounterRow(c.Id, c.Name, c.IsOpen, c.IsActive, c.SortOrder,
                c.Services.OrderBy(s => s.Service.SortOrder).Select(s => s.Service.Name).ToList()))
            .ToListAsync(ct);

    public Task<CounterForm?> GetCounterAsync(Guid id, CancellationToken ct = default) =>
        db.Counters.Where(c => c.Id == id)
            .Select(c => new CounterForm(c.Id, c.Name, c.IsActive, c.SortOrder, c.Services.Select(s => s.ServiceId).ToList()))
            .FirstOrDefaultAsync(ct);

    public async Task<OpResult> SaveCounterAsync(CounterForm form, CancellationToken ct = default)
    {
        // نتأكد إن كل الخدمات المختارة تابعة لنفس المنشأة (الـ query filter بيضمن ده)
        var validIds = await db.Services.Where(s => form.ServiceIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        if (validIds.Count == 0) return OpResult.Fail("اختار خدمة واحدة على الأقل لنقطة الخدمة.");

        Counter counter;
        if (form.Id is null)
        {
            var tenant = await tenants.ByIdAsync(TenantId, ct);
            var count = await db.Counters.CountAsync(c => c.IsActive, ct);
            if (tenant is not null && count >= tenant.MaxCounters)
                return OpResult.Fail($"باقتك تسمح بـ {tenant.MaxCounters} نقاط خدمة. رقّي الباقة لإضافة المزيد.");

            counter = new Counter { Id = Guid.CreateVersion7(), TenantId = TenantId, CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.Counters.Add(counter);
        }
        else
        {
            var existing = await db.Counters.AsTracking().Include(c => c.Services).FirstOrDefaultAsync(c => c.Id == form.Id, ct);
            if (existing is null) return OpResult.Fail("نقطة الخدمة غير موجودة.");
            counter = existing;
            foreach (var link in counter.Services.Where(l => !validIds.Contains(l.ServiceId)).ToList())
                counter.Services.Remove(link);
        }

        counter.Name = form.Name.Trim();
        counter.IsActive = form.IsActive;
        counter.SortOrder = form.SortOrder;
        foreach (var sid in validIds.Where(id => counter.Services.All(l => l.ServiceId != id)))
            counter.Services.Add(new CounterService { CounterId = counter.Id, ServiceId = sid });

        await db.SaveChangesAsync(ct);
        return OpResult.Success;
    }
}
