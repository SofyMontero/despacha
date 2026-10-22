using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Despacha.Api.Hosting;
using Despacha.Api.Shared.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Despacha.IntegrationTests;

public class SecurityTests
{
    [Fact]
    public async Task Endpoint_protegido_sin_token_responde_401()
    {
        await using var factory = new DespachaApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false, "otra-clave-de-firma-que-no-es-la-de-la-api-000")]
    [InlineData(true, DespachaApiFactory.SigningKey)]
    public async Task Token_con_firma_invalida_o_vencido_responde_401(bool expired, string signingKey)
    {
        await using var factory = new DespachaApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DespachaApiFactory.CreateToken(signingKey: signingKey, expired: expired));

        var response = await client.GetAsync("/api/platform/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_valido_entrega_usuario_y_empresa_del_token()
    {
        await using var factory = new DespachaApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DespachaApiFactory.CreateToken("7"));

        var session = await client.GetFromJsonAsync<SessionDto>("/api/platform/session");

        Assert.Equal("usuario-prueba", session!.UserId);
        Assert.Equal(7, session.TenantId);
    }

    [Fact]
    public async Task La_empresa_nunca_sale_de_un_encabezado_ni_de_la_URL()
    {
        await using var factory = new DespachaApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DespachaApiFactory.CreateToken(tenantId: null));
        client.DefaultRequestHeaders.Add("X-Tenant", "7");

        var session = await client.GetFromJsonAsync<SessionDto>("/api/platform/session?tid=7&tenantId=7");

        Assert.Null(session!.TenantId);
    }

    [Fact]
    public async Task Solo_los_endpoints_declarados_son_publicos()
    {
        await using var factory = new DespachaApiFactory();
        _ = factory.CreateClient();

        var anonymous = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => "/" + endpoint.RoutePattern.RawText!.TrimStart('/'))
            .ToHashSet();

        Assert.Subset(DespachaHostingExtensions.PublicRoutes.ToHashSet(), anonymous);
    }

    [Fact]
    public async Task CORS_solo_responde_a_origenes_configurados()
    {
        await using var factory = new DespachaApiFactory();
        using var client = factory.CreateClient();

        var allowed = await Preflight(client, "http://localhost:5173");
        var denied = await Preflight(client, "https://sitio-malicioso.example");

        Assert.Equal("http://localhost:5173", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Error_no_controlado_devuelve_ProblemDetails_sin_detalles_internos()
    {
        await using var factory = new DespachaApiFactory().WithServices(services => services.AddSingleton<IModule, TestModule>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(TestModule.InternalDetail, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task Acceso_a_datos_sin_empresa_activa_responde_403_controlado()
    {
        await using var factory = new DespachaApiFactory().WithServices(services => services.AddSingleton<IModule, TestModule>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test/tenant-db");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("No hay una empresa activa", body);
    }

    private static Task<HttpResponseMessage> Preflight(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/platform/session");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return client.SendAsync(request);
    }

    private sealed record SessionDto(string? UserId, long? TenantId);
}
