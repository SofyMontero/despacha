using Despacha.Api.Shared.Runtime;

namespace Despacha.UnitTests;

public class EnvironmentResolverTests
{
    [Fact]
    public void Sin_configuracion_el_ambiente_es_Local()
    {
        Assert.Equal(DespachaEnvironment.Local, DespachaEnvironmentResolver.Resolve(null, TestSupport.Host("Development")));
    }

    [Fact]
    public void Prod_exige_host_Production()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DespachaEnvironmentResolver.Resolve("Prod", TestSupport.Host("Development")));
        Assert.Contains("Production", ex.Message);
    }

    [Fact]
    public void Host_Production_no_puede_quedar_en_Local()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DespachaEnvironmentResolver.Resolve(null, TestSupport.Host("Production")));
    }

    [Theory]
    [InlineData("Test", "Staging", DespachaEnvironment.Test)]
    [InlineData("prod", "Production", DespachaEnvironment.Prod)]
    public void Acepta_combinaciones_validas(string configured, string host, DespachaEnvironment expected)
    {
        Assert.Equal(expected, DespachaEnvironmentResolver.Resolve(configured, TestSupport.Host(host)));
    }

    [Theory]
    [InlineData("staging")]
    [InlineData("1")]
    public void Rechaza_valores_desconocidos(string configured)
    {
        Assert.Throws<InvalidOperationException>(() =>
            DespachaEnvironmentResolver.Resolve(configured, TestSupport.Host("Development")));
    }
}
