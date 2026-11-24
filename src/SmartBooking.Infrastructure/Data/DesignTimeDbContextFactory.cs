using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmartBooking.Core;

namespace SmartBooking.Infrastructure.Data;

/// <summary>للـ dotnet ef فقط (إنشاء الـ migrations) بدون تشغيل الموقع.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=SmartBooking;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
