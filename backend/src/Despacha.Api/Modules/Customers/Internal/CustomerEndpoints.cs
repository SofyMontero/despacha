using System.Security.Claims;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Customers.Internal;

internal static class CustomerEndpoints
{
    private const int MinDigitsForPhoneSearch = 3;
    private const string InvalidPhone = "Use un número colombiano de 10 dígitos, con o sin el indicativo 57.";

    public static void Map(RouteGroupBuilder group)
    {
        var customers = group.MapGroup("/customers");

        customers.MapGet("", async ([AsParameters] CustomerQuery query, ITenantDbContextFactory<CustomersDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = db.Customers.AsNoTracking().Include(c => c.Establishment).AsQueryable();
            if (RequestValidation.Clean(query.Q) is { } q)
            {
                var digits = PhoneNumbers.DigitsOf(q);
                items = digits.Length >= MinDigitsForPhoneSearch
                    ? items.Where(c => c.Name.Contains(q) || (c.Identification != null && c.Identification.Contains(q)) || c.Phone.Contains(digits))
                    : items.Where(c => c.Name.Contains(q) || (c.Identification != null && c.Identification.Contains(q)));
            }

            if (RequestValidation.Clean(query.Phone) is { } phone)
            {
                if (!PhoneNumbers.TryNormalizeColombian(phone, out var normalized))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(query.Phone)] = [InvalidPhone] });
                }

                var forms = PhoneNumbers.EquivalentStored(normalized);
                items = items.Where(c => forms.Contains(c.Phone));
            }

            if (query.EstablishmentId is long establishmentId)
            {
                items = items.Where(c => c.EstablishmentId == establishmentId);
            }

            if (RequestValidation.Clean(query.Zone) is { } zone)
            {
                items = items.Where(c => c.Zone == zone);
            }

            if (query.Active is bool active)
            {
                items = items.Where(c => c.IsActive == active);
            }

            return Results.Ok(await items.OrderByDescending(c => c.Id)
                .ToPageAsync(PageQuery.From(query.Page, query.PageSize), ToResponse, ct));
        }).RequirePermission(Permissions.CustomersRead)
            .Produces<PagedResponse<CustomerResponse>>()
            .ProducesValidationProblem();

        customers.MapGet("/{id:long}", async Task<Results<Ok<CustomerResponse>, NotFound>> (
            long id, ITenantDbContextFactory<CustomersDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var customer = await db.Customers.AsNoTracking().Include(c => c.Establishment).SingleOrDefaultAsync(c => c.Id == id, ct);
            return customer is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(customer));
        }).RequirePermission(Permissions.CustomersRead);

        customers.MapGet("/phone-check", async Task<Ok<PhoneCheckResponse>> (
            string? phone, long? excludeId, ITenantDbContextFactory<CustomersDbContext> factory, CancellationToken ct) =>
        {
            if (!PhoneNumbers.TryNormalizeColombian(phone, out var normalized))
            {
                return TypedResults.Ok(new PhoneCheckResponse(false, null, []));
            }

            await using var db = await factory.CreateAsync(ct);
            return TypedResults.Ok(new PhoneCheckResponse(true, normalized, await SamePhoneAsync(db, normalized, excludeId, ct)));
        }).RequirePermission(Permissions.CustomersRead);

        customers.MapPost("", async Task<Results<Created<CustomerSaveResponse>, ValidationProblem>> (
            CustomerRequest request, ITenantDbContextFactory<CustomersDbContext> factory,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var (validation, phone) = await ValidateAsync(request, db, current: null, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            var now = clock.GetUtcNow().UtcDateTime;
            var customer = new Customer { RegisteredAtUtc = now, IsActive = request.IsActive ?? true, CompanyId = 1 };
            Apply(customer, request, phone, keepStoredPhone: false);
            customer.MarkCreated(user.GetUserId(), now);
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            await db.Entry(customer).Reference(c => c.Establishment).LoadAsync(ct);
            return TypedResults.Created($"/api/customers/customers/{customer.Id}",
                new CustomerSaveResponse(ToResponse(customer), await SamePhoneAsync(db, phone, customer.Id, ct)));
        }).RequirePermission(Permissions.CustomersManage);

        customers.MapPut("/{id:long}", async Task<Results<Ok<CustomerSaveResponse>, NotFound, ValidationProblem>> (
            long id, CustomerRequest request, ITenantDbContextFactory<CustomersDbContext> factory,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);
            if (customer is null)
            {
                return TypedResults.NotFound();
            }

            var (validation, phone) = await ValidateAsync(request, db, customer, ct);
            if (!validation.IsValid)
            {
                return validation.ToProblem();
            }

            db.ExpectVersion(customer, request.Version!.Value);
            Apply(customer, request, phone, keepStoredPhone: PhoneNumbers.SameDigits(customer.Phone, request.Phone));
            customer.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            await db.Entry(customer).Reference(c => c.Establishment).LoadAsync(ct);
            return TypedResults.Ok(new CustomerSaveResponse(ToResponse(customer), await SamePhoneAsync(db, phone, customer.Id, ct)));
        }).RequirePermission(Permissions.CustomersManage);

        customers.MapPatch("/{id:long}/status", async Task<Results<Ok<CustomerResponse>, NotFound>> (
            long id, StatusRequest request, ITenantDbContextFactory<CustomersDbContext> factory,
            ClaimsPrincipal user, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var customer = await db.Customers.Include(c => c.Establishment).SingleOrDefaultAsync(c => c.Id == id, ct);
            if (customer is null)
            {
                return TypedResults.NotFound();
            }

            db.ExpectVersion(customer, request.Version);
            customer.IsActive = request.IsActive;
            customer.MarkUpdated(user.GetUserId(), clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ToResponse(customer));
        }).RequirePermission(Permissions.CustomersManage);

        group.MapGet("/zones", async (ITenantDbContextFactory<CustomersDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var zones = await db.Customers.AsNoTracking()
                .Where(c => c.Zone != "")
                .Select(c => c.Zone)
                .Distinct()
                .OrderBy(z => z)
                .ToListAsync(ct);
            return TypedResults.Ok(zones);
        }).RequirePermission(Permissions.CustomersRead);
    }

    /// <summary>Como <c>api/cliente.php</c>: nombre y teléfono obligatorios; identificación, dirección, zona y establecimiento opcionales.</summary>
    private static async Task<(RequestValidation Validation, string Phone)> ValidateAsync(
        CustomerRequest request, CustomersDbContext db, Customer? current, CancellationToken ct)
    {
        var validation = new RequestValidation()
            .Required(nameof(request.Name), request.Name, CustomerLimits.Name)
            .MaxLength(nameof(request.Identification), request.Identification, CustomerLimits.Identification)
            .MaxLength(nameof(request.Address), request.Address, CustomerLimits.Address)
            .MaxLength(nameof(request.Zone), request.Zone, CustomerLimits.Zone);
        if (current is not null && request.Version is null)
        {
            validation.Add(nameof(request.Version), "Es obligatorio.");
        }

        var phone = string.Empty;
        var keepsStored = current is not null && PhoneNumbers.SameDigits(current.Phone, request.Phone);
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            validation.Add(nameof(request.Phone), "Es obligatorio.");
        }
        else if (keepsStored)
        {
            phone = current!.Phone;
        }
        else if (!PhoneNumbers.TryNormalizeColombian(request.Phone, out phone))
        {
            validation.Add(nameof(request.Phone), InvalidPhone);
        }

        if (request.EstablishmentId is long establishmentId)
        {
            var active = await db.Establishments.AsNoTracking().Where(e => e.Id == establishmentId).Select(e => (bool?)e.IsActive).SingleOrDefaultAsync(ct);
            if (active is null)
            {
                validation.Add(nameof(request.EstablishmentId), "El establecimiento no existe.");
            }
            else if (active == false && current?.EstablishmentId != establishmentId)
            {
                validation.Add(nameof(request.EstablishmentId), "El establecimiento está inactivo.");
            }
        }

        return (validation, phone);
    }

    private static Task<long[]> SamePhoneAsync(CustomersDbContext db, string phone, long? excludeId, CancellationToken ct)
    {
        var forms = PhoneNumbers.TryNormalizeColombian(phone, out var normalized)
            ? PhoneNumbers.EquivalentStored(normalized)
            : PhoneNumbers.EquivalentStored("+" + PhoneNumbers.DigitsOf(phone));
        return db.Customers.AsNoTracking()
            .Where(c => forms.Contains(c.Phone) && c.IsActive && c.Id != excludeId)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(20)
            .ToArrayAsync(ct);
    }

    private static void Apply(Customer customer, CustomerRequest request, string phone, bool keepStoredPhone)
    {
        customer.Name = request.Name!.Trim();
        var identification = RequestValidation.Clean(request.Identification);
        if (identification is not null)
        {
            customer.Identification = identification;
            if (int.TryParse(identification, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var document) && document > 0)
            {
                customer.LegacyIdentification = document;
            }
        }
        else if (customer.Id != 0)
        {
            customer.Identification = "";
        }

        if (!keepStoredPhone && !string.IsNullOrEmpty(customer.Phone) && !PhoneNumbers.SameDigits(customer.Phone, phone) && string.IsNullOrEmpty(customer.PhoneOriginal))
        {
            customer.PhoneOriginal = customer.Phone;
        }

        customer.Phone = keepStoredPhone ? customer.Phone : phone;
        customer.Address = RequestValidation.Clean(request.Address) ?? "";
        customer.Zone = RequestValidation.Clean(request.Zone) ?? "";
        customer.EstablishmentId = request.EstablishmentId;
    }

    private static CustomerResponse ToResponse(Customer c) =>
        new(c.Id, c.Name, c.ShownIdentification, c.Phone, string.IsNullOrEmpty(c.Address) ? null : c.Address,
            string.IsNullOrEmpty(c.Zone) ? null : c.Zone, c.EstablishmentId, c.Establishment?.Name,
            c.RegisteredAtUtc, c.IsActive, c.Version, c.UpdatedAtUtc);
}
