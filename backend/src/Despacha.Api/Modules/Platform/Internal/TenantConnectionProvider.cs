using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MySql.Data.MySqlClient;

namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>
/// Arma la conexión de la base de una empresa: servidor y base desde la base de control,
/// usuario y clave desde la configuración externa <c>TenantSecrets:{SecretName}</c>.
/// </summary>
internal sealed class TenantConnectionProvider(
    ControlDbContext control,
    IConfiguration configuration,
    DatabaseSafetyGuard guard,
    DespachaRuntime runtime,
    IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<string> GetConnectionStringAsync(long tenantId, CancellationToken cancellationToken)
    {
        var cacheKey = $"tenant-connection:{tenantId}";
        if (cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            return cached;
        }

        var connectionString = await BuildAsync(tenantId, cancellationToken);
        cache.Set(cacheKey, connectionString, CacheDuration);
        return connectionString;
    }

    private async Task<string> BuildAsync(long tenantId, CancellationToken cancellationToken)
    {
        var target = await control.TenantDatabases
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.Tenant.Status == TenantStatus.Active)
            .Select(d => new { d.Server, d.Port, d.DatabaseName, d.SecretName })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new TenantUnavailableException();

        guard.EnsureAllowed(target.Server, target.DatabaseName);

        var secret = configuration.GetSection($"TenantSecrets:{target.SecretName}");
        var user = secret["User"];
        if (string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException($"No está configurado el secreto de conexión '{target.SecretName}'.");
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = target.Server,
            Port = (uint)target.Port,
            Database = target.DatabaseName,
            UserID = user,
            Password = secret["Password"] ?? string.Empty,
            SslMode = runtime.IsLocal ? MySqlSslMode.Preferred : MySqlSslMode.Required,
        };

        return builder.ConnectionString;
    }
}
