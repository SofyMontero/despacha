using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Platform.Internal;

internal sealed class TenantDbContextFactory<TContext>(
    ITenantContext tenant,
    TenantConnectionProvider connections,
    IServiceProvider services) : ITenantDbContextFactory<TContext>
    where TContext : DbContext
{
    public async Task<TContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new TenantNotResolvedException();
        var connectionString = await connections.GetConnectionStringAsync(tenantId, cancellationToken);

        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDespachaMySql(connectionString);

        return ActivatorUtilities.CreateInstance<TContext>(services, builder.Options);
    }
}
