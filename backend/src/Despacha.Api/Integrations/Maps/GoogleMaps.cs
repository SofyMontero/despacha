using Microsoft.Extensions.Options;

namespace Despacha.Api.Integrations.Maps;

public sealed class MapOptions
{
    public const string SectionName = "Maps";

    /// <summary>Proveedor elegido en D-17. Hoy solo Google Maps Platform.</summary>
    public string Provider { get; set; } = "Google";

    /// <summary>Clave de Google Maps Platform. Va en user-secrets, nunca en el repositorio.</summary>
    public string? ApiKey { get; set; }
}

public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>Mapa, geocodificación y tiempo de llegada. El resto de la aplicación no habla con el proveedor.</summary>
public interface IMaps
{
    string Provider { get; }

    /// <summary>Hay clave para geocodificar y calcular rutas. Sin clave, el tiempo sale de la distancia en línea recta.</summary>
    bool CanRoute { get; }

    int EstimateMinutes(GeoPoint from, GeoPoint to);
}

/// <summary>
/// Google Maps Platform. La geocodificación y la ruta real esperan <see cref="MapOptions.ApiKey"/>.
/// Mientras no esté, el tiempo se estima a 25 km/h sobre la distancia en línea recta.
/// </summary>
internal sealed class GoogleMaps(IOptions<MapOptions> options) : IMaps
{
    public string Provider => "Google";

    public bool CanRoute => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public int EstimateMinutes(GeoPoint from, GeoPoint to)
    {
        var minutes = HaversineKm(from, to) / 25d * 60d;
        return (int)Math.Clamp(Math.Ceiling(minutes), 5, 180);
    }

    internal static double HaversineKm(GeoPoint from, GeoPoint to)
    {
        const double earthKm = 6371d;
        var lat1 = DegreesToRadians(from.Latitude);
        var lat2 = DegreesToRadians(to.Latitude);
        var dLat = DegreesToRadians(to.Latitude - from.Latitude);
        var dLng = DegreesToRadians(to.Longitude - from.Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
