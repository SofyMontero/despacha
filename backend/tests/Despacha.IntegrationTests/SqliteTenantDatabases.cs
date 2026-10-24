using Despacha.Api.Modules.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Despacha.IntegrationTests;

/// <summary>Una base SQLite en memoria por empresa: doble de prueba de la base MariaDB de cada empresa.</summary>
internal sealed class SqliteTenantDatabases : IDisposable
{
    private readonly Dictionary<long, SqliteConnection> _connections = [];
    private readonly HashSet<(long TenantId, Type Context)> _created = [];
    private readonly Lock _lock = new();

    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "despacha-media-" + Guid.NewGuid().ToString("N"));

    public DbContextOptions<TContext> Options<TContext>(long tenantId)
        where TContext : DbContext
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(tenantId, out var connection))
            {
                connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                _connections[tenantId] = connection;
            }

            return new DbContextOptionsBuilder<TContext>().UseSqlite(connection).Options;
        }
    }

    /// <summary>Crea las tablas de cada DbContext una sola vez; EnsureCreated no sirve porque varios módulos comparten la base.</summary>
    public void EnsureTables(DbContext context, long tenantId)
    {
        lock (_lock)
        {
            if (_created.Add((tenantId, context.GetType())))
            {
                context.GetService<IRelationalDatabaseCreator>().CreateTables();
            }
        }
    }

    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        if (Directory.Exists(MediaRoot))
        {
            Directory.Delete(MediaRoot, recursive: true);
        }
    }
}

internal sealed class SqliteTenantDbContextFactory<TContext>(ITenantContext tenant, SqliteTenantDatabases databases, IServiceProvider services)
    : ITenantDbContextFactory<TContext>
    where TContext : DbContext
{
    public Task<TContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new TenantNotResolvedException();
        var context = ActivatorUtilities.CreateInstance<TContext>(services, databases.Options<TContext>(tenantId));
        databases.EnsureTables(context, tenantId);
        return Task.FromResult(context);
    }
}
