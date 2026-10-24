using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Despacha.Api.Hosting;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Despacha.IntegrationTests;

public class PermissionAndTenantIsolationTests
{
    [Fact]
    public async Task Vendedor_no_administra_miembros_y_el_administrador_si()
    {
        await using var factory = new DespachaApiFactory();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        using var client = factory.CreateClient();
        var admin = await DespachaApiFactory.LoginAsync(client, "admin.alfa");
        var seller = await DespachaApiFactory.LoginAsync(client, "vendedor.alfa");

        var asSeller = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", seller.AccessToken));
        var asAdmin = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", admin.AccessToken));

        Assert.Equal(HttpStatusCode.Forbidden, asSeller.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
    }

    [Fact]
    public async Task El_administrador_solo_ve_y_modifica_miembros_de_su_empresa()
    {
        await using var factory = new DespachaApiFactory();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        var beta = await factory.ProvisionTenantAsync("beta", "admin.beta");
        var betaSeller = await factory.AddMemberAsync(beta, "vendedor.beta", "Vendedor");
        using var client = factory.CreateClient();
        var admin = await DespachaApiFactory.LoginAsync(client, "admin.alfa");

        var members = await (await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", admin.AccessToken)))
            .Content.ReadFromJsonAsync<MemberDto[]>();
        var update = await client.SendAsync(DespachaApiFactory.Authorized(
            HttpMethod.Put, $"/api/platform/members/{betaSeller}", admin.AccessToken, new { status = "Suspended" }));
        var betaRoleId = await RoleIdAsync(factory, beta, "Vendedor");
        var roleUpdate = await client.SendAsync(DespachaApiFactory.Authorized(
            HttpMethod.Put, $"/api/platform/roles/{betaRoleId}/permissions", admin.AccessToken, new { permissions = Array.Empty<string>() }));

        Assert.Equal(["admin.alfa"], members!.Select(m => m.Login).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, roleUpdate.StatusCode);
        Assert.NotEqual(alfa, beta);
    }

    [Fact]
    public async Task Token_firmado_con_empresa_sin_membresia_no_obtiene_permisos()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        var beta = await factory.ProvisionTenantAsync("beta", "admin.beta");
        using var client = factory.CreateClient();
        var adminAlfaId = await UserIdAsync(factory, "admin.alfa");

        var forged = DespachaApiFactory.CreateToken(beta.ToString(CultureInfo.InvariantCulture), subject: adminAlfaId.ToString(CultureInfo.InvariantCulture));
        var response = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", forged));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Membresia_suspendida_pierde_permisos_y_renovacion()
    {
        await using var factory = new DespachaApiFactory();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        var sellerId = await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        await factory.WithControlDbAsync(async db =>
        {
            var role = await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.TenantId == alfa && r.Name == "Vendedor");
            role.Permissions.Add(new RolePermission { Permission = Permissions.MembersRead });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        var admin = await DespachaApiFactory.LoginAsync(client, "admin.alfa");
        var seller = await DespachaApiFactory.LoginAsync(client, "vendedor.alfa");
        var before = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", seller.AccessToken));

        var suspend = await client.SendAsync(DespachaApiFactory.Authorized(
            HttpMethod.Put, $"/api/platform/members/{sellerId}", admin.AccessToken, new { status = "Suspended" }));
        var after = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", seller.AccessToken));
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = seller.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task El_administrador_ajusta_permisos_de_un_rol_pero_no_los_del_Administrador()
    {
        await using var factory = new DespachaApiFactory();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        using var client = factory.CreateClient();
        var admin = await DespachaApiFactory.LoginAsync(client, "admin.alfa");
        var seller = await DespachaApiFactory.LoginAsync(client, "vendedor.alfa");
        var sellerRole = await RoleIdAsync(factory, alfa, "Vendedor");
        var adminRole = await RoleIdAsync(factory, alfa, "Administrador");

        var grant = await client.SendAsync(DespachaApiFactory.Authorized(
            HttpMethod.Put, $"/api/platform/roles/{sellerRole}/permissions", admin.AccessToken, new { permissions = new[] { "catalog.read", Permissions.MembersRead } }));
        var unknown = await client.SendAsync(DespachaApiFactory.Authorized(
            HttpMethod.Put, $"/api/platform/roles/{sellerRole}/permissions", admin.AccessToken, new { permissions = new[] { "todo.permitido" } }));
        var lockedRole = await client.SendAsync(DespachaApiFactory.Authorized(
            HttpMethod.Put, $"/api/platform/roles/{adminRole}/permissions", admin.AccessToken, new { permissions = Array.Empty<string>() }));
        var sellerNow = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", seller.AccessToken));

        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, lockedRole.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sellerNow.StatusCode);
    }

    [Fact]
    public async Task El_administrador_crea_un_miembro_que_debe_cambiar_su_clave()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = await DespachaApiFactory.LoginAsync(client, "admin.alfa");

        var created = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Post, "/api/platform/members", admin.AccessToken,
            new { login = "domiciliario.alfa", displayName = "Domiciliario", role = "Domiciliario", temporaryPassword = "temporal-segura-1" }));
        var duplicate = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Post, "/api/platform/members", admin.AccessToken,
            new { login = "domiciliario.alfa", displayName = "Domiciliario", role = "Domiciliario" }));
        var selfEdit = await client.SendAsync(DespachaApiFactory.Authorized(
            HttpMethod.Put, $"/api/platform/members/{await UserIdAsync(factory, "admin.alfa")}", admin.AccessToken, new { status = "Suspended" }));
        var courier = await DespachaApiFactory.LoginAsync(client, "domiciliario.alfa", "temporal-segura-1");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, selfEdit.StatusCode);
        Assert.True(courier.RequiresPasswordChange);
    }

    [Fact]
    public async Task Un_permiso_de_un_modulo_desactivado_no_se_concede()
    {
        await using var factory = new DespachaApiFactory().WithServices(services => services.AddSingleton<IModule, TestModule>());
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        using var client = factory.CreateClient();
        var seller = await DespachaApiFactory.LoginAsync(client, "vendedor.alfa");
        var enabled = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/test/catalog", seller.AccessToken));

        await factory.WithControlDbAsync(async db =>
        {
            var module = await db.TenantModules.SingleAsync(m => m.TenantId == alfa && m.Module == "Catalog");
            module.Enabled = false;
            await db.SaveChangesAsync();
        });
        factory.InvalidateAccess(alfa);
        var disabled = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/test/catalog", seller.AccessToken));

        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, disabled.StatusCode);
    }

    [Fact]
    public async Task Todo_endpoint_de_negocio_exige_un_permiso_del_catalogo()
    {
        await using var factory = new DespachaApiFactory();
        _ = factory.CreateClient();
        string[] sessionOnly = ["/api/auth/tenant", "/api/auth/me", "/api/auth/password/change", "/api/platform/session"];

        var unprotected = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Route: "/" + endpoint.RoutePattern.RawText!.TrimStart('/'), endpoint.Metadata))
            .Where(e => e.Route.StartsWith("/api/", StringComparison.Ordinal))
            .Where(e => !DespachaHostingExtensions.PublicRoutes.Contains(e.Route) && !sessionOnly.Contains(e.Route))
            .Where(e => !e.Metadata.GetOrderedMetadata<AuthorizationPolicy>()
                .SelectMany(policy => policy.Requirements).OfType<PermissionRequirement>().Any())
            .Select(e => e.Route)
            .ToArray();

        Assert.Empty(unprotected);
    }

    private static async Task<long> RoleIdAsync(DespachaApiFactory factory, long tenantId, string name)
    {
        long id = 0;
        await factory.WithControlDbAsync(async db => id = await db.Roles.Where(r => r.TenantId == tenantId && r.Name == name).Select(r => r.Id).SingleAsync());
        return id;
    }

    private static async Task<long> UserIdAsync(DespachaApiFactory factory, string login)
    {
        long id = 0;
        await factory.WithControlDbAsync(async db => id = await db.Users.Where(u => u.UserName == login).Select(u => u.Id).SingleAsync());
        return id;
    }

    private sealed record MemberDto(long UserId, string Login, string DisplayName, string Role, string Status);
}
