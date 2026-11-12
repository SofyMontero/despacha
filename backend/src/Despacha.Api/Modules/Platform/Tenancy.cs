using System.Globalization;
using System.Security.Claims;
using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Platform;

public static class DespachaClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tid";
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del usuario de la base de control; los módulos lo guardan como auditoría, sin clave foránea.</summary>
    public static long? GetUserId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirst(DespachaClaims.Subject)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : null;
}

/// <summary>Empresa activa de la solicitud. Solo sale del token validado (D-05), nunca de un encabezado libre.</summary>
public interface ITenantContext
{
    long? TenantId { get; }
}

/// <summary>Crea el DbContext de un módulo conectado a la base de la empresa activa.</summary>
public interface ITenantDbContextFactory<TContext>
    where TContext : DbContext
{
    Task<TContext> CreateAsync(CancellationToken cancellationToken = default);
}

public sealed class TenantNotResolvedException()
    : DespachaException(StatusCodes.Status403Forbidden, "No hay una empresa activa en la sesión.");

public sealed class TenantUnavailableException()
    : DespachaException(StatusCodes.Status403Forbidden, "La empresa no está disponible.");

public static class TenancyApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<ControlSchemaMiddleware>().UseMiddleware<TenantResolutionMiddleware>();
}
