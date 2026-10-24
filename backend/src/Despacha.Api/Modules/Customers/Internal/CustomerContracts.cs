namespace Despacha.Api.Modules.Customers.Internal;

internal sealed record StatusRequest(bool IsActive, int Version);

internal sealed record EstablishmentQuery(string? Q, bool? Active, int? Page, int? PageSize);

internal sealed record EstablishmentRequest(string? Name, string? Address, int? Version);

internal sealed record EstablishmentResponse(long Id, string Name, string? Address, bool IsActive, int Version, DateTime? UpdatedAtUtc);

internal sealed record CustomerQuery(string? Q, string? Phone, long? EstablishmentId, string? Zone, bool? Active, int? Page, int? PageSize);

internal sealed record CustomerRequest(
    string? Name,
    string? Identification,
    string? Phone,
    string? Address,
    string? Zone,
    long? EstablishmentId,
    bool? IsActive,
    int? Version);

internal sealed record CustomerResponse(
    long Id,
    string Name,
    string? Identification,
    string Phone,
    string? Address,
    string? Zone,
    long? EstablishmentId,
    string? EstablishmentName,
    DateTime? RegisteredAtUtc,
    bool IsActive,
    int Version,
    DateTime? UpdatedAtUtc);

/// <summary>Respuesta de alta o edición. <c>SamePhoneCustomerIds</c> es la advertencia de teléfono repetido (no bloquea).</summary>
internal sealed record CustomerSaveResponse(CustomerResponse Customer, long[] SamePhoneCustomerIds);

internal sealed record PhoneCheckResponse(bool IsValid, string? Phone, long[] SamePhoneCustomerIds);
