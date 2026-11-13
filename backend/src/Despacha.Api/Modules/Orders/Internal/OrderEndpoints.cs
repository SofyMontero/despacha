using System.Data;
using System.Data.Common;
using System.Globalization;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Orders.Internal;

internal static class OrderEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var orders = endpoints.MapGroup("/api/orders").WithTags("Orders");

        orders.MapGet("", async ([AsParameters] OrderQuery query, ITenantDbContextFactory<OrdersDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var items = db.Orders.AsNoTracking();
            var statuses = OrderWorkflow.Matches(query.Status);
            if (statuses.Count > 0)
            {
                items = items.Where(order => statuses.Contains(order.Status));
            }

            if (query.From is DateOnly from)
            {
                var start = from.ToDateTime(TimeOnly.MinValue);
                items = items.Where(order => order.CreatedAt >= start);
            }

            if (query.To is DateOnly to)
            {
                var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
                items = items.Where(order => order.CreatedAt < end);
            }

            if (RequestValidation.Clean(query.Q) is { } q)
            {
                items = items.Where(order => order.Number.Contains(q) || (order.Phone != null && order.Phone.Contains(q)));
            }

            var page = await items.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
                .ToPageAsync(PageQuery.From(query.Page, query.PageSize), order => order, ct);
            var names = await CustomerNamesAsync(db, page.Items.Select(order => order.CustomerId), ct);
            return TypedResults.Ok(new PagedResponse<OrderSummary>(page.Items.Select(order => ToSummary(order, names)).ToList(), page.Total, page.Page, page.PageSize));
        }).RequirePermission("orders.read");

        orders.MapGet("/{id:long}", async Task<Results<Ok<OrderDetail>, NotFound>> (
            long id, ITenantDbContextFactory<OrdersDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            var order = await db.Orders.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, ct);
            if (order is null)
            {
                return TypedResults.NotFound();
            }

            var names = await CustomerNamesAsync(db, [order.CustomerId], ct);
            return TypedResults.Ok(ToDetail(order, names));
        }).RequirePermission("orders.read");

        orders.MapPost("", async Task<Results<Created<OrderDetail>, ValidationProblem>> (
            OrderRequest request, ITenantDbContextFactory<OrdersDbContext> factory, TimeProvider clock, CancellationToken ct) =>
        {
            await using var db = await factory.CreateAsync(ct);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var built = await BuildAsync(db, request, clock, ct);
                if (built.Problem is not null)
                {
                    return built.Problem;
                }

                db.Orders.Add(built.Order!);
                try
                {
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    var names = await CustomerNamesAsync(db, [built.Order!.CustomerId], ct);
                    return TypedResults.Created($"/api/orders/{built.Order.Id}", ToDetail(built.Order, names));
                }
                catch (DbUpdateException ex) when (attempt < 2 && IsDuplicateNumber(ex))
                {
                    db.ChangeTracker.Clear();
                }
            }

            return RequestValidation.Single("Number", "No se pudo asignar un número de pedido. Intente de nuevo.");
        }).RequirePermission("orders.approve");

        orders.MapPost("/{id:long}/transition", async Task<Results<Ok<OrderDetail>, NotFound, ValidationProblem>> (
            long id, TransitionRequest request, ITenantDbContextFactory<OrdersDbContext> factory, CancellationToken ct) =>
        {
            if (request.Action == OrderWorkflow.Reject)
            {
                return RequestValidation.Single("Action", "El rechazo usa su propio permiso.");
            }

            return await TransitionAsync(id, request.Action, factory, ct);
        }).RequirePermission("orders.approve");

        orders.MapPost("/{id:long}/reject", async Task<Results<Ok<OrderDetail>, NotFound, ValidationProblem>> (
            long id, ITenantDbContextFactory<OrdersDbContext> factory, CancellationToken ct) =>
            await TransitionAsync(id, OrderWorkflow.Reject, factory, ct)).RequirePermission("orders.cancel");
    }

    private static async Task<Results<Ok<OrderDetail>, NotFound, ValidationProblem>> TransitionAsync(
        long id, string? action, ITenantDbContextFactory<OrdersDbContext> factory, CancellationToken ct)
    {
        var target = OrderWorkflow.Target(action);
        if (target is null)
        {
            return RequestValidation.Single("Action", "Acción desconocida.");
        }

        await using var db = await factory.CreateAsync(ct);
        var order = await db.Orders.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (!OrderWorkflow.CanMove(OrderWorkflow.Parse(order.Status), target.Value))
        {
            throw new OrderTransitionException();
        }

        var previous = order.Status;
        var next = OrderWorkflow.Persist(target.Value);
        var updated = await db.Orders.Where(item => item.Id == id && item.Status == previous)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, next), ct);
        if (updated == 0)
        {
            throw new OrderTransitionException();
        }

        order.Status = next;
        var names = await CustomerNamesAsync(db, [order.CustomerId], ct);
        return TypedResults.Ok(ToDetail(order, names));
    }

    private static async Task<(CustomerOrder? Order, ValidationProblem? Problem)> BuildAsync(
        OrdersDbContext db, OrderRequest request, TimeProvider clock, CancellationToken ct)
    {
        var validation = new RequestValidation();
        var customerId = request.CustomerId ?? 0;
        if (customerId <= 0)
        {
            validation.Add("CustomerId", "Es obligatorio.");
        }

        if (request.Lines is not { Count: > 0 })
        {
            validation.Add("Lines", "Agregue al menos un producto.");
        }

        validation.MaxLength("Note", request.Note, 2000);
        if (!validation.IsValid)
        {
            return (null, validation.ToProblem());
        }

        var customer = await ReadCustomerAsync(db, customerId, ct);
        if (customer is null)
        {
            return (null, RequestValidation.Single("CustomerId", "El cliente no existe."));
        }

        if (customer.IsActive != 1)
        {
            return (null, RequestValidation.Single("CustomerId", "El cliente está inactivo."));
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, Bogota());
        var order = new CustomerOrder
        {
            CustomerId = customer.Id,
            Status = OrderWorkflow.Persist(OrderStage.Pending),
            CreatedAt = now,
            Phone = customer.Phone,
            Note = RequestValidation.Clean(request.Note),
            Address = customer.Address,
        };

        var index = 0;
        foreach (var line in request.Lines ?? [])
        {
            var field = $"Lines[{index}]";
            index++;
            var kind = line.Kind?.Trim().ToLowerInvariant();
            if (kind is not ("unidad" or "paca"))
            {
                validation.Add($"{field}.Kind", "Use unidad o paca.");
                continue;
            }

            if (line.Quantity is not int quantity || quantity < 1)
            {
                validation.Add($"{field}.Quantity", "La cantidad debe ser al menos 1.");
                continue;
            }

            if (line.ProductId is not long productId)
            {
                validation.Add($"{field}.ProductId", "Es obligatorio.");
                continue;
            }

            var product = await ReadProductAsync(db, productId, ct);
            if (product is null || product.IsActive != 1)
            {
                validation.Add($"{field}.ProductId", "El producto no existe o está inactivo.");
                continue;
            }

            if (kind == "unidad" && product.SellsByUnit != 1)
            {
                validation.Add($"{field}.Kind", "Este producto solo se vende por paca.");
                continue;
            }

            var price = kind == "paca" ? product.PackPrice : product.UnitPrice;
            order.Lines.Add(new OrderLine
            {
                ProductId = product.Id,
                Name = product.Name,
                Kind = kind,
                Quantity = quantity,
                UnitPrice = price,
                Subtotal = price * quantity,
            });
        }

        if (!validation.IsValid)
        {
            return (null, validation.ToProblem());
        }

        order.Total = order.Lines.Sum(line => line.Subtotal);
        var day = DateOnly.FromDateTime(now);
        var prefix = $"PED-{day:yyyyMMdd}-";
        var existing = await db.Orders.Where(item => item.Number.StartsWith(prefix)).Select(item => item.Number).ToListAsync(ct);
        order.Number = OrderWorkflow.NextNumber(day, existing);
        return (order, null);
    }

    private static TimeZoneInfo Bogota() => TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static bool IsDuplicateNumber(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
        || exception.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<Dictionary<long, string>> CustomerNamesAsync(OrdersDbContext db, IEnumerable<long> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToArray();
        var names = new Dictionary<long, string>();
        if (wanted.Length == 0)
        {
            return names;
        }

        var connection = await OpenAsync(db, ct);
        foreach (var id in wanted)
        {
            var customer = await ReadCustomerAsync(connection, id, ct);
            if (customer is not null)
            {
                names[customer.Id] = customer.Name;
            }
        }

        return names;
    }

    private static async Task<CustomerRow?> ReadCustomerAsync(OrdersDbContext db, long id, CancellationToken ct) =>
        await ReadCustomerAsync(await OpenAsync(db, ct), id, ct);

    private static async Task<CustomerRow?> ReadCustomerAsync(DbConnection connection, long id, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id_cliente, cli_nombre, cli_telefono, cli_direccion, activo FROM clientes WHERE id_cliente = @id";
        AddId(command, id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new CustomerRow(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? 1 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture));
    }

    private static async Task<ProductRow?> ReadProductAsync(OrdersDbContext db, long id, CancellationToken ct)
    {
        var connection = await OpenAsync(db, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id_producto, descripcion_producto, precio_unidad_producto, precio_paca_producto, acti_Unidad, activo
            FROM productos WHERE id_producto = @id
            """;
        AddId(command, id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ProductRow(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture));
    }

    private static async Task<DbConnection> OpenAsync(OrdersDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        return connection;
    }

    private static void AddId(DbCommand command, long id)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
    }

    private static OrderSummary ToSummary(CustomerOrder order, IReadOnlyDictionary<long, string> names)
    {
        var stage = OrderWorkflow.Parse(order.Status);
        return new OrderSummary(order.Id, order.Number, order.Status, OrderWorkflow.Label(stage), order.CreatedAt, order.CustomerId, names.GetValueOrDefault(order.CustomerId), order.Phone, order.Total);
    }

    private static OrderDetail ToDetail(CustomerOrder order, IReadOnlyDictionary<long, string> names)
    {
        var summary = ToSummary(order, names);
        var lines = order.Lines.OrderBy(line => line.Id).Select(line =>
            new OrderLineResponse(line.Id, line.ProductId, line.Name, line.Kind, line.Quantity, line.UnitPrice, line.Subtotal)).ToList();
        return new OrderDetail(summary.Id, summary.Number, summary.Status, summary.StatusLabel, summary.CreatedAt, summary.CustomerId, summary.CustomerName, summary.Phone, order.Address, order.Note, summary.Total, lines);
    }

    private sealed record CustomerRow(long Id, string Name, string? Phone, string? Address, int IsActive);

    private sealed record ProductRow(long Id, string Name, decimal UnitPrice, decimal PackPrice, int SellsByUnit, int IsActive);
}

internal sealed class OrderTransitionException()
    : DespachaException(StatusCodes.Status409Conflict, "El pedido no admite ese cambio de estado.");

internal sealed record OrderQuery(string? Status, DateOnly? From, DateOnly? To, string? Q, int? Page, int? PageSize);

internal sealed record OrderLineRequest(long? ProductId, string? Kind, int? Quantity);

internal sealed record OrderRequest(long? CustomerId, string? Note, IReadOnlyList<OrderLineRequest>? Lines);

internal sealed record TransitionRequest(string? Action);

internal sealed record OrderSummary(long Id, string Number, string Status, string StatusLabel, DateTime CreatedAt, long CustomerId, string? CustomerName, string? Phone, decimal Total);

internal sealed record OrderLineResponse(long Id, long? ProductId, string Name, string Kind, int Quantity, decimal UnitPrice, decimal Subtotal);

internal sealed record OrderDetail(
    long Id, string Number, string Status, string StatusLabel, DateTime CreatedAt, long CustomerId, string? CustomerName,
    string? Phone, string? Address, string? Note, decimal Total, IReadOnlyList<OrderLineResponse> Lines);
