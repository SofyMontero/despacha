using System.Globalization;

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
    public Task InvokeAsync(HttpContext context, TenantContext tenant)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && long.TryParse(context.User.FindFirst(DespachaClaims.TenantId)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tenantId)
            && tenantId > 0)
        {
            tenant.Set(tenantId);
        }

        return next(context);
    }
}
