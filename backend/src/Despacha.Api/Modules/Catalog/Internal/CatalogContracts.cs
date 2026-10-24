using Despacha.Api.Shared.Http;

namespace Despacha.Api.Modules.Catalog.Internal;

internal static class CatalogValidation
{
    /// <summary>Pesos con hasta 2 decimales, como la columna DECIMAL(12,2).</summary>
    public static RequestValidation Price(this RequestValidation validation, string field, decimal? value, bool mustBePositive)
    {
        if (value is not decimal price)
        {
            return validation.Add(field, "Es obligatorio.");
        }

        if (decimal.Round(price, 2) != price)
        {
            return validation.Add(field, "Máximo 2 decimales.");
        }

        return mustBePositive
            ? validation.Range(field, price, 0.01m, CatalogLimits.MaxPrice, "Debe ser mayor que 0.")
            : validation.Range(field, price, 0m, CatalogLimits.MaxPrice, "No puede ser negativo.");
    }

    public static RequestValidation RequireVersion(this RequestValidation validation, int? version, bool required) =>
        required && version is null ? validation.Add("Version", "Es obligatorio.") : validation;
}

internal sealed record StatusRequest(bool IsActive, int Version);

internal sealed record CategoryQuery(string? Q, bool? Active, int? Page, int? PageSize);

internal sealed record CategoryRequest(string? Name, int? Version);

internal sealed record CategoryResponse(long Id, string Name, bool IsActive, string? ImageKey, string? ImageUrl, int Version, DateTime? UpdatedAtUtc);

internal sealed record ProductQuery(string? Q, long? CategoryId, bool? Active, int? Page, int? PageSize);

internal sealed record ProductRequest(
    string? Code,
    string? Name,
    long? CategoryId,
    decimal? UnitPrice,
    decimal? PackPrice,
    int? UnitsPerPack,
    bool? SellsByUnit,
    bool? IsActive,
    int? Version);

internal sealed record ProductResponse(
    long Id,
    string Code,
    string Name,
    long? CategoryId,
    string? CategoryName,
    decimal UnitPrice,
    decimal PackPrice,
    int UnitsPerPack,
    bool SellsByUnit,
    bool IsActive,
    string? ImageKey,
    string? ImageUrl,
    int Version,
    DateTime? UpdatedAtUtc);

internal sealed record PromotionQuery(string? Q, long? ProductId, bool? Active, int? Page, int? PageSize);

internal sealed record PromotionRequest(
    string? Title,
    string? Description,
    string? Code,
    long? ProductId,
    decimal? UnitPrice,
    decimal? PackPrice,
    bool? SellsByUnit,
    int? Priority,
    bool? IsActive,
    int? Version);

internal sealed record PromotionResponse(
    long Id,
    string Title,
    string Description,
    string Code,
    long? ProductId,
    string? ProductName,
    decimal UnitPrice,
    decimal PackPrice,
    bool SellsByUnit,
    int Priority,
    bool IsActive,
    string? ImageKey,
    string? ImageUrl,
    DateTime? CreatedAtUtc,
    int Version,
    DateTime? UpdatedAtUtc);
