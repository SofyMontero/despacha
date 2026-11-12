using System.Net;
using System.Net.Http.Json;
using Despacha.Api.Modules.Platform.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using Xunit.Abstractions;

namespace Despacha.IntegrationTests.MariaDb;

/// <summary>Compatibilidad del proveedor de EF Core con MariaDB 11.8.9 (motor de referencia en Hostinger).</summary>
[Collection(MariaDbCollection.Name)]
public class MariaDbCompatibilityTests(MariaDbServer server, ITestOutputHelper output)
{
    [MariaDbFact]
    public async Task Conecta_y_reporta_la_version_de_referencia()
    {
        var connection = await server.CreateDatabaseAsync();

        var version = (string)(await MariaDbServer.ScalarAsync(connection, "SELECT VERSION()"))!;
        var collation = (string)(await MariaDbServer.ScalarAsync(connection, "SELECT @@collation_database"))!;
        output.WriteLine($"Servidor: {version}; colación por defecto: {collation}");

        Assert.StartsWith(MariaDbServer.ExpectedVersion, version);
        Assert.Contains("MariaDB", version);
    }

    [MariaDbFact]
    public async Task Conecta_con_TLS_obligatorio_como_fuera_de_Local()
    {
        var connection = new MySqlConnectionStringBuilder(await server.CreateDatabaseAsync()) { SslMode = MySqlSslMode.Required }.ConnectionString;

        var cipher = await MariaDbServer.ScalarAsync(connection, "SELECT VARIABLE_VALUE FROM information_schema.SESSION_STATUS WHERE VARIABLE_NAME = 'Ssl_cipher'");
        output.WriteLine($"Cifrado TLS: {cipher}");

        Assert.False(string.IsNullOrEmpty(cipher as string));
    }

    [MariaDbFact]
    public async Task El_script_01_se_aplica_dos_veces_sobre_la_estructura_historica_sin_tocar_sus_datos()
    {
        var connection = await server.CreateHistoricalControlDatabaseAsync();
        const string historical = "SELECT GROUP_CONCAT(CONCAT_WS('|', id, usuario, password, email, telefono, direccion, nombre, rol, " +
            "IFNULL(api_token, '-'), IFNULL(id_empresa, '-'), IFNULL(id_rol, '-')) ORDER BY id) FROM users";
        var before = await MariaDbServer.ScalarAsync(connection, historical);

        await MariaDbServer.ApplyIdentityScriptAsync(connection);
        await MariaDbServer.ApplyIdentityScriptAsync(connection);

        Assert.Equal(before, await MariaDbServer.ScalarAsync(connection, historical));
        await using var context = MariaDbServer.CreateContext(connection);
        await ControlSchema.EnsureReadyAsync(context, CancellationToken.None);
        var membership = await context.Memberships.Include(m => m.Role).SingleAsync();
        Assert.Equal(1, membership.UserId);
        Assert.Equal(RoleTemplates.Administrator, membership.Role.Name);
        Assert.Equal(6, await context.Roles.CountAsync(r => r.TenantId == 1));
        Assert.Equal(3, await context.Roles.CountAsync(r => r.IsTemplate));
        Assert.Equal(["Catalog", "Customers"], await context.TenantModules.OrderBy(m => m.Module).Select(m => m.Module).ToArrayAsync());
        Assert.All(await context.Users.ToListAsync(), user =>
        {
            Assert.Null(user.PasswordHash);
            Assert.Equal(user.UserName!.ToUpperInvariant(), user.NormalizedUserName);
            Assert.NotNull(user.SecurityStamp);
        });
    }

    [MariaDbFact]
    public async Task El_script_01_se_detiene_en_una_base_no_autorizada_sin_cambiar_nada()
    {
        var connection = await server.CreateHistoricalControlDatabaseAsync();

        var error = await Assert.ThrowsAsync<MySqlException>(() => MariaDbServer.ApplyIdentityScriptAsync(connection, authorizeDatabase: false));

        Assert.Contains("DETENER", error.Message);
        Assert.Equal(0L, Convert.ToInt64(await MariaDbServer.ScalarAsync(connection,
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND COLUMN_NAME = 'usuario_normalizado'")));
        Assert.Equal(3L, Convert.ToInt64(await MariaDbServer.ScalarAsync(connection,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()")));
    }

    [MariaDbFact]
    public async Task Sin_el_script_01_la_API_responde_503_con_las_columnas_faltantes()
    {
        var connection = await server.CreateHistoricalControlDatabaseAsync();
        await using var factory = new DespachaApiFactory { ControlConnectionString = connection };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { login = "vendedor", password = MariaDbServer.HistoricalSellerPassword });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(ControlDbContext.ControlSchemaVersion, await response.Content.ReadAsStringAsync());
    }

    [MariaDbFact]
    public async Task Tras_el_script_01_el_usuario_historico_entra_con_su_clave_bcrypt()
    {
        var connection = await server.CreateHistoricalControlDatabaseAsync();
        await MariaDbServer.ApplyIdentityScriptAsync(connection);
        await using var factory = new DespachaApiFactory { ControlConnectionString = connection };
        using var client = factory.CreateClient();

        var plain = await client.PostAsJsonAsync("/api/auth/login", new { login = "admin", password = "texto-plano" });
        var seller = await DespachaApiFactory.LoginAsync(client, "vendedor", MariaDbServer.HistoricalSellerPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, plain.StatusCode);
        Assert.Empty(seller.Memberships);
        Assert.StartsWith("AQAAAA", (string)(await MariaDbServer.ScalarAsync(connection, "SELECT password_hash FROM users WHERE id = 2"))!);
        Assert.StartsWith("$2y$", (string)(await MariaDbServer.ScalarAsync(connection, "SELECT password FROM users WHERE id = 2"))!);
    }

    [MariaDbFact]
    public async Task El_script_del_Migrator_crea_el_esquema_en_una_base_vacia()
    {
        var connection = await server.CreateDatabaseAsync();
        string script;
        using (var design = new ControlDbContextDesignTimeFactory().CreateDbContext([]))
        {
            script = design.Database.GenerateCreateScript();
        }

        await MariaDbServer.ExecuteAsync(connection, script);

        await using var context = MariaDbServer.CreateContext(connection);
        await ControlSchema.EnsureReadyAsync(context, CancellationToken.None);
        Assert.Equal(0, await context.Tenants.CountAsync());
    }

    [MariaDbFact]
    public async Task Los_tipos_de_datos_conservan_su_valor()
    {
        var connection = await MigratedDatabaseAsync();
        var lockout = new DateTimeOffset(2026, 10, 22, 20, 15, 30, TimeSpan.Zero);
        var created = new DateTime(2026, 10, 22, 20, 15, 30, DateTimeKind.Utc).AddTicks(1234560);
        var family = Guid.NewGuid();
        const string name = "Peñalosa Ávila ✓ 🚚";
        long userId;

        await using (var context = MariaDbServer.CreateContext(connection))
        {
            var user = new StaffUser
            {
                UserName = "tipos", NormalizedUserName = "TIPOS", DisplayName = name, LockoutEnd = lockout,
                LockoutEnabled = true, CreatedAtUtc = created, SecurityStamp = "s", ConcurrencyStamp = "c",
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            userId = user.Id;
            context.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId, TokenHash = new string('A', 64), FamilyId = family, CreatedAtUtc = created, ExpiresAtUtc = created.AddDays(14),
            });
            context.Tenants.Add(new Tenant { Code = "tipos", Name = name, Status = TenantStatus.Suspended });
            await context.SaveChangesAsync();
        }

        await using (var context = MariaDbServer.CreateContext(connection))
        {
            var user = await context.Users.SingleAsync(u => u.Id == userId);
            var token = await context.RefreshTokens.SingleAsync(t => t.FamilyId == family);
            var tenant = await context.Tenants.SingleAsync(t => t.Status == TenantStatus.Suspended);

            Assert.True(userId > 0);
            Assert.Equal(name, user.DisplayName);
            Assert.Equal(lockout, user.LockoutEnd);
            Assert.True(user.LockoutEnabled);
            Assert.Equal(created, user.CreatedAtUtc);
            Assert.Equal(new string('A', 64), token.TokenHash);
            Assert.Equal(name, tenant.Name);
            Assert.Equal(1, await context.RefreshTokens.CountAsync(t => t.ExpiresAtUtc > created && t.RevokedAtUtc == null));
        }
    }

    [MariaDbFact]
    public async Task Las_transacciones_confirman_revierten_y_respetan_indices_unicos()
    {
        var connection = await MigratedDatabaseAsync();
        await using var context = MariaDbServer.CreateContext(connection);

        await using (var rolledBack = await context.Database.BeginTransactionAsync())
        {
            context.Tenants.Add(new Tenant { Code = "revertida", Name = "revertida" });
            await context.SaveChangesAsync();
            await rolledBack.RollbackAsync();
        }

        context.ChangeTracker.Clear();
        await using (var committed = await context.Database.BeginTransactionAsync())
        {
            context.Tenants.Add(new Tenant { Code = "confirmada", Name = "confirmada" });
            await context.SaveChangesAsync();
            await committed.CommitAsync();
        }

        context.ChangeTracker.Clear();
        context.Tenants.Add(new Tenant { Code = "confirmada", Name = "duplicada" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        await using var check = MariaDbServer.CreateContext(connection);
        Assert.Equal(["confirmada"], await check.Tenants.Select(t => t.Code).ToArrayAsync());
    }

    [MariaDbFact]
    public async Task La_rotacion_concurrente_de_un_refresh_token_solo_gana_una_vez()
    {
        var connection = await MigratedDatabaseAsync();
        long tokenId;
        await using (var context = MariaDbServer.CreateContext(connection))
        {
            var user = new StaffUser { UserName = "rotacion", NormalizedUserName = "ROTACION", DisplayName = "rotacion" };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            var token = new RefreshToken { UserId = user.Id, TokenHash = new string('B', 64), FamilyId = Guid.NewGuid(), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
            context.RefreshTokens.Add(token);
            await context.SaveChangesAsync();
            tokenId = token.Id;
        }

        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = MariaDbServer.CreateContext(connection);
            var now = DateTime.UtcNow;
            return await context.RefreshTokens
                .Where(t => t.Id == tokenId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.RevokedReason, "rotated"));
        });

        Assert.Equal(1, (await Task.WhenAll(attempts)).Sum());
    }

    [MariaDbFact]
    public async Task El_flujo_de_identidad_y_permisos_funciona_sobre_MariaDB()
    {
        var connection = await server.CreateHistoricalControlDatabaseAsync();
        await MariaDbServer.ApplyIdentityScriptAsync(connection);
        await using var factory = new DespachaApiFactory { ControlConnectionString = connection };
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var admin = await DespachaApiFactory.LoginAsync(client, "admin.alfa");
        var seller = await DespachaApiFactory.LoginAsync(client, "vendedor.alfa");
        var members = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", admin.AccessToken));
        var denied = await client.SendAsync(DespachaApiFactory.Authorized(HttpMethod.Get, "/api/platform/members", seller.AccessToken));
        var rotated = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = admin.RefreshToken });
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = admin.RefreshToken });
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { login = "vendedor.alfa", password = "clave-incorrecta-x" });
        }

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { login = "vendedor.alfa", password = DespachaApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(alfa, admin.TenantId);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        Assert.Equal(2, (await members.Content.ReadFromJsonAsync<object[]>())!.Length);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    /// <summary>Base vacía con el esquema de control completo, como 'apply' en Local.</summary>
    private async Task<string> MigratedDatabaseAsync()
    {
        var connection = await server.CreateDatabaseAsync();
        await using var context = MariaDbServer.CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        return connection;
    }
}
