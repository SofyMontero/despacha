using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Despacha.IntegrationTests;

public class CatalogTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Fact]
    public async Task El_administrador_crea_edita_y_desactiva_productos()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var category = await CreateCategoryAsync(client, admin, "Licores");

        var created = await Send(client, HttpMethod.Post, "/api/catalog/products", admin, Product("101", "Aguardiente", category.Id));
        var product = await created.Content.ReadFromJsonAsync<ProductDto>();
        var duplicate = await Send(client, HttpMethod.Post, "/api/catalog/products", admin, Product(" 101 ", "Otro", category.Id));
        var updated = await Send(client, HttpMethod.Put, $"/api/catalog/products/{product!.Id}", admin,
            Product("101", "Aguardiente 750", category.Id, unitPrice: 45000.50m, version: product.Version));
        var stale = await Send(client, HttpMethod.Put, $"/api/catalog/products/{product.Id}", admin,
            Product("101", "Cambio perdido", category.Id, version: product.Version));
        var afterUpdate = await updated.Content.ReadFromJsonAsync<ProductDto>();
        var deactivated = await Send(client, HttpMethod.Patch, $"/api/catalog/products/{product.Id}/status", admin,
            new { isActive = false, version = afterUpdate!.Version });
        var activeList = await (await Send(client, HttpMethod.Get, "/api/catalog/products?active=true&q=Agua", admin)).Content.ReadFromJsonAsync<PageDto<ProductDto>>();
        var inactiveList = await (await Send(client, HttpMethod.Get, "/api/catalog/products?active=false&q=Agua", admin)).Content.ReadFromJsonAsync<PageDto<ProductDto>>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Licores", product.CategoryName);
        Assert.Equal(1, product.Version);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("Ya existe un producto con ese código.", await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Aguardiente 750", afterUpdate.Name);
        Assert.Equal(45000.50m, afterUpdate.UnitPrice);
        Assert.Equal(2, afterUpdate.Version);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.Equal(0, activeList!.Total);
        Assert.Equal([product.Id], inactiveList!.Items.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Valida_los_datos_del_producto_como_el_PHP()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var category = await CreateCategoryAsync(client, admin, "Cervezas");
        await Send(client, HttpMethod.Patch, $"/api/catalog/categories/{category.Id}/status", admin, new { isActive = false, version = category.Version });

        var invalid = await Send(client, HttpMethod.Post, "/api/catalog/products", admin,
            new { code = "", name = "", categoryId = 999, unitPrice = 0m, packPrice = -1m, unitsPerPack = 0 });
        var inactiveCategory = await Send(client, HttpMethod.Post, "/api/catalog/products", admin, Product("301", "Cerveza", category.Id));
        var textCode = await Send(client, HttpMethod.Post, "/api/catalog/products", admin, Product("ABC", "Cerveza", category.Id));
        var errors = await ErrorsAsync(invalid);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(["CategoryId", "Code", "Name", "PackPrice", "UnitPrice", "UnitsPerPack"], errors.Keys.Order().ToArray());
        Assert.Contains("La categoría está inactiva.", (await ErrorsAsync(inactiveCategory))["CategoryId"]);
        Assert.Contains("número entero", (await ErrorsAsync(textCode))["Code"][0]);
    }

    [Fact]
    public async Task Las_promociones_exigen_codigo_numerico_y_se_ordenan_por_prioridad()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var category = await CreateCategoryAsync(client, admin, "Licores");
        var product = await (await Send(client, HttpMethod.Post, "/api/catalog/products", admin, Product("100", "Ron", category.Id)))
            .Content.ReadFromJsonAsync<ProductDto>();

        var invalid = await Send(client, HttpMethod.Post, "/api/catalog/promotions", admin, Promotion("abc", null, 0));
        var zero = await Send(client, HttpMethod.Post, "/api/catalog/promotions", admin, Promotion("000", null, 0));
        var low = await Send(client, HttpMethod.Post, "/api/catalog/promotions", admin, Promotion("100", product!.Id, 1));
        var high = await Send(client, HttpMethod.Post, "/api/catalog/promotions", admin, Promotion("100", null, 5));
        var list = await (await Send(client, HttpMethod.Get, "/api/catalog/promotions", admin)).Content.ReadFromJsonAsync<PageDto<PromotionDto>>();

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await ErrorsAsync(invalid)).ContainsKey("Code"));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.Created, low.StatusCode);
        Assert.Equal(HttpStatusCode.Created, high.StatusCode);
        Assert.Equal([5, 1], list!.Items.Select(p => p.Priority).ToArray());
        Assert.Equal("Ron", list.Items[1].ProductName);
    }

    [Fact]
    public async Task El_vendedor_lee_el_catalogo_pero_no_lo_modifica()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        var alfa = await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.AddMemberAsync(alfa, "vendedor.alfa", "Vendedor");
        using var client = factory.CreateClient();
        var seller = (await DespachaApiFactory.LoginAsync(client, "vendedor.alfa")).AccessToken;

        var read = await Send(client, HttpMethod.Get, "/api/catalog/products", seller);
        var create = await Send(client, HttpMethod.Post, "/api/catalog/categories", seller, new { name = "No permitida" });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Una_empresa_no_ve_ni_modifica_el_catalogo_de_otra()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        await factory.ProvisionTenantAsync("beta", "admin.beta");
        using var client = factory.CreateClient();
        var alfa = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var beta = (await DespachaApiFactory.LoginAsync(client, "admin.beta")).AccessToken;
        var category = await CreateCategoryAsync(client, alfa, "Solo alfa");

        var list = await (await Send(client, HttpMethod.Get, "/api/catalog/categories", beta)).Content.ReadFromJsonAsync<PageDto<CategoryDto>>();
        var detail = await Send(client, HttpMethod.Get, $"/api/catalog/categories/{category.Id}", beta);
        var update = await Send(client, HttpMethod.Put, $"/api/catalog/categories/{category.Id}", beta, new { name = "Robada", version = category.Version });
        var stillAlfa = await (await Send(client, HttpMethod.Get, $"/api/catalog/categories/{category.Id}", alfa)).Content.ReadFromJsonAsync<CategoryDto>();

        Assert.Equal(0, list!.Total);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal("Solo alfa", stillAlfa!.Name);
    }

    [Fact]
    public async Task La_imagen_se_valida_por_contenido_y_se_sirve_fuera_del_repositorio()
    {
        await using var factory = new DespachaApiFactory().WithSqliteTenantDatabases();
        await factory.ProvisionTenantAsync("alfa", "admin.alfa");
        using var client = factory.CreateClient();
        var admin = (await DespachaApiFactory.LoginAsync(client, "admin.alfa")).AccessToken;
        var category = await CreateCategoryAsync(client, admin, "Licores");

        var fake = await UploadAsync(client, admin, $"/api/catalog/categories/{category.Id}/image", "<?php ?>"u8.ToArray(), "foto.png");
        var uploaded = await UploadAsync(client, admin, $"/api/catalog/categories/{category.Id}/image", Png, "foto.png");
        var withImage = await uploaded.Content.ReadFromJsonAsync<CategoryDto>();
        var media = await client.GetAsync(withImage!.ImageUrl);
        var traversal = await client.GetAsync("/media/..%2F..%2Fappsettings.json");
        var removed = await (await Send(client, HttpMethod.Delete, $"/api/catalog/categories/{category.Id}/image", admin)).Content.ReadFromJsonAsync<CategoryDto>();
        var gone = await client.GetAsync(withImage.ImageUrl);

        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        Assert.StartsWith("/media/t", withImage.ImageUrl);
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        Assert.Equal("image/png", media.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await media.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, traversal.StatusCode);
        Assert.Null(removed!.ImageKey);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    internal static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, string token, object? body = null) =>
        client.SendAsync(DespachaApiFactory.Authorized(method, url, token, body));

    internal static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string token, string name)
    {
        var response = await Send(client, HttpMethod.Post, "/api/catalog/categories", token, new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string token, string url, byte[] content, string fileName)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var request = DespachaApiFactory.Authorized(HttpMethod.Put, url, token);
        request.Content = new MultipartFormDataContent { { file, "file", fileName } };
        return client.SendAsync(request);
    }

    private static object Product(string code, string name, long categoryId, decimal unitPrice = 12000m, int? version = null) =>
        new { code, name, categoryId, unitPrice, packPrice = 130000m, unitsPerPack = 12, sellsByUnit = true, version };

    private static object Promotion(string code, long? productId, int priority) =>
        new { title = "Promo", description = "Dos por uno", code, productId, unitPrice = 20000m, priority };

    private sealed record PageDto<T>(T[] Items, int Total, int Page, int PageSize);

    private sealed record CategoryDto(long Id, string Name, bool IsActive, string? ImageKey, string? ImageUrl, int Version);

    private sealed record ProductDto(long Id, string Code, string Name, long? CategoryId, string? CategoryName, decimal UnitPrice, bool IsActive, int Version);

    private sealed record PromotionDto(long Id, string Code, long? ProductId, string? ProductName, int Priority);
}
