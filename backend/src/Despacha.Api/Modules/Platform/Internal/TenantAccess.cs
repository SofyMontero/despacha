using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>Roles plantilla de Fase 2. El Administrador tiene todos los permisos de los módulos activos y no se edita.</summary>
internal static class RoleTemplates
{
    public const string Administrator = "Administrador";
    public const string Seller = "Vendedor";
    public const string Courier = "Domiciliario";

    public static readonly IReadOnlyDictionary<string, string[]> Defaults = new Dictionary<string, string[]>
    {
        [Administrator] = [],
        [Seller] =
        [
            "fieldSales.workday.manage", "fieldSales.visit.create", "fieldSales.order.create",
            "customers.read.assigned", "catalog.read", "tracking.report", "receivables.read.assigned",
        ],
        [Courier] =
        [
            "deliveries.read.own", "deliveries.claim", "deliveries.updateStatus", "deliveries.evidence", "tracking.report",
        ],
    };

    public static bool IsAdministrator(Role role) => role.IsTemplate && role.Name == Administrator;
}

internal sealed record TenantAccess(long UserId, long TenantId, string Role, IReadOnlySet<string> Permissions);

/// <summary>
/// Revalida en servidor la membresía, la empresa y el usuario (D-05) y calcula los permisos efectivos
/// limitados a los módulos activos (D-10). Caché corta; los cambios de administración la invalidan.
/// </summary>
internal sealed class TenantAccessService(ControlDbContext db, IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<TenantAccess?> GetAsync(long userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var key = $"platform:access:{tenantId}:{Version(tenantId)}:{userId}";
        if (cache.TryGetValue(key, out TenantAccess? cached))
        {
            return cached;
        }

        var access = await LoadAsync(userId, tenantId, cancellationToken);
        cache.Set(key, access, Ttl);
        return access;
    }

    public void InvalidateTenant(long tenantId) =>
        cache.Set(VersionKey(tenantId), Version(tenantId) + 1, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

    private async Task<TenantAccess?> LoadAsync(long userId, long tenantId, CancellationToken cancellationToken)
    {
        var activeRoleIds = db.Memberships
            .Where(m => m.UserId == userId && m.TenantId == tenantId && m.Status == MembershipStatus.Active)
            .Where(m => db.Tenants.Any(t => t.Id == tenantId && t.Status == TenantStatus.Active))
            .Where(m => db.Users.Any(u => u.Id == userId && u.IsActive))
            .Select(m => m.RoleId);
        var role = await db.Roles.AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId && activeRoleIds.Contains(r.Id))
            .SingleOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return null;
        }

        var modules = await db.TenantModules.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Enabled)
            .Select(m => m.Module)
            .ToListAsync(cancellationToken);
        modules.Add(Permissions.PlatformModule);

        var granted = RoleTemplates.IsAdministrator(role)
            ? Permissions.Catalog.Select(p => p.Code)
            : role.Permissions.Select(p => p.Permission).Where(Permissions.Exists);

        var effective = granted.Where(code => modules.Contains(Permissions.ModuleOf(code))).ToHashSet(StringComparer.Ordinal);
        return new TenantAccess(userId, tenantId, role.Name, effective);
    }

    private int Version(long tenantId) => cache.TryGetValue(VersionKey(tenantId), out int version) ? version : 0;

    private static string VersionKey(long tenantId) => $"platform:access-version:{tenantId}";
}

internal sealed class PermissionAuthorizationHandler(TenantAccessService access, ITenantContext tenant)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.GetUserId() is not long userId || tenant.TenantId is not long tenantId)
        {
            return;
        }

        var granted = await access.GetAsync(userId, tenantId);
        if (granted?.Permissions.Contains(requirement.Permission) == true)
        {
            context.Succeed(requirement);
        }
    }
}
