using System.Security.Claims;
using Despacha.Api.Integrations.Storage;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Catalog.Internal;

internal static class CategoryEndpoints
{
    public static void Map(RouteGroupBuilder catalog)
    {
        var categories = catalog.MapGroup("/categories");

        categories.MapGet("", async ([AsParameters] CategoryQuery query, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = db.Categories.AsNoTracking();
            if (RequestValidation.Clean(query.Q) is { } q)
            {
                items = items.Where(c => c.Name.Contains(q));
            }

            if (query.Active is bool active)
            {
                items = items.Where(c => c.IsActive == active);
            }

            return TypedResults.Ok(await items.OrderBy(c => c.Name).ThenBy(c => c.Id)
                .ToPageAsync(PageQuery.From(query.Page, query.PageSize), c => ToResponse(c, storage), ct));
        }).RequirePermission(Permissions.CatalogRead);

        categories.MapGet("/{id:long}", async Task<Results<Ok<CategoryResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
            return category is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(category, storage));
        }).RequirePermission(Permissions.CatalogRead);

        categories.MapPost("", async Task<Results<Created<CategoryResponse>, ValidationProblem>> (
            CategoryRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            var validation = Validate(request, requireVersion: false);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            await using var db = await factory.CreateAsync(ct);
            var category = new Category { Name = request.Name!.Trim() };
            category.MarkCreated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/api/catalog/categories/{category.Id}", ToResponse(category, storage));
        }).RequirePermission(Permissions.CatalogManage);

        categories.MapPut("/{id:long}", async Task<Results<Ok<CategoryResponse>, NotFound, ValidationProblem>> (
            long id, CategoryRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            var validation = Validate(request, requireVersion: true);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            await using var db = await factory.CreateAsync(ct);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null)
            {
                return TypedResults.NotFound();
            }

            db.ExpectVersion(category, request.Version!.Value);
            category.Name = request.Name!.Trim();
            category.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToResponse(category, storage));
        }).RequirePermission(Permissions.CatalogManage);

        categories.MapPatch("/{id:long}/status", async Task<Results<Ok<CategoryResponse>, NotFound>> (
            long id, StatusRequest request, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null)
            {
                return TypedResults.NotFound();
            }

            db.ExpectVersion(category, request.Version);
            category.IsActive = request.IsActive;
            category.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToResponse(category, storage));
        }).RequirePermission(Permissions.CatalogManage);

        categories.MapPut("/{id:long}/image", async Task<Results<Ok<CategoryResponse>, NotFound, ValidationProblem>> (
            long id, IFormFile? file, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ITenantContext tenant, ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null)
            {
                return TypedResults.NotFound();
            }

            var problem = await CatalogImages.ReplaceAsync(db, category, file, CatalogImages.Folder(tenant.TenantId!.Value, "categories"),
                storage, user.GetUserId(), clock.GetUtcNow().UtcDateTime, ct);
            return problem is null ? TypedResults.Ok(ToResponse(category, storage)) : problem;
        }).RequirePermission(Permissions.CatalogManage).DisableAntiforgery();

        categories.MapDelete("/{id:long}/image", async Task<Results<Ok<CategoryResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CatalogDbContext> factory, IImageStorage storage,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null)
            {
                return TypedResults.NotFound();
            }

            await CatalogImages.RemoveAsync(db, category, storage, user.GetUserId(), clock.GetUtcNow().UtcDateTime, ct);
            return TypedResults.Ok(ToResponse(category, storage));
        }).RequirePermission(Permissions.CatalogManage);
    }

    private static RequestValidation Validate(CategoryRequest request, bool requireVersion) =>
        new RequestValidation()
            .Required(nameof(request.Name), request.Name, CatalogLimits.CategoryName)
            .RequireVersion(request.Version, requireVersion);

    private static CategoryResponse ToResponse(Category c, IImageStorage storage) =>
        new(c.Id, c.Name, c.IsActive, c.ImageKey, storage.GetUrl(c.ImageKey), c.Version, c.UpdatedAtUtc);
}
