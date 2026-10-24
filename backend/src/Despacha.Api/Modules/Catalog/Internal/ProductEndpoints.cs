using System.Security.Claims;
using Despacha.Api.Integrations.Storage;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Catalog.Internal;

internal static class ProductEndpoints
{
    private const int MaxUnitsPerPack = 10_000;

    public static void Map(RouteGroupBuilder catalog)
    {
        var products = catalog.MapGroup("/products");

        products.MapGet("", async ([AsParameters] ProductQuery query, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
            if (RequestValidation.Clean(query.Q) is { } q)
            {
                if (WholeNumbers.TryParsePositiveInt(q, out var code))
                {
                    var codeText = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    items = items.Where(p => p.Name.Contains(q) || p.Code == codeText);
                }
                else
                {
                    items = items.Where(p => p.Name.Contains(q));
                }
            }

            if (query.CategoryId is long categoryId)
            {
                items = items.Where(p => p.CategoryId == categoryId);
            }

            if (query.Active is bool active)
            {
                items = items.Where(p => p.IsActive == active);
            }

            return TypedResults.Ok(await items.OrderBy(p => p.Name).ThenBy(p => p.Id)
                .ToPageAsync(PageQuery.From(query.Page, query.PageSize), p => ToResponse(p, storage), ct));
        }).RequirePermission(Permissions.CatalogRead);

        products.MapGet("/{id:long}", async Task<Results<Ok<ProductResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var product = await db.Products.AsNoTracking().Include(p => p.Category).SingleOrDefaultAsync(p => p.Id == id, ct);
            return product is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(product, storage));
        }).RequirePermission(Permissions.CatalogRead);

        products.MapPost("", async Task<Results<Created<ProductResponse>, ValidationProblem>> (
            ProductRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var validation = await ValidateAsync(request, db, current: null, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            var product = new Product();
            product.UseActive(request.IsActive ?? true);
            Apply(product, request);
            product.MarkCreated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            await db.Entry(product).Reference(p => p.Category).LoadAsync(ct);
            return TypedResults.Created($"/api/catalog/products/{product.Id}", ToResponse(product, storage));
        }).RequirePermission(Permissions.CatalogManage);

        products.MapPut("/{id:long}", async Task<Results<Ok<ProductResponse>, NotFound, ValidationProblem>> (
            long id, ProductRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            var validation = await ValidateAsync(request, db, product, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            db.ExpectVersion(product, request.Version!.Value);
            Apply(product, request);
            product.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            await db.Entry(product).Reference(p => p.Category).LoadAsync(ct);
            return TypedResults.Ok(ToResponse(product, storage));
        }).RequirePermission(Permissions.CatalogManage);

        products.MapPatch("/{id:long}/status", async Task<Results<Ok<ProductResponse>, NotFound>> (
            long id, StatusRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var product = await db.Products.Include(p => p.Category).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            db.ExpectVersion(product, request.Version);
            product.UseActive(request.IsActive);
            product.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToResponse(product, storage));
        }).RequirePermission(Permissions.CatalogManage);

        products.MapPut("/{id:long}/image", async Task<Results<Ok<ProductResponse>, NotFound, ValidationProblem>> (
            long id, IFormFile? file, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ITenantContext tenant, ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var product = await db.Products.Include(p => p.Category).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            var problem = await CatalogImages.ReplaceAsync(db, product, file, CatalogImages.Folder(tenant.TenantId!.Value, "products"),
                storage, user.GetUserId(), clock.GetUtcNow().UtcDateTime, ct);
            return problem is null ? TypedResults.Ok(ToResponse(product, storage)) : problem;
        }).RequirePermission(Permissions.CatalogManage).DisableAntiforgery();

        products.MapDelete("/{id:long}/image", async Task<Results<Ok<ProductResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var product = await db.Products.Include(p => p.Category).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return TypedResults.NotFound();
            }

            await CatalogImages.RemoveAsync(db, product, storage, user.GetUserId(), clock.GetUtcNow().UtcDateTime, ct);
            return TypedResults.Ok(ToResponse(product, storage));
        }).RequirePermission(Permissions.CatalogManage);
    }

    /// <summary>Reglas del PHP: código y descripción obligatorios, precio unidad mayor que 0, precio paca no negativo y código sin repetir.</summary>
    private static async Task<RequestValidation> ValidateAsync(ProductRequest request, CatalogDbContext db, Product? current, CancellationToken ct)
    {
        var validation = new RequestValidation()
            .Required(nameof(request.Code), request.Code, CatalogLimits.Code)
            .Required(nameof(request.Name), request.Name, CatalogLimits.ProductName)
            .Price(nameof(request.UnitPrice), request.UnitPrice, mustBePositive: true)
            .Price(nameof(request.PackPrice), request.PackPrice ?? 0m, mustBePositive: false)
            .RequireVersion(request.Version, required: current is not null);

        if (request.UnitsPerPack is < 1 or > MaxUnitsPerPack)
        {
            validation.Add(nameof(request.UnitsPerPack), $"Debe estar entre 1 y {MaxUnitsPerPack}.");
        }

        if (request.CategoryId is not long categoryId)
        {
            validation.Add(nameof(request.CategoryId), "Es obligatorio.");
        }
        else if (await db.Categories.AsNoTracking().Where(c => c.Id == categoryId).Select(c => (bool?)c.IsActive).SingleOrDefaultAsync(ct) is not bool active)
        {
            validation.Add(nameof(request.CategoryId), "La categoría no existe.");
        }
        else if (!active && current?.CategoryId != categoryId)
        {
            validation.Add(nameof(request.CategoryId), "La categoría está inactiva.");
        }

        if (!string.IsNullOrWhiteSpace(request.Code) && !WholeNumbers.TryParsePositiveInt(request.Code, out _))
        {
            validation.Add(nameof(request.Code), "Debe ser un número entero mayor que 0.");
        }

        if (WholeNumbers.TryParsePositiveInt(request.Code, out var code))
        {
            var codeText = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var keepsCurrentCode = current is not null && current.Code == codeText;
            var currentId = current?.Id;
            if (!keepsCurrentCode && await db.Products.AnyAsync(p => p.Code == codeText && p.Id != currentId, ct))
            {
                validation.Add(nameof(request.Code), "Ya existe un producto con ese código.");
            }
        }

        return validation;
    }

    private static void Apply(Product product, ProductRequest request)
    {
        product.Code = WholeNumbers.TryParsePositiveInt(request.Code, out var code)
            ? code.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : request.Code!.Trim();
        product.Name = request.Name!.Trim();
        product.CategoryId = request.CategoryId;
        product.UnitPrice = request.UnitPrice!.Value;
        product.PackPrice = request.PackPrice ?? 0m;
        product.UnitsPerPack = request.UnitsPerPack ?? 1;
        product.SellsByUnit = request.SellsByUnit ?? true;
    }

    private static ProductResponse ToResponse(Product p, IImageStorage storage) =>
        new(p.Id, p.Code, p.Name, p.CategoryId, p.Category?.Name, p.UnitPrice, p.PackPrice, p.UnitsPerPack, p.SellsByUnit, p.IsActive,
            p.ImageKey, storage.GetUrl(p.ImageKey), p.Version, p.UpdatedAtUtc);
}
