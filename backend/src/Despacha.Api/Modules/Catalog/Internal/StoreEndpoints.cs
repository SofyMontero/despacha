using Despacha.Api.Integrations.Storage;
using Despacha.Api.Modules.Platform;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Catalog.Internal;

/// <summary>Catálogo que ve el cliente, sin sesión de personal.</summary>
internal static class StoreEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var store = endpoints.MapGroup("/api/store/{tenantCode}").WithTags("Store");

        store.MapGet("/categories", async (ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = await db.Categories.AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.Name)
                .Select(category => new StoreCategory(category.Id, category.Name, category.ImageKey))
                .ToListAsync(ct);
            return TypedResults.Ok(items.Select(item => item with { ImageUrl = storage.GetUrl(item.ImageUrl) }).ToList());
        }).AllowAnonymous();

        store.MapGet("/products", async (long? categoryId, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = db.Products.AsNoTracking().Where(product => product.IsActive);
            if (categoryId is > 0)
            {
                items = items.Where(product => product.CategoryId == categoryId);
            }

            var page = await items.OrderBy(product => product.Name).Take(200)
                .Select(product => new StoreProduct(
                    product.Id, product.Name, product.UnitPrice, product.PackPrice, product.SellsByUnit, product.ImageKey))
                .ToListAsync(ct);
            return TypedResults.Ok(page.Select(item => item with { ImageUrl = storage.GetUrl(item.ImageUrl) }).ToList());
        }).AllowAnonymous();

        store.MapGet("/promotions", async (ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = await db.Promotions.AsNoTracking()
                .Where(promotion => promotion.IsActive)
                .OrderBy(promotion => promotion.Title)
                .Select(promotion => new StorePromotion(
                    promotion.Id, promotion.Title, promotion.Description, promotion.UnitPrice, promotion.PackPrice, promotion.SellsByUnit, promotion.ImageKey))
                .ToListAsync(ct);
            return TypedResults.Ok(items.Select(item => item with { ImageUrl = storage.GetUrl(item.ImageUrl) }).ToList());
        }).AllowAnonymous();
    }
}

internal sealed record StoreCategory(long Id, string Name, string? ImageUrl);

internal sealed record StoreProduct(long Id, string Name, decimal UnitPrice, decimal PackPrice, bool SellsByUnit, string? ImageUrl);

internal sealed record StorePromotion(long Id, string Title, string? Description, decimal UnitPrice, decimal PackPrice, bool SellsByUnit, string? ImageUrl);
