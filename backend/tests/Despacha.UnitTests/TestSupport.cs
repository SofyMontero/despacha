using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Despacha.UnitTests;

internal static class TestSupport
{
    public static IHostEnvironment Host(string environmentName) => new HostingEnvironment { EnvironmentName = environmentName };

    public static DatabaseSafetyGuard Guard(DespachaEnvironment environment, params string[] allowedHosts) =>
        new(new DespachaRuntime(environment), Options.Create(new DatabaseSafetyOptions { AllowedHosts = allowedHosts }));
}

/// <summary>Base de control en SQLite en memoria: doble de prueba, sin MySQL real.</summary>
internal sealed class SqliteControlDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteControlDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ControlDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ControlDbContext>().UseSqlite(_connection).Options);

    public long AddTenant(string code, string server, string database, string secretName, TenantStatus status = TenantStatus.Active)
    {
        using var context = CreateContext();
        var tenant = new Tenant { Code = code, Name = code, Status = status };
        context.Tenants.Add(tenant);
        context.SaveChanges();
        context.TenantDatabases.Add(new TenantDatabase
        {
            TenantId = tenant.Id,
            Server = server,
            DatabaseName = database,
            SecretName = secretName,
        });
        context.SaveChanges();
        return tenant.Id;
    }

    public void Dispose() => _connection.Dispose();
}

internal sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);
