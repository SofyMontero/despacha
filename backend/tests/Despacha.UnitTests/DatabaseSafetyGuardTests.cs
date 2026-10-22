using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;

namespace Despacha.UnitTests;

public class DatabaseSafetyGuardTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Local_permite_loopback(string host)
    {
        TestSupport.Guard(DespachaEnvironment.Local).EnsureAllowed(host, "despacha_control_local");
    }

    [Fact]
    public void Local_bloquea_servidor_remoto_aunque_este_en_la_lista_de_Test()
    {
        var guard = TestSupport.Guard(DespachaEnvironment.Local, "db.example.com");
        Assert.Throws<DatabaseConnectionBlockedException>(() => guard.EnsureAllowed("db.example.com", "despacha_local"));
    }

    [Fact]
    public void Local_bloquea_base_de_produccion_en_localhost()
    {
        var guard = TestSupport.Guard(DespachaEnvironment.Local);
        Assert.Throws<DatabaseConnectionBlockedException>(() => guard.EnsureAllowed("localhost", "control_prod"));
    }

    [Fact]
    public void Test_solo_permite_servidores_declarados()
    {
        var guard = TestSupport.Guard(DespachaEnvironment.Test, "mysql-test.interno");
        guard.EnsureAllowed("mysql-test.interno", "control_test");
        Assert.Throws<DatabaseConnectionBlockedException>(() => guard.EnsureAllowed("mysql-prod.interno", "control_test"));
        Assert.Throws<DatabaseConnectionBlockedException>(() => guard.EnsureAllowed("mysql-test.interno", "control_prod"));
    }

    [Fact]
    public void Test_sin_servidores_declarados_bloquea_todo()
    {
        Assert.Throws<DatabaseConnectionBlockedException>(() =>
            TestSupport.Guard(DespachaEnvironment.Test).EnsureAllowed("localhost", "control_test"));
    }

    [Fact]
    public void Prod_rechaza_bases_de_test_o_local()
    {
        var guard = TestSupport.Guard(DespachaEnvironment.Prod);
        guard.EnsureAllowed("mysql-prod.interno", "control_prod");
        Assert.Throws<DatabaseConnectionBlockedException>(() => guard.EnsureAllowed("mysql-prod.interno", "control_test"));
    }

    [Fact]
    public void Revisa_todos_los_servidores_de_la_cadena_de_conexion()
    {
        var guard = TestSupport.Guard(DespachaEnvironment.Local);
        guard.EnsureAllowed("Server=localhost;Database=despacha_control_local;User ID=despacha_local;");
        Assert.Throws<DatabaseConnectionBlockedException>(() =>
            guard.EnsureAllowed("Server=localhost,db.example.com;Database=despacha_control_local;User ID=x;"));
    }
}
