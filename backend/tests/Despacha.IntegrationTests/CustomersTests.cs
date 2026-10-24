using System.Net;
using System.Net.Http.Json;
using static Despacha.IntegrationTests.CatalogTests;

namespace Despacha.IntegrationTests;

public class CustomersTests
{
    [Fact]
    public async Task El_telefono_se_normaliza_y_el_repetido_solo_se_advierte()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;

        var first = await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Bar La 85", phone = "300 123 4567", zone = "Chapinero" });
        var firstBody = await first.Content.ReadFromJsonAsync<SaveDto>();
        var second = await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Otra razón social", phone = "+57 3001234567" });
        var secondBody = await second.Content.ReadFromJsonAsync<SaveDto>();
        var invalid = await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Sin teléfono válido", phone = "12345" });
        var check = await (await Send(client, HttpMethod.Get, "/api/customers/customers/phone-check?phone=3001234567", admin)).Content.ReadFromJsonAsync<PhoneCheckDto>();
        var byPhone = await (await Send(client, HttpMethod.Get, "/api/customers/customers?phone=573001234567", admin)).Content.ReadFromJsonAsync<PageDto>();
        var byPartialPhone = await (await Send(client, HttpMethod.Get, "/api/customers/customers?q=123%204567", admin)).Content.ReadFromJsonAsync<PageDto>();
        var zones = await (await Send(client, HttpMethod.Get, "/api/customers/zones", admin)).Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("+573001234567", firstBody!.Customer.Phone);
        Assert.Empty(firstBody.SamePhoneCustomerIds);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal([firstBody.Customer.Id], secondBody!.SamePhoneCustomerIds);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await ErrorsAsync(invalid)).ContainsKey("Phone"));
        Assert.Equal(2, check!.SamePhoneCustomerIds.Length);
        Assert.Equal(2, byPhone!.Total);
        Assert.Equal(2, byPartialPhone!.Total);
        Assert.Equal(["Chapinero"], zones!);
    }

    [Fact]
    public async Task El_cliente_se_asocia_a_un_establecimiento_activo()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;

        var created = await Send(client, HttpMethod.Post, "/api/customers/establishments", admin, new { name = "Bar Central", address = "Calle 1" });
        var establishment = await created.Content.ReadFromJsonAsync<EstablishmentDto>();
        var duplicate = await Send(client, HttpMethod.Post, "/api/customers/establishments", admin, new { name = "bar central" });
        var customer = await (await Send(client, HttpMethod.Post, "/api/customers/customers", admin,
            new { name = "Cliente", phone = "3109876543", establishmentId = establishment!.Id })).Content.ReadFromJsonAsync<SaveDto>();
        await Send(client, HttpMethod.Patch, $"/api/customers/establishments/{establishment.Id}/status", admin, new { isActive = false, version = establishment.Version });
        var toInactive = await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Nuevo", phone = "3109876544", establishmentId = establishment.Id });
        var keepInactive = await Send(client, HttpMethod.Put, $"/api/customers/customers/{customer!.Customer.Id}", admin,
            new { name = "Cliente editado", phone = "3109876543", establishmentId = establishment.Id, version = customer.Customer.Version });
        var filtered = await (await Send(client, HttpMethod.Get, $"/api/customers/customers?establishmentId={establishment.Id}", admin)).Content.ReadFromJsonAsync<PageDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("Bar Central", customer.Customer.EstablishmentName);
        Assert.Contains("El establecimiento está inactivo.", (await ErrorsAsync(toInactive))["EstablishmentId"]);
        Assert.Equal(HttpStatusCode.OK, keepInactive.StatusCode);
        Assert.Equal(1, filtered!.Total);
    }

    [Fact]
    public async Task El_vendedor_no_ve_todos_los_clientes_y_otra_empresa_tampoco()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.ProvisionTenantAsync("beta", "admin.beta");
        await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var seller = (await DespachaApiFactory.LoginAsync(client, "vendedor.alfa")).AccessToken;
        var beta = (await DespachaApiFactory.LoginAsync(client, "admin.beta")).AccessToken;
        var customer = await (await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Cliente alfa", phone = "3001112233" }))
            .Content.ReadFromJsonAsync<SaveDto>();

        var asSeller = await Send(client, HttpMethod.Get, "/api/customers/customers", seller);
        var betaList = await (await Send(client, HttpMethod.Get, "/api/customers/customers", beta)).Content.ReadFromJsonAsync<PageDto>();
        var betaDetail = await Send(client, HttpMethod.Get, $"/api/customers/customers/{customer!.Customer.Id}", beta);
        var betaStatus = await Send(client, HttpMethod.Patch, $"/api/customers/customers/{customer.Customer.Id}/status", beta, new { isActive = false, version = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, asSeller.StatusCode);
        Assert.Equal(0, betaList!.Total);
        Assert.Equal(HttpStatusCode.NotFound, betaDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, betaStatus.StatusCode);
    }

    [Fact]
    public async Task Dos_ediciones_simultaneas_del_cliente_responden_409()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var customer = await (await Send(client, HttpMethod.Post, "/api/customers/customers", admin, new { name = "Cliente", phone = "3001112233" }))
            .Content.ReadFromJsonAsync<SaveDto>();
        var url = $"/api/customers/customers/{customer!.Customer.Id}";

        var first = await Send(client, HttpMethod.Put, url, admin, new { name = "Primero", phone = "3001112233", version = 1 });
        var second = await Send(client, HttpMethod.Put, url, admin, new { name = "Segundo", phone = "3001112233", version = 1 });
        var current = await (await Send(client, HttpMethod.Get, url, admin)).Content.ReadFromJsonAsync<CustomerDto>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("modificado por otra persona", await second.Content.ReadAsStringAsync());
        Assert.Equal("Primero", current!.Name);
    }

    private sealed record CustomerDto(long Id, string Name, string Phone, long? EstablishmentId, string? EstablishmentName, int Version);

    private sealed record SaveDto(CustomerDto Customer, long[] SamePhoneCustomerIds);

    private sealed record PhoneCheckDto(bool IsValid, string? Phone, long[] SamePhoneCustomerIds);

    private sealed record EstablishmentDto(long Id, string Name, int Version);

    private sealed record PageDto(CustomerDto[] Items, int Total);
}
