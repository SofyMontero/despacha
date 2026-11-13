namespace Despacha.Api.Modules.Orders.Internal;

/// <summary>
/// Estados que caben en <c>pedidos.ped_estado</c> (varchar 10), los mismos que escribe el PHP.
/// <c>finalizaContado</c> y <c>finalizaCredito</c> quedaron cortados en la base histórica.
/// </summary>
internal enum OrderStage
{
    Pending,
    Approved,
    Dispatched,
    DeliveredCash,
    DeliveredCredit,
    Rejected,
    Unknown,
}

internal static class OrderWorkflow
{
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Dispatch = "dispatch";
    public const string DeliverCash = "deliverCash";
    public const string DeliverCredit = "deliverCredit";

    public static OrderStage Parse(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "1" or "pendiente" => OrderStage.Pending,
        "2" or "aceptado" => OrderStage.Approved,
        "enviado" => OrderStage.Dispatched,
        "finalizaco" or "finalizacontado" => OrderStage.DeliveredCash,
        "finalizacr" or "finalizacredito" => OrderStage.DeliveredCredit,
        "3" or "rechazado" => OrderStage.Rejected,
        _ => OrderStage.Unknown,
    };

    public static string Persist(OrderStage stage) => stage switch
    {
        OrderStage.Pending => "pendiente",
        OrderStage.Approved => "aceptado",
        OrderStage.Dispatched => "enviado",
        OrderStage.DeliveredCash => "finalizaCo",
        OrderStage.DeliveredCredit => "finalizaCr",
        OrderStage.Rejected => "rechazado",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    public static string Label(OrderStage stage) => stage switch
    {
        OrderStage.Pending => "Pendiente",
        OrderStage.Approved => "Aceptado",
        OrderStage.Dispatched => "Enviado",
        OrderStage.DeliveredCash => "Finalizado de contado",
        OrderStage.DeliveredCredit => "Finalizado a crédito",
        OrderStage.Rejected => "Rechazado",
        _ => "Desconocido",
    };

    public static bool CanMove(OrderStage from, OrderStage to) => (from, to) switch
    {
        (OrderStage.Pending, OrderStage.Approved) => true,
        (OrderStage.Pending, OrderStage.Rejected) => true,
        (OrderStage.Approved, OrderStage.Dispatched) => true,
        (OrderStage.Dispatched, OrderStage.DeliveredCash) => true,
        (OrderStage.Dispatched, OrderStage.DeliveredCredit) => true,
        _ => false,
    };

    public static OrderStage? Target(string? action) => action switch
    {
        Approve => OrderStage.Approved,
        Reject => OrderStage.Rejected,
        Dispatch => OrderStage.Dispatched,
        DeliverCash => OrderStage.DeliveredCash,
        DeliverCredit => OrderStage.DeliveredCredit,
        _ => null,
    };

    public static string PermissionFor(string action) =>
        action == Reject ? "orders.cancel" : "orders.approve";

    public static IReadOnlyList<string> Matches(string? group) => group switch
    {
        "pending" => ["1", "pendiente"],
        "approved" => ["2", "aceptado"],
        "dispatch" => ["enviado"],
        "delivered" => ["finalizaCo", "finalizaCr", "finalizaContado", "finalizaCredito"],
        "rejected" => ["3", "rechazado"],
        _ => [],
    };

    /// <summary>Siguiente consecutivo del día. Los números históricos aleatorios suben el máximo de ese día.</summary>
    public static string NextNumber(DateOnly day, IEnumerable<string> numbersThatDay)
    {
        var prefix = $"PED-{day:yyyyMMdd}-";
        var max = 0;
        foreach (var number in numbersThatDay)
        {
            if (number.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(number[prefix.Length..], out var value))
            {
                max = Math.Max(max, value);
            }
        }

        var next = max + 1;
        return prefix + next.ToString(next > 9999 ? "0" : "0000");
    }
}
