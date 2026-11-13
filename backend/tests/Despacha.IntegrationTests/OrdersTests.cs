using System.Net;
using System.Net.Http.Json;
using static Despacha.IntegrationTests.CatalogTests;

namespace Despacha.IntegrationTests;

public class OrdersTests
{
    [Fact]
    public async Task El_servidor_calcula_el_total_y_el_estado_avanza_como_en_el_php()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;

        var category = await (await Send(client, HttpMethod.Post, "/api/catalog/categories", admin, new { name = "Aguardiente" })).Content.ReadFromJsonAsync<IdDto>();
        var product = await (await Send(client, HttpMethod.Post, "/api/catalog/products", admin, new
        {
            code = "101",
            name = "Media",
            categoryId = category!.Id,
            unitPrice = 12000m,
            packPrice = 130000m,
            unitsPerPack = 12,
            sellsByUnit = true,
        })).Content.ReadFromJsonAsync<IdDto>();
        var customer = await (await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Bar Central", phone = "3001112233", zone = "Chapinero" })).Content.ReadFromJsonAsync<CustomerSave>();

        var created = await Send(client, HttpMethod.Post, "/api/orders", admin, new
        {
            customerId = customer!.Customer.Id,
            note = "Sin hielo",
            lines = new[] { new { productId = product!.Id, kind = "unidad", quantity = 2, unitPrice = 1 } },
        });
        var order = await created.Content.ReadFromJsonAsync<OrderDto>();
        var accepted = await Send(client, HttpMethod.Post, $"/api/orders/{order!.Id}/transition", admin, new { action = "approve" });
        var sent = await Send(client, HttpMethod.Post, $"/api/orders/{order.Id}/transition", admin, new { action = "dispatch" });
        var early = await Send(client, HttpMethod.Post, $"/api/orders/{order.Id}/transition", admin, new { action = "approve" });
        var finished = await Send(client, HttpMethod.Post, $"/api/orders/{order.Id}/transition", admin, new { action = "deliverCash" });
        var listed = await (await Send(client, HttpMethod.Get, "/api/orders?status=delivered", admin)).Content.ReadFromJsonAsync<PageDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(24000m, order.Total);
        Assert.Equal(12000m, order.Lines[0].UnitPrice);
        Assert.StartsWith("PED-", order.Number, StringComparison.Ordinal);
        Assert.Equal("pendiente", order.Status);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("finalizaCo", (await finished.Content.ReadFromJsonAsync<OrderDto>())!.Status);
        Assert.Equal(1, listed!.Total);
    }

    [Fact]
    public async Task Rechazar_exige_su_permiso_y_solo_parte_de_pendiente()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var seller = (await DespachaApiFactory.LoginAsync(client, "vendedor.alfa")).AccessToken;

        var category = await (await Send(client, HttpMethod.Post, "/api/catalog/categories", admin, new { name = "Cerveza" })).Content.ReadFromJsonAsync<IdDto>();
        var product = await (await Send(client, HttpMethod.Post, "/api/catalog/products", admin, new
        {
            code = "202",
            name = "Lata",
            categoryId = category!.Id,
            unitPrice = 4000m,
            packPrice = 40000m,
            unitsPerPack = 12,
            sellsByUnit = true,
        })).Content.ReadFromJsonAsync<IdDto>();
        var customer = await (await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Tienda", phone = "3010001122" })).Content.ReadFromJsonAsync<CustomerSave>();
        var order = await (await Send(client, HttpMethod.Post, "/api/orders", admin, new
        {
            customerId = customer!.Customer.Id,
            lines = new[] { new { productId = product!.Id, kind = "unidad", quantity = 1 } },
        })).Content.ReadFromJsonAsync<OrderDto>();

        var forbidden = await Send(client, HttpMethod.Get, "/api/orders", seller);
        var rejected = await Send(client, HttpMethod.Post, $"/api/orders/{order!.Id}/reject", admin);
        var again = await Send(client, HttpMethod.Post, $"/api/orders/{order.Id}/transition", admin, new { action = "approve" });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("rechazado", (await rejected.Content.ReadFromJsonAsync<OrderDto>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    private sealed record IdDto(long Id);

    private sealed record CustomerSave(CustomerDto Customer);

    private sealed record CustomerDto(long Id);

    private sealed record OrderLineDto(decimal UnitPrice);

    private sealed record OrderDto(long Id, string Number, string Status, decimal Total, OrderLineDto[] Lines);

    private sealed record PageDto(int Total);
}
