using System.Globalization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Despacha.Api.Shared.Persistence;

/// <summary>Conversiones de columnas que ya existen y no deben reescribirse al leerlas.</summary>
public static class BusinessColumns
{
    public static PropertyBuilder<string?> EmptyImageAsNull(this PropertyBuilder<string?> property) =>
        property.HasConversion(value => value ?? "", value => string.IsNullOrEmpty(value) ? null : value);

    /// <summary>
    /// Una fecha cero llega como <see cref="DateTime.MinValue"/> cuando la conexión convierte ese valor.
    /// Se lee como null y no se sustituye por la fecha de la migración.
    /// </summary>
    public static PropertyBuilder<DateTime?> NullIfZeroDate(this PropertyBuilder<DateTime?> property) =>
        property.HasConversion(value => value, value => value.HasValue && value.Value.Year < 2 ? null : value);
}

public static class WholeNumbers
{
    public static bool TryParsePositiveInt(string? input, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    }
}
