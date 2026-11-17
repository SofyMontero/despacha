using Despacha.Api.Integrations.Maps;
using Microsoft.Extensions.Options;

namespace Despacha.UnitTests;

public sealed class GoogleMapsTests
{
    [Fact]
    public void Sin_clave_estima_el_tiempo_por_distancia()
    {
        var maps = new GoogleMaps(Options.Create(new MapOptions()));
        var here = new GeoPoint(4.6486, -74.0628);
        var nearby = new GeoPoint(4.6570, -74.0628);

        Assert.Equal("Google", maps.Provider);
        Assert.False(maps.CanRoute);
        Assert.InRange(maps.EstimateMinutes(here, nearby), 5, 15);
        Assert.Equal(5, maps.EstimateMinutes(here, here));
    }
}
