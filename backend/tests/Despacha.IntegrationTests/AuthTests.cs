using System.Net;
using System.Net.Http.Json;
using Despacha.Api.Modules.Platform.Internal;
using Microsoft.EntityFrameworkCore;

namespace Despacha.IntegrationTests;

public class AuthTests
{
    [Fact]
    public async Task Login_con_una_empresa_entrega_token_con_tid_y_permisos_del_rol()
    {
        await using var factory = new DespachaApiFactory();
        var tenantId = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();

        var session = await DespachaApiFactory.LoginAsync(client, "admin.alfa");
        var me = await (await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/auth/me", session.AccessToken)))
            .Content.ReadFromJsonAsync<MeDto>();

        Assert.Equal(tenantId, session.TenantId);
        Assert.False(string.IsNullOrEmpty(session.RefreshToken));
        Assert.InRange(session.ExpiresIn, 14 * 60, 15 * 60);
        Assert.Equal("Administrador", me!.Role);
        Assert.Contains("platform.members.manage", me.Permissions);
        Assert.Contains("cash.reconcile", me.Permissions);
    }

    [Fact]
    public async Task Contrasena_y_refresh_token_se_guardan_solo_como_hash()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var session = await DespachaApiFactory.LoginAsync(client, "admin.alfa");

        await factory.WithControlDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.UserName == "admin.alfa");
            var tokens = await db.RefreshTokens.Select(t => t.TokenHash).ToListAsync();
            Assert.DoesNotContain(DespachaApiFactory.Password, user.PasswordHash);
            Assert.StartsWith("AQAAAA", user.PasswordHash);
            Assert.DoesNotContain(session.RefreshToken, tokens);
            Assert.Contains(TokenService.Hash(session.RefreshToken!), tokens);
        });
    }

    [Fact]
    public async Task Usuario_inexistente_y_contrasena_incorrecta_responden_igual()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { login = "admin.alfa", password = "otra-clave-incorrecta" });
        var unknownUser = await client.PostAsJsonAsync("/api/auth/login", new { login = "nadie", password = "otra-clave-incorrecta" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(Detail(await wrongPassword.Content.ReadAsStringAsync()), Detail(await unknownUser.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Cinco_intentos_fallidos_bloquean_la_cuenta_aunque_luego_use_la_clave_correcta()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { login = "admin.alfa", password = "otra-clave-incorrecta" });
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new { login = "admin.alfa", password = DespachaApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await factory.WithControlDbAsync(async db =>
            Assert.NotNull((await db.Users.SingleAsync(u => u.UserName == "admin.alfa")).LockoutEnd));
    }

    [Fact]
    public async Task Login_limitado_por_tasa_responde_429()
    {
        await using var factory = new DespachaApiFactory().WithSetting("Security:RateLimit:AuthPermitsPerMinute", "2");
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", new { login = "nadie", password = "x" })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Refresh_rota_el_token_y_su_reutilizacion_revoca_toda_la_familia()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var first = await DespachaApiFactory.LoginAsync(client, "admin.alfa");

        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        var second = await rotated.Content.ReadFromJsonAsync<AuthDto>();
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = second!.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(first.TenantId, second.TenantId);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revoca_el_refresh_token()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var session = await DespachaApiFactory.LoginAsync(client, "admin.alfa");

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = session.RefreshToken });
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Usuario_con_varias_empresas_elige_una_y_no_puede_elegir_ajena()
    {
        await using var factory = new DespachaApiFactory();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        var beta = await factory.ProvisionTenantAsync("beta", "admin.beta");
        var gamma = await factory.ProvisionTenantAsync("gamma", "admin.gamma");
        await factory.AddMemberAsync(beta, "admin.alfa", "Vendedor");
        using var client = factory.CreateClient();

        var session = await DespachaApiFactory.LoginAsync(client, "admin.alfa");
        var inBeta = await DespachaApiFactory.SelectTenantAsync(client, session, beta);
        var foreign = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Post, "/api/auth/tenant", inBeta.AccessToken, new { tenantId = gamma }));
        var me = await (await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/auth/me", inBeta.AccessToken)))
            .Content.ReadFromJsonAsync<MeDto>();

        Assert.Null(session.TenantId);
        Assert.Equal([alfa, beta], session.Memberships.Select(m => m.TenantId).Order().ToArray());
        Assert.Equal(beta, inBeta.TenantId);
        Assert.Equal("Vendedor", me!.Role);
        Assert.DoesNotContain("platform.members.read", me.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    public async Task Recuperacion_de_contrasena_cambia_la_clave_y_cierra_las_sesiones()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var session = await DespachaApiFactory.LoginAsync(client, "admin.alfa");

        var unknown = await client.PostAsJsonAsync("/api/auth/password/forgot", new { login = "nadie" });
        var forgot = await client.PostAsJsonAsync("/api/auth/password/forgot", new { login = "admin.alfa" });
        var token = Assert.Single(factory.ResetDelivery.Tokens).Value;
        var weak = await client.PostAsJsonAsync("/api/auth/password/reset", new { login = "admin.alfa", token, newPassword = "corta" });
        var reset = await client.PostAsJsonAsync("/api/auth/password/reset", new { login = "admin.alfa", token, newPassword = "nueva-clave-segura-1" });
        var oldPassword = await client.PostAsJsonAsync("/api/auth/login", new { login = "admin.alfa", password = DespachaApiFactory.Password });
        var oldRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        await DespachaApiFactory.LoginAsync(client, "admin.alfa", "nueva-clave-segura-1");
    }

    [Fact]
    public async Task Cliente_web_recibe_el_refresh_en_cookie_segura_y_exige_origen_permitido()
    {
        await using var factory = new DespachaApiFactory();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient(new() { HandleCookies = false });

        var login = await client.PostAsJsonAsync("/api/auth/login", new { login = "admin.alfa", password = DespachaApiFactory.Password, client = "web" });
        var body = await login.Content.ReadFromJsonAsync<AuthDto>();
        var setCookie = login.Headers.GetValues("Set-Cookie").Single();
        var cookie = setCookie.Split(';')[0];

        var withoutOrigin = await RefreshWithCookie(client, cookie, origin: null);
        var foreignOrigin = await RefreshWithCookie(client, cookie, "https://sitio-malicioso.example");
        var allowed = await RefreshWithCookie(client, cookie, "http://localhost:5173");

        Assert.Null(body!.RefreshToken);
        Assert.StartsWith(AuthEndpoints.RefreshCookie + "=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, withoutOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains(allowed.Headers.GetValues("Set-Cookie"), value => value.StartsWith(AuthEndpoints.RefreshCookie + "=", StringComparison.Ordinal));
    }

    private static Task<HttpResponseMessage> RefreshWithCookie(HttpClient client, string cookie, string? origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Cookie", cookie);
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return client.SendAsync(request);
    }

    private static string? Detail(string problemJson) =>
        System.Text.Json.JsonDocument.Parse(problemJson).RootElement.GetProperty("title").GetString();

    private sealed record MeDto(long UserId, string Login, long? TenantId, string? Role, string[] Permissions);
}
