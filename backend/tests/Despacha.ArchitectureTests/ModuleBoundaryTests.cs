using NetArchTest.Rules;

namespace Despacha.ArchitectureTests;

/// <summary>D-01: los límites entre módulos se cuidan con pruebas, no con el compilador.</summary>
public class ModuleBoundaryTests
{
    private const string Root = "Despacha.Api";

    private static readonly string[] Modules =
    [
        "Platform", "Catalog", "Customers", "Orders", "FieldSales", "Billing",
        "Receivables", "CashManagement", "Deliveries", "Tracking", "Notifications",
    ];

    public static IEnumerable<object[]> ModuleNames => Modules.Select(module => new object[] { module });

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Un_modulo_no_usa_la_implementacion_interna_de_otro(string module)
    {
        var forbidden = Modules.Where(other => other != module).Select(other => $"{Root}.Modules.{other}.Internal").ToArray();

        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace($"{Root}.Modules.{module}")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [InlineData("Shared")]
    [InlineData("Integrations")]
    public void Shared_e_Integrations_no_dependen_de_modulos(string layer)
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace($"{Root}.{layer}")
            .ShouldNot().HaveDependencyOn($"{Root}.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Existen_los_once_modulos_de_la_Fase_2()
    {
        var found = typeof(Program).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(Despacha.Api.Shared.Modules.IModule).IsAssignableFrom(type))
            .Select(type => type.Namespace!.Split('.')[^1])
            .Order()
            .ToArray();

        Assert.Equal(Modules.Order().ToArray(), found);
    }

    private static string Describe(TestResult result) =>
        "Tipos que violan la regla: " + string.Join(", ", result.FailingTypeNames ?? []);
}
