using Despacha.Api.Modules.Platform;
using Despacha.Api.Modules.Platform.Internal;
using Microsoft.AspNetCore.Builder;

namespace Despacha.UnitTests;

public class PermissionCatalogTests
{
    [Fact]
    public void Los_codigos_son_unicos_y_pertenecen_a_un_modulo_conocido()
    {
        Assert.Equal(Permissions.Catalog.Count, Permissions.Catalog.Select(p => p.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Permissions.Catalog, permission => Assert.Contains(permission.Module, Permissions.Modules));
    }

    [Fact]
    public void Los_roles_plantilla_usan_solo_permisos_del_catalogo()
    {
        Assert.Equal(["Administrador", "Domiciliario", "Vendedor"], RoleTemplates.Defaults.Keys.Order().ToArray());
        Assert.All(RoleTemplates.Defaults.Values.SelectMany(codes => codes), code => Assert.True(Permissions.Exists(code), code));
    }

    [Fact]
    public void Vendedor_y_Domiciliario_no_administran_la_plataforma()
    {
        var operational = RoleTemplates.Defaults[RoleTemplates.Seller].Concat(RoleTemplates.Defaults[RoleTemplates.Courier]);

        Assert.DoesNotContain(operational, code => code.StartsWith("platform.", StringComparison.Ordinal));
    }

    [Fact]
    public void Exigir_un_permiso_inexistente_falla_al_mapear()
    {
        var app = WebApplication.CreateBuilder().Build();

        Assert.Throws<ArgumentException>(() => app.MapGet("/x", () => "x").RequirePermission("no.existe"));
    }
}
