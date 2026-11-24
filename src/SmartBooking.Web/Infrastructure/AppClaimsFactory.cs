using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartBooking.Infrastructure.Data;

namespace SmartBooking.Web.Infrastructure;

public sealed class AppClaimsFactory(
    UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppClaims.FullName, user.FullName));
        if (user.TenantId is { } tenantId)
            identity.AddClaim(new Claim(AppClaims.TenantId, tenantId.ToString()));
        return identity;
    }
}
