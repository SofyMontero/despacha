using System.Security.Claims;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Customers.Internal;

internal static class EstablishmentEndpoints
{
    public static void Map(RouteGroupBuilder customers)
    {
        var establishments = customers.MapGroup("/establishments");

        establishments.MapGet("", async ([AsParameters] EstablishmentQuery query, ITenantDbContextFactory<CustomersDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = db.Establishments.AsNoTracking();
            if (RequestValidation.Clean(query.Q) is { } q)
            {
                items = items.Where(e => e.Name.Contains(q) || (e.Address != null && e.Address.Contains(q)));
            }

            if (query.Active is bool active)
            {
                items = items.Where(e => e.IsActive == active);
            }

            return TypedResults.Ok(await items.OrderBy(e => e.Name).ThenBy(e => e.Id)
                .ToPageAsync(PageQuery.From(query.Page, query.PageSize), ToResponse, ct));
        }).RequirePermission(Permissions.CustomersRead);

        establishments.MapGet("/{id:long}", async Task<Results<Ok<EstablishmentResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CustomersDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var establishment = await db.Establishments.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
            return establishment is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(establishment));
        }).RequirePermission(Permissions.CustomersRead);

        establishments.MapPost("", async Task<Results<Created<EstablishmentResponse>, ValidationProblem>> (
            EstablishmentRequest request, ITenantDbContextFactory<CustomersDbContext> factory,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var validation = await ValidateAsync(request, db, currentId: null, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            var now = clock.GetUtcNow().UtcDateTime;
            var establishment = new Establishment { OpenedAtUtc = now };
            Apply(establishment, request);
            establishment.MarkCreated(user.GetUserId(), now);
            db.Establishments.Add(establishment);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/api/customers/establishments/{establishment.Id}", ToResponse(establishment));
        }).RequirePermission(Permissions.CustomersManage);

        establishments.MapPut("/{id:long}", async Task<Results<Ok<EstablishmentResponse>, NotFound, ValidationProblem>> (
            long id, EstablishmentRequest request, ITenantDbContextFactory<CustomersDbContext> factory,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var establishment = await db.Establishments.SingleOrDefaultAsync(e => e.Id == id, ct);
            if (establishment is null)
            {
                return TypedResults.NotFound();
            }

            var validation = await ValidateAsync(request, db, id, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            db.ExpectVersion(establishment, request.Version!.Value);
            Apply(establishment, request);
            establishment.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToResponse(establishment));
        }).RequirePermission(Permissions.CustomersManage);

        establishments.MapPatch("/{id:long}/status", async Task<Results<Ok<EstablishmentResponse>, NotFound>> (
            long id, StatusRequest request, ITenantDbContextFactory<CustomersDbContext> factory,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var establishment = await db.Establishments.SingleOrDefaultAsync(e => e.Id == id, ct);
            if (establishment is null)
            {
                return TypedResults.NotFound();
            }

            db.ExpectVersion(establishment, request.Version);
            establishment.IsActive = request.IsActive;
            establishment.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToResponse(establishment));
        }).RequirePermission(Permissions.CustomersManage);
    }

    /// <summary>Como <c>BarModel::existeBar</c>: el nombre no se repite (sin distinguir mayúsculas).</summary>
    private static async Task<RequestValidation> ValidateAsync(EstablishmentRequest request, CustomersDbContext db, long? currentId, CancellationToken ct)
    {
        var validation = new RequestValidation()
            .Required(nameof(request.Name), request.Name, CustomerLimits.EstablishmentName)
            .MaxLength(nameof(request.Address), request.Address, CustomerLimits.Address);
        if (currentId is not null && request.Version is null)
        {
            validation.Add(nameof(request.Version), "Es obligatorio.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim().ToLowerInvariant();
            if (await db.Establishments.AnyAsync(e => e.Name.ToLower() == name && e.Id != currentId, ct))
            {
                validation.Add(nameof(request.Name), "Ya existe un establecimiento con ese nombre.");
            }
        }

        return validation;
    }

    private static void Apply(Establishment establishment, EstablishmentRequest request)
    {
        establishment.Name = request.Name!.Trim();
        establishment.Address = RequestValidation.Clean(request.Address);
    }

    private static EstablishmentResponse ToResponse(Establishment e) =>
        new(e.Id, e.Name, e.Address, e.IsActive, e.Version, e.UpdatedAtUtc);
}
