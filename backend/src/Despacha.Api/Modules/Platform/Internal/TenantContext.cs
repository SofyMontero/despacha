using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Platform.Internal;

internal sealed class TenantContext : ITenantContext
{
    public long? TenantId { get; private set; }

    public void Set(long tenantId)
    {
        if (TenantId is not null && TenantId != tenantId)
        {
            throw new InvalidOperationException("La empresa de la solicitud ya fue establecida.");
        }

        TenantId = tenantId;
    }
}

internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenant, ControlDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && long.TryParse(context.User.FindFirst(DespachaClaims.TenantId)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId)
            && tenantId > 0)
        {
            tenant.Set(tenantId);
        }
        else if (StoreTenantCode(context) is { } code)
        {
            var id = await db.Tenants.AsNoTracking()
                .Where(company => company.Code == code && company.Status == TenantStatus.Active)
                .Select(company => company.Id)
                .SingleOrDefaultAsync();
            if (id > 0)
            {
                tenant.Set(id);
            }
        }

        await next(context);
    }

    /// <summary>El catálogo público identifica la empresa por el código de la URL, no por la sesión del personal.</summary>
    private static string? StoreTenantCode(HttpContext context)
    {
        var parts = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: >= 3 } && parts[0] == "api" && parts[1] == "store" && parts[2].Length > 0
            ? parts[2]
            : null;
    }
}
