using Despacha.Api.Modules.Orders.Internal;

namespace Despacha.UnitTests;

public class OrderWorkflowTests
{
    [Theory]
    [InlineData("1", "pendiente")]
    [InlineData("pendiente", "pendiente")]
    [InlineData("2", "aceptado")]
    [InlineData("aceptado", "aceptado")]
    [InlineData("enviado", "enviado")]
    [InlineData("finalizaCo", "finalizaCo")]
    [InlineData("finalizaContado", "finalizaCo")]
    [InlineData("finalizaCr", "finalizaCr")]
    [InlineData("finalizaCredito", "finalizaCr")]
    [InlineData("3", "rechazado")]
    [InlineData("rechazado", "rechazado")]
    public void Lee_los_estados_que_dejo_el_php(string raw, string expected) =>
        Assert.Equal(expected, OrderWorkflow.Persist(OrderWorkflow.Parse(raw)));

    [Fact]
    public void El_cambio_sigue_el_flujo_del_administrador()
    {
        Assert.True(OrderWorkflow.CanMove(OrderStage.Pending, OrderStage.Approved));
        Assert.True(OrderWorkflow.CanMove(OrderStage.Pending, OrderStage.Rejected));
        Assert.True(OrderWorkflow.CanMove(OrderStage.Approved, OrderStage.Dispatched));
        Assert.True(OrderWorkflow.CanMove(OrderStage.Dispatched, OrderStage.DeliveredCash));
        Assert.True(OrderWorkflow.CanMove(OrderStage.Dispatched, OrderStage.DeliveredCredit));
        Assert.False(OrderWorkflow.CanMove(OrderStage.Pending, OrderStage.Dispatched));
        Assert.False(OrderWorkflow.CanMove(OrderStage.DeliveredCash, OrderStage.Approved));
        Assert.Equal("finalizaCo", OrderWorkflow.Persist(OrderStage.DeliveredCash));
        Assert.Equal("finalizaCr", OrderWorkflow.Persist(OrderStage.DeliveredCredit));
    }

    [Fact]
    public void El_numero_continua_despues_del_mayor_de_ese_dia()
    {
        var day = new DateOnly(2026, 11, 11);
        var next = OrderWorkflow.NextNumber(day, ["PED-20261111-4821", "PED-20261110-9999", "otro"]);

        Assert.Equal("PED-20261111-4822", next);
        Assert.Equal("PED-20261111-0001", OrderWorkflow.NextNumber(day, []));
        Assert.Equal("PED-20261111-10000", OrderWorkflow.NextNumber(day, ["PED-20261111-9999"]));
    }
}
