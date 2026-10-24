using Despacha.Api.Modules.Customers.Internal;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;

namespace Despacha.Api.Modules.Customers;

internal sealed class CustomersModule() : ModuleBase("Customers")
{
    public override void Register(IServiceCollection services, IConfiguration configuration) =>
        services.AddTenantSchema<CustomersDbContext>(Name, CustomersDbContext.MigrationsHistoryTable);

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var customers = endpoints.MapGroup("/api/customers").WithTags("Customers");
        EstablishmentEndpoints.Map(customers);
        CustomerEndpoints.Map(customers);
    }
}
