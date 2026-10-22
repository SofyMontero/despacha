using System.Security.Claims;

namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>Endpoint mínimo protegido para verificar autenticación y empresa activa. El login llega en 4B.</summary>
internal static class SessionEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/platform")
            .WithTags("Platform")
            .MapGet("/session", (ClaimsPrincipal user, ITenantContext tenant) =>
                TypedResults.Ok(new SessionResponse(user.FindFirst(DespachaClaims.Subject)?.Value, tenant.TenantId)))
            .WithName("GetSession");
    }
}

internal sealed record SessionResponse(string? UserId, long? TenantId);
