using Despacha.Api.Modules.Catalog.Internal;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;

namespace Despacha.Api.Modules.Catalog;

internal sealed class CatalogModule() : ModuleBase("Catalog")
{
    public override void Register(IServiceCollection services, IConfiguration configuration) =>
        services.AddTenantSchema<CatalogDbContext>(Name, CatalogDbContext.MigrationsHistoryTable);

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var catalog = endpoints.MapGroup("/api/catalog").WithTags("Catalog");
        CategoryEndpoints.Map(catalog);
        ProductEndpoints.Map(catalog);
        PromotionEndpoints.Map(catalog);
    }
}
