using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartBooking.Core;
using SmartBooking.Core.Entities;

namespace SmartBooking.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // بيتقرأ وقت تنفيذ كل استعلام (مش وقت إنشاء الـ DbContext) ويدخل كـ parameter في الـ global query filters.
    // مهم: الـ DbContext ممكن يتعمل قبل ما المستخدم يتعرف (أثناء الـ authentication)، فالقيمة لازم تتقرأ متأخر.
    private Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<CounterService> CounterServices => Set<CounterService>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketPushSubscription> TicketPushSubscriptions => Set<TicketPushSubscription>();
    public DbSet<DailySequence> DailySequences => Set<DailySequence>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(120);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TenantId);
        });

        b.Entity<Plan>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(60);
            e.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false);
            e.Property(x => x.MonthlyPrice).HasPrecision(10, 2);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasData(SeedData.Plans);
        });

        b.Entity<Tenant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Slug).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.City).HasMaxLength(60);
            e.Property(x => x.Address).HasMaxLength(200);
            e.Property(x => x.TimeZoneId).HasMaxLength(40).IsUnicode(false);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Service>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Description).HasMaxLength(250);
            e.Property(x => x.Prefix).HasMaxLength(2).IsUnicode(false);
            e.HasOne<Tenant>().WithMany(t => t.Services).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.IsActive, x.SortOrder });
            e.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
        });

        b.Entity<Counter>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60);
            e.HasOne<Tenant>().WithMany(t => t.Counters).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.IsActive });
            e.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
        });

        b.Entity<CounterService>(e =>
        {
            e.HasKey(x => new { x.CounterId, x.ServiceId });
            e.HasOne(x => x.Counter).WithMany(c => c.Services).HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany(s => s.Counters).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ServiceId);
            e.HasQueryFilter(x => CurrentTenantId == null || x.Counter.TenantId == CurrentTenantId);
        });

        b.Entity<Ticket>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(8).IsUnicode(false);
            e.Property(x => x.PublicToken).HasMaxLength(32).IsUnicode(false);
            e.Property(x => x.CustomerName).HasMaxLength(80);
            e.Property(x => x.CustomerPhone).HasMaxLength(20).IsUnicode(false);

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Counter).WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.PublicToken).IsUnique();

            // شبكة أمان ثانية: مستحيل يتكرر نفس الرقم لنفس الخدمة في نفس اليوم
            e.HasIndex(x => new { x.ServiceId, x.ServiceDate, x.Number }).IsUnique();

            // نفس الرقم (الموبايل) ما يقدرش يحجز دورين نشطين في نفس الخدمة، حتى لو ضغط مرتين في نفس الثانية
            e.HasIndex(x => new { x.ServiceId, x.ServiceDate, x.CustomerPhone })
                .IsUnique()
                .HasFilter("[CustomerPhone] IS NOT NULL AND [Status] IN (0, 1, 2)")
                .HasDatabaseName("UX_Tickets_ActivePhone");

            // ترتيب الطابور وحساب "قدامك كام واحد"
            e.HasIndex(x => new { x.ServiceId, x.ServiceDate, x.Status, x.Id });

            // لوحة التحكم والإحصائيات
            e.HasIndex(x => new { x.TenantId, x.ServiceDate, x.Status }).IncludeProperties(x => new { x.ServiceId, x.CounterId });
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => new { x.CounterId, x.Status }).HasFilter("[CounterId] IS NOT NULL AND [Status] IN (1, 2)");

            // التنبيه الذكي يدور بس على المنتظرين اللي لسه ما اتنبهوش
            e.HasIndex(x => new { x.ServiceDate, x.ServiceId })
                .HasFilter("[Status] = 0 AND [AlertSentAt] IS NULL")
                .HasDatabaseName("IX_Tickets_PendingAlerts");

            e.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
        });

        b.Entity<TicketPushSubscription>(e =>
        {
            e.Property(x => x.Endpoint).HasMaxLength(600).IsUnicode(false);
            e.Property(x => x.P256dh).HasMaxLength(120).IsUnicode(false);
            e.Property(x => x.Auth).HasMaxLength(60).IsUnicode(false);
            e.HasOne<Ticket>().WithMany(t => t.PushSubscriptions).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TicketId);
        });

        b.Entity<DailySequence>(e =>
        {
            e.HasKey(x => new { x.ServiceId, x.ServiceDate });
            e.HasOne<Service>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false);
            e.Property(x => x.PaymentReference).HasMaxLength(100);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => x.Status).HasFilter("[Status] = 0");
            e.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
        });

        b.Entity<IdentityRole<Guid>>().HasData(SeedData.Roles);
    }
}
