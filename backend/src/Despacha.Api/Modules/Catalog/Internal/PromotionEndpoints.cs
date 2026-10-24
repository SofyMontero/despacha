using System.Security.Claims;
using Despacha.Api.Integrations.Storage;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Catalog.Internal;

internal static class PromotionEndpoints
{
    public static void Map(RouteGroupBuilder catalog)
    {
        var promotions = catalog.MapGroup("/promotions");

        promotions.MapGet("", async ([AsParameters] PromotionQuery query, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = db.Promotions.AsNoTracking().Include(p => p.Product).AsQueryable();
            if (RequestValidation.Clean(query.Q) is { } q)
            {
                if (WholeNumbers.TryParsePositiveInt(q, out var code))
                {
                    var codeText = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    items = items.Where(p => p.Title.Contains(q) || p.Code == codeText);
                }
                else
                {
                    items = items.Where(p => p.Title.Contains(q));
                }
            }

            if (query.ProductId is long productId)
            {
                items = items.Where(p => p.ProductId == productId);
            }

            if (query.Active is bool active)
            {
                items = items.Where(p => p.IsActive == active);
            }

            return TypedResults.Ok(await items.OrderByDescending(p => p.Priority).ThenByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
                .ToPageAsync(PageQuery.From(query.Page, query.PageSize), p => ToResponse(p, storage), ct));
        }).RequirePermission(Permissions.CatalogRead);

        promotions.MapGet("/{id:long}", async Task<Results<Ok<PromotionResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var promotion = await db.Promotions.AsNoTracking().Include(p => p.Product).SingleOrDefaultAsync(p => p.Id == id, ct);
            return promotion is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(promotion, storage));
        }).RequirePermission(Permissions.CatalogRead);

        promotions.MapPost("", async Task<Results<Created<PromotionResponse>, ValidationProblem>> (
            PromotionRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var validation = await ValidateAsync(request, db, isUpdate: false, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            var promotion = new Promotion { IsActive = request.IsActive ?? true };
            Apply(promotion, request);
            promotion.MarkCreated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            db.Promotions.Add(promotion);
            await db.SaveChangesAsync(ct);
            await db.Entry(promotion).Reference(p => p.Product).LoadAsync(ct);
            return TypedResults.Created($"/api/catalog/promotions/{promotion.Id}", ToResponse(promotion, storage));
        }).RequirePermission(Permissions.CatalogManage);

        promotions.MapPut("/{id:long}", async Task<Results<Ok<PromotionResponse>, NotFound, ValidationProblem>> (
            long id, PromotionRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var promotion = await db.Promotions.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (promotion is null)
            {
                return TypedResults.NotFound();
            }

            var validation = await ValidateAsync(request, db, isUpdate: true, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            db.ExpectVersion(promotion, request.Version!.Value);
            Apply(promotion, request);
            promotion.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            await db.Entry(promotion).Reference(p => p.Product).LoadAsync(ct);
            return TypedResults.Ok(ToResponse(promotion, storage));
        }).RequirePermission(Permissions.CatalogManage);

        promotions.MapPatch("/{id:long}/status", async Task<Results<Ok<PromotionResponse>, NotFound>> (
            long id, StatusRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var promotion = await db.Promotions.Include(p => p.Product).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (promotion is null)
            {
                return TypedResults.NotFound();
            }

            db.ExpectVersion(promotion, request.Version);
            promotion.IsActive = request.IsActive;
            promotion.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToResponse(promotion, storage));
        }).RequirePermission(Permissions.CatalogManage);

        promotions.MapPut("/{id:long}/image", async Task<Results<Ok<PromotionResponse>, NotFound, ValidationProblem>> (
            long id, IFormFile? file, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ITenantContext tenant, ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var promotion = await db.Promotions.Include(p => p.Product).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (promotion is null)
            {
                return TypedResults.NotFound();
            }

            var problem = await CatalogImages.ReplaceAsync(db, promotion, file, CatalogImages.Folder(tenant.TenantId!.Value, "promotions"),
                storage, user.GetUserId(), clock.GetUtcNow().UtcDateTime, ct);
            return problem is null ? TypedResults.Ok(ToResponse(promotion, storage)) : problem;
        }).RequirePermission(Permissions.CatalogManage).DisableAntiforgery();

        promotions.MapDelete("/{id:long}/image", async Task<Results<Ok<PromotionResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var promotion = await db.Promotions.Include(p => p.Product).SingleOrDefaultAsync(p => p.Id == id, ct);
            if (promotion is null)
            {
                return TypedResults.NotFound();
            }

            await CatalogImages.RemoveAsync(db, promotion, storage, user.GetUserId(), clock.GetUtcNow().UtcDateTime, ct);
            return TypedResults.Ok(ToResponse(promotion, storage));
        }).RequirePermission(Permissions.CatalogManage);
    }

    /// <summary>
    /// Reglas de <c>promoController.php</c>: título y descripción obligatorios y código de producto entero mayor que 0.
    /// Los precios son opcionales y no negativos.
    /// </summary>
    private static async Task<RequestValidation> ValidateAsync(PromotionRequest request, CatalogDbContext db, bool isUpdate, CancellationToken ct)
    {
        var validation = new RequestValidation()
            .Required(nameof(request.Title), request.Title, CatalogLimits.PromotionTitle)
            .Required(nameof(request.Description), request.Description, CatalogLimits.PromotionDescription)
            .Required(nameof(request.Code), request.Code, CatalogLimits.Code)
            .Price(nameof(request.UnitPrice), request.UnitPrice ?? 0m, mustBePositive: false)
            .Price(nameof(request.PackPrice), request.PackPrice ?? 0m, mustBePositive: false)
            .RequireVersion(request.Version, isUpdate);

        if (!string.IsNullOrWhiteSpace(request.Code) && !WholeNumbers.TryParsePositiveInt(request.Code, out _))
        {
            validation.Add(nameof(request.Code), "Debe ser un código de producto numérico mayor que 0.");
        }

        if (request.ProductId is long productId && !await db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            validation.Add(nameof(request.ProductId), "El producto no existe.");
        }

        return validation;
    }

    private static void Apply(Promotion promotion, PromotionRequest request)
    {
        promotion.Title = request.Title!.Trim();
        promotion.Description = request.Description!.Trim();
        promotion.Code = WholeNumbers.TryParsePositiveInt(request.Code, out var code)
            ? code.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : request.Code!.Trim();
        promotion.ProductId = request.ProductId;
        promotion.UnitPrice = request.UnitPrice ?? 0m;
        promotion.PackPrice = request.PackPrice ?? 0m;
        promotion.SellsByUnit = request.SellsByUnit ?? true;
        promotion.Priority = request.Priority ?? 0;
    }

    private static PromotionResponse ToResponse(Promotion p, IImageStorage storage) =>
        new(p.Id, p.Title, p.Description ?? "", p.Code, p.ProductId, p.Product?.Name, p.UnitPrice, p.PackPrice, p.SellsByUnit, p.Priority,
            p.IsActive, p.ImageKey, storage.GetUrl(p.ImageKey), p.CreatedAtUtc, p.Version, p.UpdatedAtUtc);
}
