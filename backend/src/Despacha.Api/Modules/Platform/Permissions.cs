using Microsoft.AspNetCore.Authorization;

namespace Despacha.Api.Modules.Platform;

public sealed record PermissionDefinition(string Code, string Module);

/// <summary>Catálogo fijo de permisos (D-03). Cada permiso pertenece a un módulo que la empresa debe tener activo (D-10).</summary>
public static class Permissions
{
    public const string MembersRead = "platform.members.read";
    public const string MembersManage = "platform.members.manage";
    public const string RolesRead = "platform.roles.read";
    public const string RolesManage = "platform.roles.manage";
    public const string CatalogRead = "catalog.read";
    public const string CatalogManage = "catalog.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersManage = "customers.manage";

    public const string PlatformModule = "Platform";

    public static readonly IReadOnlyList<string> Modules =
    [
        PlatformModule, "Catalog", "Customers", "Orders", "FieldSales", "Billing",
        "Receivables", "CashManagement", "Deliveries", "Tracking", "Notifications",
    ];

    public static readonly IReadOnlyList<PermissionDefinition> Catalog =
    [
        new(MembersRead, PlatformModule),
        new(MembersManage, PlatformModule),
        new(RolesRead, PlatformModule),
        new(RolesManage, PlatformModule),
        new(CatalogRead, "Catalog"),
        new(CatalogManage, "Catalog"),
        new(CustomersRead, "Customers"),
        new("customers.read.assigned", "Customers"),
        new(CustomersManage, "Customers"),
        new("orders.approve", "Orders"),
        new("orders.cancel", "Orders"),
        new("fieldSales.workday.manage", "FieldSales"),
        new("fieldSales.visit.create", "FieldSales"),
        new("fieldSales.order.create", "FieldSales"),
        new("fieldSales.assignCustomers", "FieldSales"),
        new("fieldSales.workday.readAll", "FieldSales"),
        new("fieldSales.route.read", "FieldSales"),
        new("deliveries.read.own", "Deliveries"),
        new("deliveries.claim", "Deliveries"),
        new("deliveries.updateStatus", "Deliveries"),
        new("deliveries.evidence", "Deliveries"),
        new("deliveries.plan", "Deliveries"),
        new("deliveries.assign", "Deliveries"),
        new("billing.register", "Billing"),
        new("billing.void", "Billing"),
        new("receivables.read.assigned", "Receivables"),
        new("receivables.credit.manage", "Receivables"),
        new("receivables.credit.override", "Receivables"),
        new("receivables.payment.register", "Receivables"),
        new("receivables.payment.collect", "Receivables"),
        new("receivables.adjust", "Receivables"),
        new("cash.movement.create", "CashManagement"),
        new("cash.movement.validate", "CashManagement"),
        new("cash.close", "CashManagement"),
        new("cash.reconcile", "CashManagement"),
        new("tracking.report", "Tracking"),
    ];

    private static readonly Dictionary<string, PermissionDefinition> ByCode =
        Catalog.ToDictionary(permission => permission.Code, StringComparer.Ordinal);

    public static bool Exists(string code) => ByCode.ContainsKey(code);

    public static string ModuleOf(string code) => ByCode[code].Module;
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public static class PermissionEndpointExtensions
{
    /// <summary>Exige sesión con empresa activa, membresía vigente, el permiso y su módulo activo.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        if (!Permissions.Exists(permission))
        {
            throw new ArgumentException($"El permiso '{permission}' no existe en el catálogo.", nameof(permission));
        }

        return builder.RequireAuthorization(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build());
    }
}
