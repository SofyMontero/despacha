using System.Net;
using Despacha.Api.Integrations.WhatsApp;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Despacha.IntegrationTests;

public class ApiStartupTests
{
    [Fact]
    public async Task La_API_arranca_y_responde_health_live()
    {
        await using var factory = new DespachaApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_ready_revisa_la_base_de_control_sin_exponer_detalles()
    {
        await using var factory = new DespachaApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Las_dependencias_se_resuelven()
    {
        await using var factory = new DespachaApiFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.Equal(DespachaEnvironment.Local, services.GetRequiredService<DespachaRuntime>().Environment);
        Assert.NotNull(services.GetRequiredService<DatabaseSafetyGuard>());
        Assert.NotNull(services.GetRequiredService<ITenantContext>());
        Assert.NotNull(services.GetRequiredService<ITenantDbContextFactory<ProbeDbContext>>());
        Assert.Equal("DisabledWhatsAppSender", services.GetRequiredService<IWhatsAppSender>().GetType().Name);
        string[] expectedModules =
        [
            "Billing", "CashManagement", "Catalog", "Customers", "Deliveries", "FieldSales",
            "Notifications", "Orders", "Platform", "Receivables", "Tracking",
        ];
        Assert.Equal(expectedModules, services.GetServices<IModule>().Select(m => m.Name).Order().ToArray());
    }

    [Fact]
    public async Task Ambiente_Prod_en_host_Development_impide_el_arranque()
    {
        await using var factory = new DespachaApiFactory().WithSetting("Despacha:Environment", "Prod");

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Production", Flatten(ex));
    }

    [Fact]
    public async Task Host_Production_sin_ambiente_declarado_no_arranca()
    {
        await using var factory = new DespachaApiFactory { HostEnvironment = "Production" };

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Despacha:Environment", Flatten(ex));
    }

    [Theory]
    [InlineData("Server=mysql.produccion.example.com;Database=despacha_control;User ID=app;", "no está permitido")]
    [InlineData("Server=localhost;Database=control_prod;User ID=app;", "producción")]
    public async Task Una_base_de_control_no_local_o_de_produccion_impide_el_arranque(string connectionString, string expected)
    {
        await using var factory = new DespachaApiFactory().WithSetting("ConnectionStrings:Control", connectionString);

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains(expected, Flatten(ex));
    }

    [Fact]
    public async Task CORS_con_comodin_impide_el_arranque()
    {
        await using var factory = new DespachaApiFactory().WithSetting("Cors:AllowedOrigins:0", "*");

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("CORS", Flatten(ex));
    }

    private static string Flatten(Exception ex)
    {
        var messages = new List<string>();
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
            if (current is AggregateException aggregate)
            {
                messages.AddRange(aggregate.InnerExceptions.Select(inner => inner.Message));
            }
        }

        return string.Join(" | ", messages);
    }
}
