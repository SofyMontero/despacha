using Despacha.Api.Modules.Platform;
using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;

namespace Despacha.UnitTests;

public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteControlDatabase _control = new();
    private readonly ControlDbContext _context;
    private readonly long _tenantA;
    private readonly long _tenantB;

    public TenantIsolationTests()
    {
        _tenantA = _control.AddTenant("empresa-a", "localhost", "despacha_empresa_a_local", "empresa_a_local");
        _tenantB = _control.AddTenant("empresa-b", "127.0.0.1", "despacha_empresa_b_local", "empresa_b_local");
        _context = _control.CreateContext();
    }

    [Fact]
    public async Task Cada_empresa_obtiene_su_propia_base_y_su_propio_secreto()
    {
        var provider = CreateProvider();

        var a = new MySqlConnectionStringBuilder(await provider.GetConnectionStringAsync(_tenantA, default));
        var b = new MySqlConnectionStringBuilder(await provider.GetConnectionStringAsync(_tenantB, default));

        Assert.Equal("despacha_empresa_a_local", a.Database);
        Assert.Equal("usuario_a", a.UserID);
        Assert.Equal("despacha_empresa_b_local", b.Database);
        Assert.Equal("usuario_b", b.UserID);
    }

    [Fact]
    public async Task El_factory_conecta_el_DbContext_a_la_empresa_activa()
    {
        var provider = CreateProvider();

        await using var contextA = await CreateFactory(provider, _tenantA).CreateAsync();
        await using var contextB = await CreateFactory(provider, _tenantB).CreateAsync();

        Assert.Contains("despacha_empresa_a_local", contextA.Database.GetConnectionString());
        Assert.DoesNotContain("despacha_empresa_b_local", contextA.Database.GetConnectionString());
        Assert.Contains("despacha_empresa_b_local", contextB.Database.GetConnectionString());
    }

    [Fact]
    public async Task Sin_empresa_activa_no_se_crea_ningun_DbContext()
    {
        await Assert.ThrowsAsync<TenantNotResolvedException>(() => CreateFactory(CreateProvider(), tenantId: null).CreateAsync());
    }

    [Fact]
    public async Task Empresa_suspendida_o_inexistente_no_esta_disponible()
    {
        var suspended = _control.AddTenant("suspendida", "localhost", "despacha_suspendida_local", "suspendida", TenantStatus.Suspended);
        var provider = CreateProvider();

        await Assert.ThrowsAsync<TenantUnavailableException>(() => provider.GetConnectionStringAsync(suspended, default));
        await Assert.ThrowsAsync<TenantUnavailableException>(() => provider.GetConnectionStringAsync(9999, default));
    }

    [Fact]
    public async Task Empresa_sin_secreto_configurado_falla_sin_exponer_valores()
    {
        var tenant = _control.AddTenant("sin-secreto", "localhost", "despacha_sin_secreto_local", "no_existe");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProvider().GetConnectionStringAsync(tenant, default));
        Assert.Contains("no_existe", ex.Message);
    }

    [Fact]
    public async Task Empresa_con_servidor_remoto_se_bloquea_en_Local()
    {
        var tenant = _control.AddTenant("remota", "db.example.com", "despacha_remota", "empresa_a_local");
        await Assert.ThrowsAsync<DatabaseConnectionBlockedException>(() => CreateProvider().GetConnectionStringAsync(tenant, default));
    }

    private TenantConnectionProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantSecrets:empresa_a_local:User"] = "usuario_a",
                ["TenantSecrets:empresa_a_local:Password"] = "clave-de-prueba-a",
                ["TenantSecrets:empresa_b_local:User"] = "usuario_b",
                ["TenantSecrets:empresa_b_local:Password"] = "clave-de-prueba-b",
            })
            .Build();

        return new TenantConnectionProvider(
            _context,
            configuration,
            TestSupport.Guard(DespachaEnvironment.Local),
            new DespachaRuntime(DespachaEnvironment.Local),
            new MemoryCache(new MemoryCacheOptions()));
    }

    private static TenantDbContextFactory<ProbeDbContext> CreateFactory(TenantConnectionProvider provider, long? tenantId)
    {
        var tenant = new TenantContext();
        if (tenantId is not null)
        {
            tenant.Set(tenantId.Value);
        }

        return new TenantDbContextFactory<ProbeDbContext>(tenant, provider, new ServiceCollection().BuildServiceProvider());
    }

    public void Dispose()
    {
        _context.Dispose();
        _control.Dispose();
    }
}
