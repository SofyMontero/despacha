using Despacha.Api.Modules.Orders.Internal;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;

namespace Despacha.Api.Modules.Orders;

internal sealed class OrdersModule() : ModuleBase("Orders")
{
    public override void Register(IServiceCollection services, IConfiguration configuration) =>
        services.AddTenantSchema<OrdersDbContext>(Name, OrdersDbContext.MigrationsHistoryTable);

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) => OrderEndpoints.Map(endpoints);
}
