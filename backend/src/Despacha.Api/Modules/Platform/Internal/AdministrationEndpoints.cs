using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>Miembros y roles de la empresa activa. Toda consulta se filtra por la empresa del token.</summary>
internal static class AdministrationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var platform = endpoints.MapGroup("/api/platform").WithTags("Platform");

        platform.MapGet("/members", async (ITenantContext tenant, ControlDbContext db, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId!.Value;
            var members = await db.Memberships.AsNoTracking()
                .Where(m => m.TenantId == tenantId)
                .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { u.Id, u.UserName, u.DisplayName, Role = m.Role.Name, m.Status })
                .OrderBy(m => m.DisplayName)
                .ToListAsync(ct);
            return TypedResults.Ok(members.Select(m => new MemberResponse(m.Id, m.UserName!, m.DisplayName, m.Role, m.Status.ToString())).ToList());
        }).RequirePermission(Permissions.MembersRead);

        platform.MapPost("/members", async Task<Results<Created<MemberResponse>, ValidationProblem, Conflict>> (
            CreateMemberRequest request, ITenantContext tenant, ControlDbContext db, UserManager<StaffUser> users,
            TenantAccessService access, PlatformAudit audit, ClaimsPrincipal principal, TimeProvider clock, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId!.Value;
            var role = await db.Roles.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Name == request.Role, ct);
            if (role is null)
            {
                return Invalid("Role", "El rol no existe en la empresa.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var user = await users.FindByNameAsync(request.Login.Trim());
            if (user is null)
            {
                if (string.IsNullOrEmpty(request.TemporaryPassword))
                {
                    return Invalid("TemporaryPassword", "Un usuario nuevo requiere contraseña temporal.");
                }

                user = new StaffUser
                {
                    UserName = request.Login.Trim(),
                    DisplayName = request.DisplayName.Trim(),
                    RequiresPasswordChange = true,
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
                };
                var created = await users.CreateAsync(user, request.TemporaryPassword);
                if (!created.Succeeded)
                {
                    return (ValidationProblem)AuthEndpoints.ToResult(created).Result;
                }
            }
            else if (await db.Memberships.AnyAsync(m => m.UserId == user.Id && m.TenantId == tenantId, ct))
            {
                return TypedResults.Conflict();
            }

            db.Memberships.Add(new Membership
            {
                UserId = user.Id,
                TenantId = tenantId,
                RoleId = role.Id,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(ct);
            await audit.RecordAsync("member.created", principal.GetUserId(), tenantId, $"user:{user.Id}", ct);
            await transaction.CommitAsync(ct);
            access.InvalidateTenant(tenantId);

            return TypedResults.Created($"/api/platform/members/{user.Id}",
                new MemberResponse(user.Id, user.UserName!, user.DisplayName, role.Name, nameof(MembershipStatus.Active)));
        }).RequirePermission(Permissions.MembersManage);

        platform.MapPut("/members/{userId:long}", async Task<Results<NoContent, NotFound, ValidationProblem>> (
            long userId, UpdateMemberRequest request, ITenantContext tenant, ControlDbContext db,
            TenantAccessService access, PlatformAudit audit, ClaimsPrincipal principal, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId!.Value;
            var membership = await db.Memberships.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            if (userId == principal.GetUserId())
            {
                return Invalid("UserId", "No puede modificar su propia membresía.");
            }

            if (request.Role is not null)
            {
                var role = await db.Roles.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Name == request.Role, ct);
                if (role is null)
                {
                    return Invalid("Role", "El rol no existe en la empresa.");
                }

                membership.RoleId = role.Id;
            }

            if (request.Status is not null)
            {
                if (!Enum.TryParse<MembershipStatus>(request.Status, ignoreCase: false, out var status) || !Enum.IsDefined(status))
                {
                    return Invalid("Status", "Estado no válido. Use Active o Suspended.");
                }

                membership.Status = status;
            }

            await db.SaveChangesAsync(ct);
            access.InvalidateTenant(tenantId);
            await audit.RecordAsync("member.updated", principal.GetUserId(), tenantId, $"user:{userId}", ct);
            return TypedResults.NoContent();
        }).RequirePermission(Permissions.MembersManage);

        platform.MapGet("/roles", async (ITenantContext tenant, ControlDbContext db, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId!.Value;
            var roles = await db.Roles.AsNoTracking().Include(r => r.Permissions)
                .Where(r => r.TenantId == tenantId)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);
            return TypedResults.Ok(roles.Select(r => new RoleResponse(
                r.Id,
                r.Name,
                r.IsTemplate,
                RoleTemplates.IsAdministrator(r),
                (RoleTemplates.IsAdministrator(r) ? Permissions.Catalog.Select(p => p.Code) : r.Permissions.Select(p => p.Permission))
                    .Order(StringComparer.Ordinal).ToArray())));
        }).RequirePermission(Permissions.RolesRead);

        platform.MapPut("/roles/{roleId:long}/permissions", async Task<Results<NoContent, NotFound, ValidationProblem>> (
            long roleId, UpdateRolePermissionsRequest request, ITenantContext tenant, ControlDbContext db,
            TenantAccessService access, PlatformAudit audit, ClaimsPrincipal principal, CancellationToken ct) =>
        {
            var tenantId = tenant.TenantId!.Value;
            var role = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);
            if (role is null)
            {
                return TypedResults.NotFound();
            }

            if (RoleTemplates.IsAdministrator(role))
            {
                return Invalid("Role", "El rol Administrador siempre tiene todos los permisos.");
            }

            var unknown = request.Permissions.Where(code => !Permissions.Exists(code)).ToArray();
            if (unknown.Length > 0)
            {
                return Invalid("Permissions", "Permisos inexistentes: " + string.Join(", ", unknown));
            }

            var requested = request.Permissions.ToHashSet(StringComparer.Ordinal);
            role.Permissions.RemoveAll(p => !requested.Contains(p.Permission));
            requested.ExceptWith(role.Permissions.Select(p => p.Permission));
            role.Permissions.AddRange(requested.Select(code => new RolePermission { Permission = code }));
            await db.SaveChangesAsync(ct);
            access.InvalidateTenant(tenantId);
            await audit.RecordAsync("role.permissions_updated", principal.GetUserId(), tenantId, $"role:{role.Id}", ct);
            return TypedResults.NoContent();
        }).RequirePermission(Permissions.RolesManage);
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}

internal sealed record MemberResponse(long UserId, string Login, string DisplayName, string Role, string Status);

internal sealed record CreateMemberRequest(string Login, string DisplayName, string Role, string? TemporaryPassword);

internal sealed record UpdateMemberRequest(string? Role, string? Status);

internal sealed record RoleResponse(long Id, string Name, bool IsTemplate, bool IsLocked, string[] Permissions);

internal sealed record UpdateRolePermissionsRequest(string[] Permissions);
